using System.Text;
using RebuildStudio.Core.Workspace;

namespace RebuildStudio.Core.Analysis;

public static class ReportFormatter
{
    public static string Format(PeReport r, CapabilityAssessment a, WorkspaceSession? s)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Arquivo: {r.FilePath}");
        sb.AppendLine($"Tamanho: {r.SizeBytes:N0} bytes");
        sb.AppendLine($"SHA-256: {r.Sha256}");
        sb.AppendLine($"PE válido: {(r.IsPe ? "sim" : "não")}{(r.IsDll ? " (DLL)" : "")}");
        sb.AppendLine($"Arquitetura: {r.Architecture}");
        sb.AppendLine($"Tipo detectado: {r.Kind}");
        if (r.AssemblyName is not null) sb.AppendLine($"Assembly: {r.AssemblyName} (CLR {r.ClrMetadataVersion})");
        if (r.ManagedAssemblyPath is not null && r.ManagedAssemblyPath != r.FilePath)
            sb.AppendLine($"Código gerenciado: {r.ManagedAssemblyPath}");
        if (r.Sections.Count > 0) sb.AppendLine($"Seções: {string.Join(", ", r.Sections)}");
        AppendList(sb, "Observações", r.Notes);
        AppendList(sb, "Limitações", r.Limitations);
        sb.AppendLine();
        sb.AppendLine($"Viabilidade: {a.Label} — {a.Summary}");
        AppendList(sb, "Disponível nesta versão", a.Available);
        AppendList(sb, "Indisponível", a.Unavailable);
        if (s is not null)
        {
            sb.AppendLine();
            sb.AppendLine("Workspace isolado criado (original preservado):");
            sb.AppendLine($"  Backup: {s.BackupPath}");
            sb.AppendLine($"  Cópia de trabalho: {s.WorkPath}");
        }
        return sb.ToString();
    }

    static void AppendList(StringBuilder sb, string title, IReadOnlyList<string> items)
    {
        if (items.Count == 0) return;
        sb.AppendLine($"{title}:");
        foreach (var i in items) sb.AppendLine($"  • {i}");
    }
}
