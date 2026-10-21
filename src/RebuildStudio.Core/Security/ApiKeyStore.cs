using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace RebuildStudio.Core.Security;

/// <summary>Guarda chaves de API cifradas com DPAPI (escopo do usuário atual do Windows).</summary>
[SupportedOSPlatform("windows")]
public sealed class ApiKeyStore
{
    static readonly byte[] Entropy = Encoding.UTF8.GetBytes("RebuildStudioAI.v1");
    readonly string _dir;

    public ApiKeyStore(string directory)
    {
        _dir = directory;
        Directory.CreateDirectory(_dir);
    }

    string FileFor(string provider) =>
        Path.Combine(_dir, new string(provider.Where(char.IsLetterOrDigit).ToArray()) + ".key");

    public void Save(string provider, string key)
    {
        var enc = ProtectedData.Protect(Encoding.UTF8.GetBytes(key.Trim()), Entropy, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(FileFor(provider), enc);
    }

    public string? Load(string provider)
    {
        var f = FileFor(provider);
        if (!File.Exists(f)) return null;
        try
        {
            return Encoding.UTF8.GetString(
                ProtectedData.Unprotect(File.ReadAllBytes(f), Entropy, DataProtectionScope.CurrentUser));
        }
        catch (CryptographicException) { return null; }
    }

    public bool Has(string provider) => File.Exists(FileFor(provider));

    public void Delete(string provider)
    {
        var f = FileFor(provider);
        if (File.Exists(f)) File.Delete(f);
    }
}
