using System.Text.Json;
using MGF.Contracts.Abstractions.Integrations.Square;
using MGF.UseCases.Integrations.Square.IngestWebhook;

namespace MGF.UseCases.Tests;

public sealed class IngestSquareWebhookUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_PassesRecordAndPayload()
    {
        var store = new FakeSquareWebhookStore { InsertedCount = 1 };
        var useCase = new IngestSquareWebhookUseCase(store);

        var result = await useCase.ExecuteAsync(new IngestSquareWebhookRequest(
            SquareEventId: "evt_123",
            EventType: "payment.updated",
            ObjectType: "payment",
            ObjectId: "payment_456",
            LocationId: "loc_789",
            PayloadJson: "{\"event_id\":\"evt_123\"}"));

        Assert.Equal(1, result.InsertedEventCount);
        Assert.NotNull(store.Record);
        Assert.Equal("evt_123", store.Record!.SquareEventId);
        Assert.Equal("payment.updated", store.Record.EventType);
        Assert.Equal("payment", store.Record.ObjectType);
        Assert.Equal("payment_456", store.Record.ObjectId);
        Assert.Equal("loc_789", store.Record.LocationId);
        Assert.Equal("{\"event_id\":\"evt_123\"}", store.Record.PayloadJson);

        Assert.False(string.IsNullOrWhiteSpace(store.JobId));
        using var payload = JsonDocument.Parse(store.JobPayloadJson ?? "{}");
        var root = payload.RootElement;
        Assert.Equal("evt_123", root.GetProperty("square_event_id").GetString());
    }

    private sealed class FakeSquareWebhookStore : ISquareWebhookStore
    {
        public int InsertedCount { get; set; }
        public SquareWebhookEventRecord? Record { get; private set; }
        public string? JobId { get; private set; }
        public string? JobPayloadJson { get; private set; }

        public Task<int> InsertEventAsync(SquareWebhookEventRecord record, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Use InsertEventAndEnqueueJobAsync in this test.");
        }

        public Task<int> InsertEventAndEnqueueJobAsync(
            SquareWebhookEventRecord record,
            string jobId,
            string payloadJson,
            CancellationToken cancellationToken = default)
        {
            Record = record;
            JobId = jobId;
            JobPayloadJson = payloadJson;
            return Task.FromResult(InsertedCount);
        }

        public Task EnqueueProcessingJobAsync(
            string jobId,
            string payloadJson,
            string squareEventId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Use InsertEventAndEnqueueJobAsync in this test.");
        }

        public Task MarkFailedAsync(
            string squareEventId,
            string error,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Not used in this test.");
        }
    }
}
