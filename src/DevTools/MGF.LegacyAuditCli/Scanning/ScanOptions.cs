namespace MGF.Tools.LegacyAudit.Scanning;

internal sealed class ScanOptions
{
    public required string RootPath { get; init; }
    public required string OutputPath { get; init; }
    public ScanProfile Profile { get; init; } = ScanProfile.Editorial;
    public int MaxDepth { get; init; } = -1;
}
