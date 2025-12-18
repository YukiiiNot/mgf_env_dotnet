namespace MGF.Infrastructure.Data;

public sealed class SquareReconcileCursor
{
    public string ReconcileKey { get; set; } = string.Empty;
    public DateTimeOffset CursorAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

