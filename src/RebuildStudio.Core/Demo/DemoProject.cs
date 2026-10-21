using RebuildStudio.Core.Workspace;

namespace RebuildStudio.Core.Demo;

/// <summary>Projeto .NET de teste controlado usado para demonstrar o fluxo real: patch → compilar → validar → exportar.</summary>
public static class DemoProject
{
    public const string ProjectFile = "DemoApp.csproj";
    public const string ProgramFile = "Program.cs";
    public const string AssemblyFile = "DemoApp.dll";
    public const string OriginalGreeting = "Olá, mundo!";

    public static string? FindSampleDir(string startDir)
    {
        for (var dir = new DirectoryInfo(startDir); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "samples", "DemoApp");
            if (File.Exists(Path.Combine(candidate, ProjectFile))) return candidate;
        }
        return null;
    }

    public static PatchPlan ManualPlan(string newGreeting)
    {
        var text = (newGreeting ?? "").ReplaceLineEndings(" ").Trim();
        if (text.Length == 0) throw new ArgumentException("Informe o novo texto da saudação.");
        if (text.Length > 200) throw new ArgumentException("Texto longo demais (máx. 200 caracteres).");

        var escaped = text.Replace("\\", "\\\\").Replace("\"", "\\\"");
        var patch = new FilePatch(ProgramFile,
            $"const string Greeting = \"{OriginalGreeting}\";",
            $"const string Greeting = \"{escaped}\";");

        return new PatchPlan(
            $"Trocar a saudação do programa de teste para: {text}",
            new[] { patch },
            "Manual (sem IA)",
            ExpectedRuntimeText: text,
            ForbiddenRuntimeText: text == OriginalGreeting ? null : OriginalGreeting);
    }
}
