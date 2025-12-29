namespace MGF.Worker.ProjectDelivery;

internal sealed record DeliveryManifest
{
    public int SchemaVersion { get; init; }
    public string DeliveryRunId { get; init; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; init; }
    public string ProjectId { get; init; } = string.Empty;
    public string ProjectCode { get; init; } = string.Empty;
    public string ProjectName { get; init; } = string.Empty;
    public string ClientName { get; init; } = string.Empty;
    public string SourcePath { get; init; } = string.Empty;
    public string DestinationPath { get; init; } = string.Empty;
    public string StablePath { get; init; } = string.Empty;
    public string VersionPath { get; init; } = string.Empty;
    public string VersionLabel { get; init; } = string.Empty;
    public string CurrentVersion { get; init; } = string.Empty;
    public string? StableShareUrl { get; init; }
    public DateTimeOffset RetentionUntilUtc { get; init; }
    public IReadOnlyList<DeliveryFileSummary> Files { get; init; } = Array.Empty<DeliveryFileSummary>();
}
