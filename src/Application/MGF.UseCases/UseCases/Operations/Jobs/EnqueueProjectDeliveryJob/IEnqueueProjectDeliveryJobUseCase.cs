namespace MGF.UseCases.Operations.Jobs.EnqueueProjectDeliveryJob;

public interface IEnqueueProjectDeliveryJobUseCase
{
    Task<EnqueueProjectDeliveryJobResult> ExecuteAsync(
        EnqueueProjectDeliveryJobRequest request,
        CancellationToken cancellationToken = default);
}
