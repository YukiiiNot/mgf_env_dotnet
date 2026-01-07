namespace MGF.Contracts.Abstractions.RootIntegrity;

public interface IRootIntegrityExecutor
{
    Task<RootIntegrityResult> ExecuteAsync(
        RootIntegrityPayload payload,
        RootIntegrityContract contract,
        string jobId,
        CancellationToken cancellationToken = default);
}
