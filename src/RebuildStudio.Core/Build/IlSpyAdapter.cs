using System.ComponentModel;

namespace RebuildStudio.Core.Build;

public sealed record DecompileResult(bool Success, int CsFiles, string OutputDir, IReadOnlyList<string> Log);

/// <summary>Integração com o ilspycmd (ILSpy CLI). Ferramenta externa e opcional: dotnet tool install -g ilspycmd</summary>
public sealed class IlSpyAdapter
{
    public async Task<bool> IsAvailableAsync(CancellationToken ct)
    {
        try
        {
            var r = await ProcessRunner.RunAsync("ilspycmd", new[] { "--version" },
                Environment.CurrentDirectory, null, TimeSpan.FromSeconds(20), ct);
            return r.ExitCode == 0 && !r.TimedOut;
        }
        catch (Win32Exception) { return false; }
    }

    public async Task<DecompileResult> DecompileAsync(
        string assemblyPath, string outputDir, Action<string>? onLine, CancellationToken ct)
    {
        if (!File.Exists(assemblyPath)) throw new FileNotFoundException("Assembly não encontrado.", assemblyPath);
        Directory.CreateDirectory(outputDir);

        var r = await ProcessRunner.RunAsync("ilspycmd", new[] { "-p", "-o", outputDir, assemblyPath },
            outputDir, onLine, TimeSpan.FromMinutes(5), ct);
        var count = Directory.GetFiles(outputDir, "*.cs", SearchOption.AllDirectories).Length;
        return new DecompileResult(r.ExitCode == 0 && !r.TimedOut && count > 0, count, outputDir, r.Lines);
    }
}
