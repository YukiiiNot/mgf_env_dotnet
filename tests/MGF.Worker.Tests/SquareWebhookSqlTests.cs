namespace MGF.Worker.Tests;

using MGF.Contracts.Abstractions.Integrations.Square;
using MGF.Data.Data;

public sealed class SquareWebhookSqlTests
{
    [Fact]
    public void BuildInsertEventCommand_UsesExpectedArguments()
    {
        var record = new SquareWebhookEventRecord(
            SquareEventId: "evt_123",
            EventType: "payment.updated",
            ObjectType: "payment",
            ObjectId: "payment_456",
            LocationId: "loc_789",
            PayloadJson: "{\"event_id\":\"evt_123\"}");

        var command = SquareWebhookSql.BuildInsertEventCommand(record);

        Assert.Contains("INSERT INTO public.square_webhook_events", command.Format);
        Assert.Contains("ON CONFLICT (square_event_id) DO NOTHING;", command.Format);

        var args = command.GetArguments();
        Assert.Equal(record.SquareEventId, args[0]);
        Assert.Equal(record.EventType, args[1]);
        Assert.Equal(record.ObjectType, args[2]);
        Assert.Equal(record.ObjectId, args[3]);
        Assert.Equal(record.LocationId, args[4]);
        Assert.Equal(record.PayloadJson, args[5]);
    }

    [Fact]
    public void BuildEnqueueProcessingJobCommand_UsesExpectedArguments()
    {
        var command = SquareWebhookSql.BuildEnqueueProcessingJobCommand(
            jobId: "job_123",
            payloadJson: "{\"square_event_id\":\"evt_123\"}",
            squareEventId: "evt_123");

        Assert.Contains("INSERT INTO public.jobs", command.Format);

        var args = command.GetArguments();
        Assert.Equal("job_123", args[0]);
        Assert.Equal("square.webhook_event.process", args[1]);
        Assert.Equal("{\"square_event_id\":\"evt_123\"}", args[2]);
        Assert.Equal("queued", args[3]);
        Assert.Equal("square_webhook_event", args[4]);
        Assert.Equal("evt_123", args[5]);
    }

    [Fact]
    public void BuildMarkFailedCommand_UsesExpectedArguments()
    {
        var command = SquareWebhookSql.BuildMarkFailedCommand("evt_123", "error text");

        Assert.Contains("UPDATE public.square_webhook_events", command.Format);

        var args = command.GetArguments();
        Assert.Equal("failed", args[0]);
        Assert.Equal("error text", args[1]);
        Assert.Equal("evt_123", args[2]);
    }
}
