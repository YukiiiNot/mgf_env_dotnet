namespace MGF.UseCases.Integrations.Square.IngestWebhook;

using System.Text.Json;
using MGF.Contracts.Abstractions.Integrations.Square;
using MGF.Domain.Entities;

public sealed class IngestSquareWebhookUseCase : IIngestSquareWebhookUseCase
{
    private readonly ISquareWebhookStore store;

    public IngestSquareWebhookUseCase(ISquareWebhookStore store)
    {
        this.store = store;
    }

    public async Task<IngestSquareWebhookResult> ExecuteAsync(
        IngestSquareWebhookRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var jobId = EntityIds.NewWithPrefix("job");
        var jobPayloadJson = JsonSerializer.Serialize(new { square_event_id = request.SquareEventId });

        var insertedEventCount = await store.InsertEventAndEnqueueJobAsync(
            new SquareWebhookEventRecord(
                SquareEventId: request.SquareEventId,
                EventType: request.EventType,
                ObjectType: request.ObjectType,
                ObjectId: request.ObjectId,
                LocationId: request.LocationId,
                PayloadJson: request.PayloadJson),
            jobId,
            jobPayloadJson,
            cancellationToken);

        return new IngestSquareWebhookResult(insertedEventCount);
    }
}
