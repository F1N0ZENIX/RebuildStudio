using System.Text.RegularExpressions;

namespace RebuildStudio.Core.Security;

public static class SecretRedactor
{
    static readonly (Regex Pattern, string Replacement)[] Rules =
    {
        (new Regex(@"(?i)(bearer\s+)[A-Za-z0-9._\-]{8,}", RegexOptions.Compiled), "$1***"),
        (new Regex(@"(?i)(api[_-]?key[""'\s:=]+)[A-Za-z0-9._\-]{8,}", RegexOptions.Compiled), "$1***"),
        (new Regex(@"sk-[A-Za-z0-9_\-]{8,}", RegexOptions.Compiled), "sk-***")
    };

    public static string Redact(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        foreach (var (p, r) in Rules) text = p.Replace(text, r);
        return text;
    }
}
