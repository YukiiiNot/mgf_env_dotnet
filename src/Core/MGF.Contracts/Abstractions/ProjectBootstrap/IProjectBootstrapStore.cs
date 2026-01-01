namespace MGF.Contracts.Abstractions;

using System.Text.Json;

public interface IProjectBootstrapStore
{
    Task AppendProvisioningRunAsync(
        string projectId,
        JsonElement metadata,
        JsonElement runResult,
        CancellationToken cancellationToken = default);

    Task UpdateProjectStatusAsync(
        string projectId,
        string statusKey,
        CancellationToken cancellationToken = default);

    Task<string?> UpsertProjectStorageRootAsync(
        string projectId,
        string storageProviderKey,
        string rootKey,
        string folderRelpath,
        CancellationToken cancellationToken = default);
}
