namespace MGF.Worker.Email.Sending;

using MGF.Worker.Email.Models;

public interface IEmailSender
{
    Task<DeliveryEmailResult> SendAsync(DeliveryEmailRequest request, CancellationToken cancellationToken);
}
