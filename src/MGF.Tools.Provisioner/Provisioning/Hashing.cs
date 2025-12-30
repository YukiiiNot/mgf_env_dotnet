using System.Security.Cryptography;
using System.Text;

namespace MGF.Tools.Provisioner;

public static class Hashing
{
    public static string Sha256Hex(byte[] data)
    {
        var hash = SHA256.HashData(data);
        var builder = new StringBuilder(hash.Length * 2);
        foreach (var b in hash)
        {
            builder.Append(b.ToString("x2"));
        }

        return builder.ToString();
    }
}
