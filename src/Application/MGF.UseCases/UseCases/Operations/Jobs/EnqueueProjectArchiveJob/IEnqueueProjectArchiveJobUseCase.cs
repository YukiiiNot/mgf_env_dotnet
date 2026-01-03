namespace MGF.UseCases.Operations.Jobs.EnqueueProjectArchiveJob;

public interface IEnqueueProjectArchiveJobUseCase
{
    Task<EnqueueProjectArchiveJobResult> ExecuteAsync(
        EnqueueProjectArchiveJobRequest request,
        CancellationToken cancellationToken = default);
}
