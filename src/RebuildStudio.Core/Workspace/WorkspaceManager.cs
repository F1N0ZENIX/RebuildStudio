namespace RebuildStudio.Core.Workspace;

public sealed record WorkspaceSession(
    string Id, string SessionDir, string SourcePath, string BackupPath, string WorkPath,
    string OutputDir, string LogDir, bool IsDirectory, string SourceSha256);

/// <summary>Cria workspaces isolados: backup somente leitura + cópia de trabalho. O original nunca é alterado.</summary>
public sealed class WorkspaceManager
{
    public string Root { get; }

    public WorkspaceManager(string root)
    {
        Root = Path.GetFullPath(root);
        Directory.CreateDirectory(Root);
    }

    public WorkspaceSession Create(string sourcePath)
    {
        sourcePath = Path.GetFullPath(sourcePath);
        var isDir = Directory.Exists(sourcePath);
        if (!isDir && !File.Exists(sourcePath))
            throw new FileNotFoundException("Origem não encontrada.", sourcePath);

        var name = SafeName(Path.GetFileName(sourcePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)));
        var id = $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..6]}";
        var dir = Path.Combine(Root, $"{id}-{name}");
        var backup = Path.Combine(dir, "original", name);
        var work = Path.Combine(dir, "work", name);
        var output = Path.Combine(dir, "out");
        var logs = Path.Combine(dir, "logs");
        foreach (var d in new[] { Path.GetDirectoryName(backup)!, Path.GetDirectoryName(work)!, output, logs })
            Directory.CreateDirectory(d);

        var srcHash = Hashing.Sha256Path(sourcePath);
        CopyAny(sourcePath, backup, isDir);
        CopyAny(sourcePath, work, isDir);

        if (Hashing.Sha256Path(backup) != srcHash)
            throw new IOException("Falha de integridade: o backup não confere com o original.");

        ClearReadOnly(work);
        MarkReadOnly(backup);
        return new WorkspaceSession(id, dir, sourcePath, backup, work, output, logs, isDir, srcHash);
    }

    public void Restore(WorkspaceSession s)
    {
        if (s.IsDirectory)
        {
            if (Directory.Exists(s.WorkPath)) ForceDelete(s.WorkPath);
            CopyAny(s.BackupPath, s.WorkPath, true);
        }
        else
        {
            File.Copy(s.BackupPath, s.WorkPath, true);
        }
        ClearReadOnly(s.WorkPath);
        if (Directory.Exists(s.OutputDir)) ForceDelete(s.OutputDir);
        Directory.CreateDirectory(s.OutputDir);

        if (Hashing.Sha256Path(s.WorkPath) != s.SourceSha256)
            throw new IOException("Falha de integridade após restaurar o backup.");
    }

    public IReadOnlyList<string> ListSessions() =>
        Directory.GetDirectories(Root).Select(d => Path.GetFileName(d)!).OrderByDescending(n => n).ToList();

    /// <summary>Resolve um caminho relativo garantindo que ele permaneça dentro de baseDir.</summary>
    public static string ResolveInside(string baseDir, string relative)
    {
        var b = Path.GetFullPath(baseDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(Path.Combine(b, relative));
        if (!full.StartsWith(b, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException($"Caminho fora do workspace autorizado: {relative}");
        return full;
    }

    public static void ForceDelete(string path)
    {
        if (File.Exists(path)) { File.SetAttributes(path, FileAttributes.Normal); File.Delete(path); return; }
        if (!Directory.Exists(path)) return;
        foreach (var f in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
            File.SetAttributes(f, FileAttributes.Normal);
        Directory.Delete(path, true);
    }

    static void CopyAny(string src, string dst, bool isDir)
    {
        if (isDir) CopyDirectory(src, dst);
        else File.Copy(src, dst, true);
    }

    static void CopyDirectory(string src, string dst)
    {
        Directory.CreateDirectory(dst);
        foreach (var f in Directory.GetFiles(src))
            File.Copy(f, Path.Combine(dst, Path.GetFileName(f)), true);
        foreach (var d in Directory.GetDirectories(src))
        {
            var n = Path.GetFileName(d);
            if (n is "bin" or "obj" or ".git") continue;
            CopyDirectory(d, Path.Combine(dst, n));
        }
    }

    static void MarkReadOnly(string path) => SetFlag(path, true);
    static void ClearReadOnly(string path) => SetFlag(path, false);

    static void SetFlag(string path, bool readOnly)
    {
        var files = File.Exists(path)
            ? new[] { path }
            : Directory.GetFiles(path, "*", SearchOption.AllDirectories);
        foreach (var f in files)
        {
            var a = File.GetAttributes(f);
            File.SetAttributes(f, readOnly ? a | FileAttributes.ReadOnly : a & ~FileAttributes.ReadOnly);
        }
    }

    static string SafeName(string n)
    {
        var bad = Path.GetInvalidFileNameChars();
        var s = new string(n.Select(c => bad.Contains(c) ? '_' : c).ToArray());
        return string.IsNullOrWhiteSpace(s) ? "item" : s;
    }
}
