namespace MGF.Worker.ProjectArchive;

internal static class ProjectArchiveGuards
{
    internal const string StatusToArchive = "to_archive";
    internal const string StatusArchiving = "archiving";
    internal const string StatusArchived = "archived";
    internal const string StatusArchiveFailed = "archive_failed";

    internal static bool TryValidateStart(
        string? statusKey,
        bool force,
        out string? error,
        out bool alreadyArchiving)
    {
        alreadyArchiving = string.Equals(statusKey, StatusArchiving, StringComparison.OrdinalIgnoreCase);
        if (alreadyArchiving)
        {
            error = "Project is already archiving.";
            return false;
        }

        if (force)
        {
            error = null;
            return true;
        }

        if (string.Equals(statusKey, StatusToArchive, StringComparison.OrdinalIgnoreCase)
            || string.Equals(statusKey, StatusArchiveFailed, StringComparison.OrdinalIgnoreCase))
        {
            error = null;
            return true;
        }

        if (string.Equals(statusKey, StatusArchived, StringComparison.OrdinalIgnoreCase))
        {
            error = "Project is already archived.";
            return false;
        }

        error = "Project status is not eligible for archiving.";
        return false;
    }
}
