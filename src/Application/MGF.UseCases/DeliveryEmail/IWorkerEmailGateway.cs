namespace MGF.UseCases.DeliveryEmail;

using MGF.Contracts.Abstractions;

public interface IWorkerEmailGateway
{
    Task<DeliveryEmailAudit> SendDeliveryReadyAsync(
        WorkerDeliveryEmailRequest request,
        CancellationToken cancellationToken = default);
}
