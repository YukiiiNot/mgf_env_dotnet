namespace MGF.Contracts.Abstractions.ProjectDelivery;

using System.Text.Json;
using MGF.Contracts.Abstractions.Email;

public interface IProjectDeliveryExecutor
{
    Task<ProjectDeliverySourceResult> ResolveLucidlinkSourceAsync(
        ProjectDeliveryPayload payload,
        string? storageRelpath,
        CancellationToken cancellationToken = default);

    Task<ProjectDeliveryTargetResult> ProcessDropboxAsync(
        ProjectDeliveryPayload payload,
        DeliveryTokens tokens,
        ProjectDeliverySourceResult source,
        string dropboxDeliveryRelpath,
        JsonElement projectMetadata,
        CancellationToken cancellationToken = default);

    Task<EmailSendResult> SendDeliveryEmailAsync(
        ProjectDeliveryPayload payload,
        DeliveryTokens tokens,
        ProjectDeliverySourceResult source,
        ProjectDeliveryTargetResult target,
        CancellationToken cancellationToken = default);
}
