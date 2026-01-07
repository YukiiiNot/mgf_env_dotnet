namespace MGF.Email.Models;

using MGF.FolderProvisioning;

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
