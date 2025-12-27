using System.Security.Cryptography;
using System.Text;

namespace MGF.Tools.LegacyAudit.Scanning;

internal static class DirectoryFingerprint
{
    public static string? Compute(IReadOnlyList<string> entries)
    {
        if (entries.Count == 0)
        {
            return null;
        }

        var sorted = entries.OrderBy(value => value, StringComparer.OrdinalIgnoreCase);
        var data = Encoding.UTF8.GetBytes(string.Join("\n", sorted));
        var hash = SHA256.HashData(data);
        return Convert.ToHexString(hash);
    }
}
