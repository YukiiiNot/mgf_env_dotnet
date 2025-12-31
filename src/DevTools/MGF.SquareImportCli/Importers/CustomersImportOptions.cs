namespace MGF.Tools.SquareImport.Importers;

internal enum CustomersMinConfidenceToAutoLink
{
    None = 0,
    EmailOnly = 1,
    PhoneOnly = 2,
    EmailOrPhone = 3,
}

internal enum CustomersImportMode
{
    Square = 0,
    Applied = 1,
}

internal sealed record CustomersImportOptions(
    bool WriteReports,
    DirectoryInfo? ReportDir,
    bool Strict,
    CustomersMinConfidenceToAutoLink MinConfidenceToAutoLink,
    CustomersImportMode Mode
)
{
    public static bool TryParseMinConfidence(
        string? value,
        out CustomersMinConfidenceToAutoLink parsed,
        out string? error
    )
    {
        error = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            parsed = CustomersMinConfidenceToAutoLink.EmailOrPhone;
            return true;
        }

        var normalized = value.Trim();

        if (normalized.Equals("email_or_phone", StringComparison.OrdinalIgnoreCase))
        {
            parsed = CustomersMinConfidenceToAutoLink.EmailOrPhone;
            return true;
        }

        if (normalized.Equals("email_only", StringComparison.OrdinalIgnoreCase))
        {
            parsed = CustomersMinConfidenceToAutoLink.EmailOnly;
            return true;
        }

        if (normalized.Equals("phone_only", StringComparison.OrdinalIgnoreCase))
        {
            parsed = CustomersMinConfidenceToAutoLink.PhoneOnly;
            return true;
        }

        if (normalized.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            parsed = CustomersMinConfidenceToAutoLink.None;
            return true;
        }

        parsed = CustomersMinConfidenceToAutoLink.EmailOrPhone;
        error = "Use one of: email_or_phone, email_only, phone_only, none.";
        return false;
    }

    public static bool TryParseMode(string? value, out CustomersImportMode parsed, out string? error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            parsed = CustomersImportMode.Square;
            return true;
        }

        var normalized = value.Trim();

        if (normalized.Equals("square", StringComparison.OrdinalIgnoreCase))
        {
            parsed = CustomersImportMode.Square;
            return true;
        }

        if (normalized.Equals("applied", StringComparison.OrdinalIgnoreCase))
        {
            parsed = CustomersImportMode.Applied;
            return true;
        }

        parsed = CustomersImportMode.Square;
        error = "Use one of: square, applied.";
        return false;
    }
}
