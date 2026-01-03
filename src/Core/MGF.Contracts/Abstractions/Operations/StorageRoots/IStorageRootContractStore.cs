namespace MGF.Contracts.Abstractions.Operations.StorageRoots;

public interface IStorageRootContractStore
{
    Task<StorageRootContract?> GetActiveContractAsync(
        string providerKey,
        string rootKey,
        CancellationToken cancellationToken = default);
}

public sealed record StorageRootContract(
    IReadOnlyList<string> RequiredFolders,
    IReadOnlyList<string> OptionalFolders,
    IReadOnlyList<string> AllowedExtras,
    IReadOnlyList<string> AllowedRootFiles,
    string? QuarantineRelpath);
