namespace MGF.Contracts.Abstractions.RootIntegrity;

public interface IRootIntegrityStore
{
    Task<RootIntegrityContract?> GetContractAsync(
        string providerKey,
        string rootKey,
        CancellationToken cancellationToken = default);
}
