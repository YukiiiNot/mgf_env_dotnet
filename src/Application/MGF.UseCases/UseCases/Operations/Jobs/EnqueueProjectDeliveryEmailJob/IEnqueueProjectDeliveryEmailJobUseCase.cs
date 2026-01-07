namespace MGF.UseCases.Operations.Jobs.EnqueueProjectDeliveryEmailJob;

public interface IEnqueueProjectDeliveryEmailJobUseCase
{
    Task<EnqueueProjectDeliveryEmailJobResult> ExecuteAsync(
        EnqueueProjectDeliveryEmailJobRequest request,
        CancellationToken cancellationToken = default);
}
