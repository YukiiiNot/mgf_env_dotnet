namespace MGF.UseCases.DeliveryEmail;

public interface ISendDeliveryEmailUseCase
{
    Task<SendDeliveryEmailResult> ExecuteAsync(
        SendDeliveryEmailRequest request,
        CancellationToken cancellationToken = default);
}
