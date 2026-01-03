namespace MGF.UseCases.DeliveryEmail.RenderDeliveryEmailPreview;

public interface IRenderDeliveryEmailPreviewUseCase
{
    Task<RenderDeliveryEmailPreviewResult> ExecuteAsync(
        RenderDeliveryEmailPreviewRequest request,
        CancellationToken cancellationToken = default);
}
