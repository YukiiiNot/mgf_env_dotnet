namespace MGF.UseCases.DeliveryEmail.SendDeliveryEmail;

using MGF.Provisioning;

public enum DeliveryEmailMode
{
    PreviewOnly,
    Send
}

public sealed record DeliveryEmailObservedRecipients(
    IReadOnlyList<string> To,
    string? ReplyTo);

public sealed record DeliveryEmailFile(
    string RelativePath,
    long SizeBytes,
    DateTimeOffset LastWriteTimeUtc);

public sealed record WorkerDeliveryEmailRequest(
    ProvisioningTokens Tokens,
    string ShareUrl,
    string VersionLabel,
    DateTimeOffset RetentionUntilUtc,
    IReadOnlyList<DeliveryEmailFile> Files,
    IReadOnlyList<string> Recipients,
    string? ObservedReplyTo,
    DeliveryEmailMode Mode);

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
