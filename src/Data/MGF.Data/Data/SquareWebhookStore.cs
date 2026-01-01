namespace MGF.Data.Data;

using Microsoft.EntityFrameworkCore;
using MGF.Data.Abstractions;

public sealed class SquareWebhookStore : ISquareWebhookStore
{
    private readonly AppDbContext db;

    public SquareWebhookStore(AppDbContext db)
    {
        this.db = db;
    }

    public Task<int> InsertEventAsync(SquareWebhookEventRecord record, CancellationToken cancellationToken = default)
    {
        return db.Database.ExecuteSqlInterpolatedAsync(
            SquareWebhookSql.BuildInsertEventCommand(record),
            cancellationToken);
    }

    public Task EnqueueProcessingJobAsync(
        string jobId,
        string payloadJson,
        string squareEventId,
        CancellationToken cancellationToken = default)
    {
        return db.Database.ExecuteSqlInterpolatedAsync(
            SquareWebhookSql.BuildEnqueueProcessingJobCommand(jobId, payloadJson, squareEventId),
            cancellationToken);
    }

    public Task MarkFailedAsync(string squareEventId, string error, CancellationToken cancellationToken = default)
    {
        return db.Database.ExecuteSqlInterpolatedAsync(
            SquareWebhookSql.BuildMarkFailedCommand(squareEventId, error),
            cancellationToken);
    }
}

internal static class SquareWebhookSql
{
    internal static FormattableString BuildInsertEventCommand(SquareWebhookEventRecord record)
    {
        return $"""
        INSERT INTO public.square_webhook_events (square_event_id, event_type, object_type, object_id, location_id, payload)
        VALUES ({record.SquareEventId}, {record.EventType}, {record.ObjectType}, {record.ObjectId}, {record.LocationId}, {record.PayloadJson}::jsonb)
        ON CONFLICT (square_event_id) DO NOTHING;
        """;
    }

    internal static FormattableString BuildEnqueueProcessingJobCommand(
        string jobId,
        string payloadJson,
        string squareEventId)
    {
        return $"""
        INSERT INTO public.jobs (job_id, job_type_key, payload, status_key, run_after, entity_type_key, entity_key)
        VALUES ({jobId}, {"square.webhook_event.process"}, {payloadJson}::jsonb, {"queued"}, now(), {"square_webhook_event"}, {squareEventId});
        """;
    }

    internal static FormattableString BuildMarkFailedCommand(string squareEventId, string error)
    {
        return $"""
        UPDATE public.square_webhook_events
        SET status = {"failed"},
            error = {error}
        WHERE square_event_id = {squareEventId};
        """;
    }
}
