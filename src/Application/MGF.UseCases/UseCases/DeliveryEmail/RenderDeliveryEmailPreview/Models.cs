namespace MGF.UseCases.DeliveryEmail.RenderDeliveryEmailPreview;

using MGF.Contracts.Abstractions.Email;
using MGF.Email.Models;
using MGF.FolderProvisioning;

public sealed record RenderDeliveryEmailPreviewRequest(
    ProvisioningTokens Tokens,
    string ShareUrl,
    string VersionLabel,
    DateTimeOffset RetentionUntilUtc,
    IReadOnlyList<DeliveryEmailFileSummary> Files,
    IReadOnlyList<string> Recipients,
    string? ReplyTo,
    string? LogoUrl,
    string? FromName);

public sealed record RenderDeliveryEmailPreviewResult(
    EmailMessage Message);

