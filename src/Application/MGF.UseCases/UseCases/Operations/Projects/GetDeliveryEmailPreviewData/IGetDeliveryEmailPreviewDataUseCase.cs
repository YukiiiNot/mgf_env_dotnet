namespace MGF.UseCases.Operations.Projects.GetDeliveryEmailPreviewData;

public interface IGetDeliveryEmailPreviewDataUseCase
{
    Task<GetDeliveryEmailPreviewDataResult?> ExecuteAsync(
        GetDeliveryEmailPreviewDataRequest request,
        CancellationToken cancellationToken = default);
}
