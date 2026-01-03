namespace MGF.UseCases.Operations.ProjectDelivery.RunProjectDelivery;

public interface IRunProjectDeliveryUseCase
{
    Task<RunProjectDeliveryResult> ExecuteAsync(
        RunProjectDeliveryRequest request,
        CancellationToken cancellationToken = default);
}
