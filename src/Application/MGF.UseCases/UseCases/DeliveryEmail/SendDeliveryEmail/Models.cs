namespace MGF.UseCases.DeliveryEmail.SendDeliveryEmail;

using MGF.Email.Models;

public sealed record SendDeliveryEmailRequest(
    string ProjectId,
    string? DeliveryVersionId,
    IReadOnlyList<string> EditorInitials,
    DeliveryEmailMode Mode,
    DeliveryEmailObservedRecipients? ObservedRecipients);

public sealed record SendDeliveryEmailResult(
    string Status,
    string? Provider,
    string? ProviderMessageId,
    IReadOnlyList<string> To,
    string Subject,
    string? StableFinalLinkUsed,
    bool AuditRecorded,
    string? Error);

