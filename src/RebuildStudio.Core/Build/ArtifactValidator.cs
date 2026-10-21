using System.Text;
using RebuildStudio.Core.Analysis;

namespace RebuildStudio.Core.Build;

public sealed record ValidationResult(bool Passed, IReadOnlyList<string> Checks);

public static class ArtifactValidator
{
    /// <summary>Valida que o arquivo é um assembly .NET e (opcionalmente) contém/não contém um texto literal.</summary>
    public static ValidationResult ValidateAssembly(string dllPath, string? mustContain, string? mustNotContain)
    {
        if (!File.Exists(dllPath))
            return new ValidationResult(false, new[] { $"FALHA: artefato não encontrado: {dllPath}" });

        var checks = new List<string>();
        var ok = true;

        var rep = new PeAnalyzer().Analyze(dllPath);
        if (rep.IsPe && rep.Kind == ExecutableKind.DotNet)
            checks.Add($"OK: assembly .NET válido ({rep.AssemblyName}), SHA-256 {rep.Sha256[..16]}…");
        else
        {
            ok = false;
            checks.Add("FALHA: o arquivo gerado não é um assembly .NET válido.");
        }

        var bytes = File.ReadAllBytes(dllPath);
        if (mustContain is not null)
        {
            var found = ContainsUtf16(bytes, mustContain);
            ok &= found;
            checks.Add(found ? $"OK: contém o texto esperado “{mustContain}”."
                             : $"FALHA: texto esperado “{mustContain}” não encontrado no binário.");
        }
        if (mustNotContain is not null)
        {
            var found = ContainsUtf16(bytes, mustNotContain);
            ok &= !found;
            checks.Add(!found ? $"OK: o texto antigo “{mustNotContain}” não está mais no binário."
                              : $"FALHA: o texto antigo “{mustNotContain}” ainda está no binário.");
        }
        return new ValidationResult(ok, checks);
    }

    // Literais de string de assemblies .NET ficam no heap #US em UTF-16LE.
    static bool ContainsUtf16(byte[] data, string text) =>
        data.AsSpan().IndexOf(Encoding.Unicode.GetBytes(text)) >= 0;
}
