namespace MGF.Contracts.Abstractions.ProjectDelivery;

public interface IProjectDeliveryData
{
    Task<string?> GetProjectStorageRootRelpathAsync(
        string projectId,
        string storageProviderKey,
        bool testMode,
        CancellationToken cancellationToken = default);

    Task<string> GetDropboxDeliveryRelpathAsync(
        CancellationToken cancellationToken = default);
}
