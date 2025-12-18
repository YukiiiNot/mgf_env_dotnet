namespace MGF.Api.Square;

using System.Security.Cryptography;
using System.Text;

public sealed class SquareWebhookVerifier : ISquareWebhookVerifier
{
    // Square signature: base64(HMAC-SHA256(signatureKey, notificationUrl + requestBody))
    public bool IsValid(string notificationUrl, byte[] bodyBytes, string signatureKey, string providedSignature)
    {
        if (string.IsNullOrWhiteSpace(notificationUrl))
        {
            return false;
        }

        if (bodyBytes.Length == 0)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(signatureKey))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(providedSignature))
        {
            return false;
        }

        var keyBytes = Encoding.UTF8.GetBytes(signatureKey);
        var urlBytes = Encoding.UTF8.GetBytes(notificationUrl);

        var messageBytes = new byte[urlBytes.Length + bodyBytes.Length];
        Buffer.BlockCopy(urlBytes, 0, messageBytes, 0, urlBytes.Length);
        Buffer.BlockCopy(bodyBytes, 0, messageBytes, urlBytes.Length, bodyBytes.Length);

        byte[] expectedHash;
        using (var hmac = new HMACSHA256(keyBytes))
        {
            expectedHash = hmac.ComputeHash(messageBytes);
        }

        if (!TryDecodeBase64(providedSignature.Trim(), out var providedBytes))
        {
            CryptographicOperations.FixedTimeEquals(expectedHash, ZeroHashBuffer);
            return false;
        }

        if (providedBytes.Length != expectedHash.Length)
        {
            CryptographicOperations.FixedTimeEquals(expectedHash, ZeroHashBuffer);
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(expectedHash, providedBytes);
    }

    private static bool TryDecodeBase64(string base64, out byte[] bytes)
    {
        try
        {
            bytes = Convert.FromBase64String(base64);
            return true;
        }
        catch (FormatException)
        {
            bytes = Array.Empty<byte>();
            return false;
        }
    }

    private static readonly byte[] ZeroHashBuffer = new byte[32];
}
