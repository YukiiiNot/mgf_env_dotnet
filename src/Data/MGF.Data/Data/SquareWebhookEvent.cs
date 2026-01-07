namespace MGF.Data.Data;

using System.Text.Json;

public sealed class SquareWebhookEvent
{
    public Guid SquareWebhookEventId { get; set; }
    public string SquareEventId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string? ObjectType { get; set; }
    public string? ObjectId { get; set; }
    public string? LocationId { get; set; }
    public JsonElement Payload { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public string Status { get; set; } = "received";
    public string? Error { get; set; }
}


