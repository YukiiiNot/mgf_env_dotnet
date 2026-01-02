namespace MGF.UseCases.Integrations.Square.IngestWebhook;

public sealed record IngestSquareWebhookRequest(
    string SquareEventId,
    string EventType,
    string? ObjectType,
    string? ObjectId,
    string? LocationId,
    string PayloadJson);

public sealed record IngestSquareWebhookResult(
    int InsertedEventCount);
