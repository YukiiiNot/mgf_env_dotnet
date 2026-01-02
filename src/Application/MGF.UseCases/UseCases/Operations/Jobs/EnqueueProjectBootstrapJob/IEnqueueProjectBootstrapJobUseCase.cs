namespace MGF.UseCases.Operations.Jobs.EnqueueProjectBootstrapJob;

public interface IEnqueueProjectBootstrapJobUseCase
{
    Task<EnqueueProjectBootstrapJobResult> ExecuteAsync(
        EnqueueProjectBootstrapJobRequest request,
        CancellationToken cancellationToken = default);
}
