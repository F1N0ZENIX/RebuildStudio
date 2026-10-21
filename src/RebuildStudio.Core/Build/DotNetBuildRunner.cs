using System.ComponentModel;
using RebuildStudio.Core.Workspace;

namespace RebuildStudio.Core.Build;

public sealed record BuildResult(
    bool Success, int ExitCode, string OutputDir,
    IReadOnlyList<string> Artifacts, IReadOnlyList<string> Errors, IReadOnlyList<string> Log);

public sealed class DotNetBuildRunner
{
    /// <summary>
    /// Compila um .csproj da cópia de trabalho. "Sucesso" exige exit code 0 E a existência do assembly principal.
    /// </summary>
    public async Task<BuildResult> BuildAsync(
        WorkspaceSession s, string projectRelativePath, Action<string>? onLine, CancellationToken ct)
    {
        var proj = WorkspaceManager.ResolveInside(s.WorkPath, projectRelativePath);
        if (!proj.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) || !File.Exists(proj))
            throw new FileNotFoundException("Projeto .csproj não encontrado no workspace.", projectRelativePath);

        var outDir = Path.Combine(s.OutputDir, "build");
        if (Directory.Exists(outDir)) WorkspaceManager.ForceDelete(outDir);
        Directory.CreateDirectory(outDir);

        var args = new[]
        {
            "build", proj, "-c", "Release", "-o", outDir,
            "--nologo", "-v", "minimal", "-nodeReuse:false", "-p:UseSharedCompilation=false"
        };

        ProcessResult result;
        try
        {
            result = await ProcessRunner.RunAsync("dotnet", args, Path.GetDirectoryName(proj)!,
                onLine, TimeSpan.FromMinutes(5), ct);
        }
        catch (Win32Exception ex)
        {
            return new BuildResult(false, -1, outDir, Array.Empty<string>(),
                new[] { "SDK .NET ('dotnet') não encontrado no PATH: " + ex.Message }, Array.Empty<string>());
        }

        var errors = result.Lines
            .Where(l => l.Contains(": error ", StringComparison.OrdinalIgnoreCase))
            .Distinct().ToList();
        if (result.TimedOut) errors.Add("Tempo limite de compilação excedido.");

        var files = Directory.GetFiles(outDir).Select(f => Path.GetFileName(f)!).OrderBy(x => x).ToList();
        var primary = Path.GetFileNameWithoutExtension(proj) + ".dll";
        var ok = result.ExitCode == 0 && !result.TimedOut
                 && files.Contains(primary, StringComparer.OrdinalIgnoreCase);
        if (result.ExitCode == 0 && !result.TimedOut && !ok)
            errors.Add($"O compilador retornou sucesso, mas '{primary}' não foi produzido.");

        return new BuildResult(ok, result.ExitCode, outDir, files, errors, result.Lines);
    }
}
