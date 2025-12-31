namespace MGF.Worker.Email.Composition;

using MGF.Provisioning;
using MGF.Worker.ProjectDelivery;

public sealed record DeliveryReadyEmailContext(
    ProvisioningTokens Tokens,
    string ShareUrl,
    string VersionLabel,
    DateTimeOffset RetentionUntilUtc,
    IReadOnlyList<DeliveryFileSummary> Files,
    IReadOnlyList<string> Recipients,
    string? ReplyTo,
    string? LogoUrl,
    string? FromName);

