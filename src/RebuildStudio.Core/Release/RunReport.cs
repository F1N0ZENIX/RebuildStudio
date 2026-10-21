using System.Text;
using RebuildStudio.Core.Build;
using RebuildStudio.Core.Workspace;

namespace RebuildStudio.Core.Release;

public sealed record RunReport(
    WorkspaceSession Session, PatchPlan? Plan, IReadOnlyList<string> ApplyLog,
    BuildResult? Build, ValidationResult? Validation, DateTimeOffset GeneratedAt);

public static class ReportWriter
{
    public static string ToMarkdown(RunReport r)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Relatório — Rebuild Studio AI");
        sb.AppendLine();
        sb.AppendLine($"- Gerado em: {r.GeneratedAt:yyyy-MM-dd HH:mm:ss zzz}");
        sb.AppendLine($"- Sessão: `{r.Session.Id}`");
        sb.AppendLine($"- Origem: `{r.Session.SourcePath}`");
        sb.AppendLine($"- SHA-256 da origem: `{r.Session.SourceSha256}`");
        sb.AppendLine();

        sb.AppendLine("## Plano de alterações");
        if (r.Plan is null) sb.AppendLine("(nenhum)");
        else
        {
            sb.AppendLine($"- Origem do plano: {r.Plan.Source}");
            sb.AppendLine($"- Resumo: {r.Plan.Summary}");
            foreach (var l in r.ApplyLog) sb.AppendLine($"  - {l}");
        }
        sb.AppendLine();

        sb.AppendLine("## Compilação");
        if (r.Build is null) sb.AppendLine("(não executada)");
        else
        {
            sb.AppendLine($"- Resultado: **{(r.Build.Success ? "SUCESSO" : "FALHA")}** (exit code {r.Build.ExitCode})");
            foreach (var a in r.Build.Artifacts)
            {
                var p = Path.Combine(r.Build.OutputDir, a);
                if (File.Exists(p)) sb.AppendLine($"  - `{a}` — SHA-256 `{Hashing.Sha256File(p)}`");
            }
            if (r.Build.Errors.Count > 0)
            {
                sb.AppendLine("- Erros:");
                foreach (var e in r.Build.Errors.Take(30)) sb.AppendLine($"  - {e}");
            }
        }
        sb.AppendLine();

        sb.AppendLine("## Validação");
        if (r.Validation is null) sb.AppendLine("(não executada)");
        else
        {
            sb.AppendLine($"- Resultado: **{(r.Validation.Passed ? "APROVADA" : "REPROVADA")}**");
            foreach (var c in r.Validation.Checks) sb.AppendLine($"  - {c}");
        }
        sb.AppendLine();
        sb.AppendLine("> Este relatório descreve apenas o fluxo executado. Nenhum arquivo original foi alterado.");
        return sb.ToString();
    }
}

public sealed class ReleaseExporter
{
    /// <summary>Exporta artefatos + relatório, verificando a integridade de cada arquivo copiado.</summary>
    public string Export(RunReport r, string destinationRoot)
    {
        if (r.Build is null || !r.Build.Success || r.Validation is null || !r.Validation.Passed)
            throw new InvalidOperationException("Só é possível exportar um resultado compilado e validado.");

        var dest = Path.Combine(destinationRoot, "RebuildStudio-" + r.Session.Id);
        var art = Path.Combine(dest, "artefatos");
        Directory.CreateDirectory(art);

        foreach (var name in r.Build.Artifacts)
        {
            var src = Path.Combine(r.Build.OutputDir, name);
            var dst = Path.Combine(art, name);
            File.Copy(src, dst, true);
            if (Hashing.Sha256File(src) != Hashing.Sha256File(dst))
                throw new IOException($"Falha de integridade ao exportar '{name}'.");
        }

        File.WriteAllText(Path.Combine(dest, "relatorio.md"), ReportWriter.ToMarkdown(r), new System.Text.UTF8Encoding(false));
        return dest;
    }
}
