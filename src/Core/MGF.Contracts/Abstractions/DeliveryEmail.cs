namespace MGF.Contracts.Abstractions;

using System.Text.Json;

public interface IDeliveryEmailData
{
    Task<DeliveryEmailProject?> GetProjectAsync(string projectId, CancellationToken cancellationToken = default);

    Task RecordDeliveryEmailSentAsync(
        string projectId,
        JsonElement metadata,
        DeliveryEmailAudit emailResult,
        CancellationToken cancellationToken = default);
}

public sealed record DeliveryEmailProject(
    string ProjectId,
    string ProjectCode,
    string ProjectName,
    string ClientId,
    string? ClientName,
    JsonElement Metadata,
    IReadOnlyList<string> CanonicalRecipients);

public sealed record DeliveryEmailAudit(
    string Status,
    string Provider,
    string FromAddress,
    IReadOnlyList<string> To,
    string Subject,
    DateTimeOffset? SentAtUtc,
    string? ProviderMessageId,
    string? Error,
    string TemplateVersion,
    string? ReplyTo);
