namespace MGF.UseCases.DeliveryEmail.SendDeliveryEmail;

using MGF.Contracts.Abstractions;

public interface IWorkerEmailGateway
{
    Task<DeliveryEmailAudit> SendDeliveryReadyAsync(
        WorkerDeliveryEmailRequest request,
        CancellationToken cancellationToken = default);
}
