namespace MGF.LegacyAuditCli.Scanning;

internal enum ScanProfile
{
    Editorial,
    Everything
}

internal static class ScanProfileRules
{
    public static bool TryParse(string? value, out ScanProfile profile)
    {
        profile = ScanProfile.Editorial;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        return Enum.TryParse(value, true, out profile);
    }

    public static IReadOnlyList<string> GetDefaultExcludes(ScanProfile profile)
    {
        return profile switch
        {
            ScanProfile.Editorial => new[]
            {
                "$RECYCLE.BIN",
                "System Volume Information",
                ".Trash",
                ".TemporaryItems",
                ".Spotlight-V100",
                "__MACOSX"
            },
            _ => Array.Empty<string>()
        };
    }
}

