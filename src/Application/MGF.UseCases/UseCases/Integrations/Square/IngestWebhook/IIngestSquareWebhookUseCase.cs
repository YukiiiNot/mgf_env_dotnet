namespace MGF.UseCases.Integrations.Square.IngestWebhook;

public interface IIngestSquareWebhookUseCase
{
    Task<IngestSquareWebhookResult> ExecuteAsync(
        IngestSquareWebhookRequest request,
        CancellationToken cancellationToken = default);
}
