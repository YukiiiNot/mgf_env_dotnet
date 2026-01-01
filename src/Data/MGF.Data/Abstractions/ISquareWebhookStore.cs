namespace MGF.Data.Abstractions;

public interface ISquareWebhookStore
{
    Task<int> InsertEventAsync(SquareWebhookEventRecord record, CancellationToken cancellationToken = default);

    Task EnqueueProcessingJobAsync(
        string jobId,
        string payloadJson,
        string squareEventId,
        CancellationToken cancellationToken = default);

    Task MarkFailedAsync(
        string squareEventId,
        string error,
        CancellationToken cancellationToken = default);
}

public sealed record SquareWebhookEventRecord(
    string SquareEventId,
    string EventType,
    string? ObjectType,
    string? ObjectId,
    string? LocationId,
    string PayloadJson);
