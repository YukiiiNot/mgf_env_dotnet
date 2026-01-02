namespace MGF.Contracts.Abstractions.Integrations.Square;

public interface ISquareWebhookVerifier
{
    bool IsValid(string notificationUrl, byte[] bodyBytes, string signatureKey, string providedSignature);
}
