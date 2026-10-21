using System.Security.Cryptography;
using System.Text;

namespace RebuildStudio.Core;

public static class Hashing
{
    public static string Sha256File(string path)
    {
        using var fs = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(fs));
    }

    /// <summary>Hash de arquivo, ou de diretório (caminhos relativos + hash de cada arquivo, ignorando bin/obj/.git).</summary>
    public static string Sha256Path(string path)
    {
        if (File.Exists(path)) return Sha256File(path);
        var root = Path.GetFullPath(path);
        var files = Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/'))
            .Where(r => !IsExcluded(r))
            .OrderBy(r => r, StringComparer.Ordinal);
        using var h = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var rel in files)
        {
            var fileHash = Sha256File(Path.Combine(root, rel));
            h.AppendData(Encoding.UTF8.GetBytes(rel + ":" + fileHash + "\n"));
        }
        return Convert.ToHexString(h.GetHashAndReset());
    }

    public static bool IsExcluded(string relativePath)
    {
        var parts = relativePath.Replace('\\', '/').Split('/');
        return parts.Any(p => p is "bin" or "obj" or ".git");
    }
}
