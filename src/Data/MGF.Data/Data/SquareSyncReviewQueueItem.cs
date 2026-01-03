namespace MGF.Data.Data;

using System.Text.Json;

public sealed class SquareSyncReviewQueueItem
{
    public Guid SquareSyncReviewId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string Status { get; set; } = "open";
    public string ReviewType { get; set; } = string.Empty;
    public string ProcessorKey { get; set; } = string.Empty;
    public string ProcessorPaymentId { get; set; } = string.Empty;
    public string SquareEventId { get; set; } = string.Empty;
    public string? SquareCustomerId { get; set; }
    public JsonElement Payload { get; set; }
    public string? Error { get; set; }
}


