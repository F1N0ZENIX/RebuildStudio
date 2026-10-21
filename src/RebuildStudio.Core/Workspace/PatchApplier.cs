namespace RebuildStudio.Core.Workspace;

public sealed record FilePatch(string File, string Old, string New);

public sealed record PatchPlan(
    string Summary,
    IReadOnlyList<FilePatch> Patches,
    string Source,
    string? ExpectedRuntimeText = null,
    string? ForbiddenRuntimeText = null);

/// <summary>Aplica patches de texto somente na cópia de trabalho, de forma atômica e restrita.</summary>
public sealed class PatchApplier
{
    // Arquivos de build (.csproj, .props, .targets, .sln...) ficam de fora de propósito:
    // alterá-los poderia executar comandos arbitrários durante a compilação.
    static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
        { ".cs", ".txt", ".json", ".resx", ".md", ".xaml", ".html", ".css", ".js" };

    public IReadOnlyList<string> Apply(WorkspaceSession s, PatchPlan plan)
    {
        if (!s.IsDirectory)
            throw new InvalidOperationException("Patches de texto só se aplicam a projetos em diretório.");
        if (plan.Patches.Count == 0)
            throw new InvalidOperationException("O plano não contém alterações.");

        var pending = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var log = new List<string>();

        foreach (var p in plan.Patches)
        {
            if (string.IsNullOrEmpty(p.Old))
                throw new InvalidOperationException($"Patch em '{p.File}' sem texto original.");

            var path = WorkspaceManager.ResolveInside(s.WorkPath, p.File);
            var ext = Path.GetExtension(path);
            if (!Allowed.Contains(ext))
                throw new InvalidOperationException($"Extensão '{ext}' não permitida (arquivos de build não podem ser alterados): {p.File}");
            if (!File.Exists(path))
                throw new FileNotFoundException("Arquivo do patch não existe no workspace.", p.File);

            var text = pending.TryGetValue(path, out var cur) ? cur : File.ReadAllText(path);
            var count = Count(text, p.Old);
            if (count != 1)
                throw new InvalidOperationException($"Em '{p.File}' o trecho original aparece {count} vez(es); esperado exatamente 1.");

            pending[path] = text.Replace(p.Old, p.New, StringComparison.Ordinal);
            log.Add($"{p.File}: - {Short(p.Old)}  |  + {Short(p.New)}");
        }

        foreach (var kv in pending) File.WriteAllText(kv.Key, kv.Value);
        return log;
    }

    static int Count(string text, string value)
    {
        var n = 0;
        for (var i = text.IndexOf(value, StringComparison.Ordinal); i >= 0;
             i = text.IndexOf(value, i + value.Length, StringComparison.Ordinal)) n++;
        return n;
    }

    static string Short(string s)
    {
        s = s.ReplaceLineEndings(" ");
        return s.Length <= 120 ? s : s[..117] + "...";
    }
}
