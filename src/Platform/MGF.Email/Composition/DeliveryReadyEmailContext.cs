namespace MGF.Email.Composition;

using MGF.Email.Models;
using MGF.Provisioning;

public sealed record DeliveryReadyEmailContext(
    ProvisioningTokens Tokens,
    string ShareUrl,
    string VersionLabel,
    DateTimeOffset RetentionUntilUtc,
    IReadOnlyList<DeliveryEmailFileSummary> Files,
    IReadOnlyList<string> Recipients,
    string? ReplyTo,
    string? LogoUrl,
    string? FromName);

