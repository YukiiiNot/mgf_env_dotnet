namespace MGF.UseCases.DeliveryEmail.SendDeliveryEmail;

public interface ISendDeliveryEmailUseCase
{
    Task<SendDeliveryEmailResult> ExecuteAsync(
        SendDeliveryEmailRequest request,
        CancellationToken cancellationToken = default);
}
