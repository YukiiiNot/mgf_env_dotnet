namespace MGF.UseCases.Operations.Jobs.EnqueueRootIntegrityJob;

public interface IEnqueueRootIntegrityJobUseCase
{
    Task<EnqueueRootIntegrityJobResult> ExecuteAsync(
        EnqueueRootIntegrityJobRequest request,
        CancellationToken cancellationToken = default);
}
