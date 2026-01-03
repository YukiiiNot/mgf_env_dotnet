namespace MGF.UseCases.Operations.RootIntegrity.RunRootIntegrity;

public interface IRunRootIntegrityUseCase
{
    Task<RunRootIntegrityResult> ExecuteAsync(
        RunRootIntegrityRequest request,
        CancellationToken cancellationToken = default);
}
