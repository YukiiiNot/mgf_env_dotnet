namespace MGF.Domain.Tests;

using System.Security.Cryptography;
using System.Text;
using MGF.Api.Square;

public sealed class SquareWebhookVerifierTests
{
    [Fact]
    public void IsValid_ReturnsTrue_ForMatchingSignature()
    {
        var notificationUrl = "https://example.com/webhooks/square";
        var body = "{\"event_id\":\"evt_123\",\"type\":\"payment.updated\"}";
        var bodyBytes = Encoding.UTF8.GetBytes(body);
        var signatureKey = "test_signature_key";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(signatureKey));
        var expected = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(notificationUrl + body)));

        var verifier = new SquareWebhookVerifier();

        Assert.True(verifier.IsValid(notificationUrl, bodyBytes, signatureKey, expected));
        Assert.False(verifier.IsValid(notificationUrl, bodyBytes, signatureKey, expected + "x"));
        Assert.False(verifier.IsValid(notificationUrl, bodyBytes, signatureKey, "not-base64!!!"));
        Assert.False(verifier.IsValid(notificationUrl, bodyBytes, signatureKey, Convert.ToBase64String(new byte[] { 1 })));
    }
}
