namespace MGF.DevConsole.Desktop.Hosting.Connection;

public sealed record ApiConnectionState(
    ApiConnectionStatus Status,
    string Message,
    DateTimeOffset LastUpdatedUtc,
    DateTimeOffset? LastSuccessUtc,
    string? ApiEnvironment)
{
    public static ApiConnectionState Initial()
    {
        return new ApiConnectionState(
            ApiConnectionStatus.Degraded,
            "Checking API connectivity...",
            DateTimeOffset.UtcNow,
            null,
            null);
    }
}
