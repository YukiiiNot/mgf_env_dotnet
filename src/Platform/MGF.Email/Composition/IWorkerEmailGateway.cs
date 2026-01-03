namespace MGF.Email.Composition;

using MGF.Contracts.Abstractions;
using MGF.Email.Models;

public interface IWorkerEmailGateway
{
    Task<DeliveryEmailAudit> SendDeliveryReadyAsync(
        WorkerDeliveryEmailRequest request,
        CancellationToken cancellationToken = default);
}
