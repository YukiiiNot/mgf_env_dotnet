namespace MGF.Data.Stores.Delivery;

using System.Text.Json;

public interface IProjectDeliveryStore
{
    Task AppendDeliveryRunAsync(
        string projectId,
        JsonElement metadata,
        JsonElement runResult,
        CancellationToken cancellationToken = default);

    Task AppendDeliveryEmailAsync(
        string projectId,
        JsonElement metadata,
        JsonElement emailResult,
        CancellationToken cancellationToken = default);

    Task UpdateProjectStatusAsync(
        string projectId,
        string statusKey,
        CancellationToken cancellationToken = default);
}
