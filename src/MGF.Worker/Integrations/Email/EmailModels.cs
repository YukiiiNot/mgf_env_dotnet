namespace MGF.Worker.Integrations.Email;

public sealed record DeliveryEmailRequest(
    string FromAddress,
    IReadOnlyList<string> To,
    string Subject,
    string BodyText,
    string? ReplyTo);

public sealed record DeliveryEmailResult(
    string Status,
    string Provider,
    string FromAddress,
    IReadOnlyList<string> To,
    string Subject,
    DateTimeOffset? SentAtUtc,
    string? ProviderMessageId,
    string? Error,
    string? ReplyTo);

public interface IEmailSender
{
    Task<DeliveryEmailResult> SendAsync(DeliveryEmailRequest request, CancellationToken cancellationToken);
}
