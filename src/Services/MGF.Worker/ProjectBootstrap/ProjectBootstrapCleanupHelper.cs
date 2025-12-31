namespace MGF.Worker.ProjectBootstrap;

internal static class ProjectBootstrapCleanupHelper
{
    internal static readonly TimeSpan[] DefaultBackoff =
    [
        TimeSpan.FromMilliseconds(100),
        TimeSpan.FromMilliseconds(250),
        TimeSpan.FromMilliseconds(500),
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2)
    ];

    internal static async Task<CleanupResult> TryDeleteWithRetryAsync(
        string targetPath,
        CancellationToken cancellationToken,
        Action<string>? deleteAction = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        deleteAction ??= path => Directory.Delete(path, recursive: true);
        delay ??= (span, token) => Task.Delay(span, token);

        Exception? lastException = null;

        for (var attempt = 0; attempt < DefaultBackoff.Length; attempt++)
        {
            try
            {
                deleteAction(targetPath);
                return new CleanupResult(true, null);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                lastException = ex;
                await delay(DefaultBackoff[attempt], cancellationToken);
            }
            catch (Exception ex)
            {
                return new CleanupResult(false, ex.Message);
            }
        }

        var message = lastException?.Message ?? "Cleanup failed due to an unknown error.";
        return new CleanupResult(false, $"{message} (locked; cleanup skipped)");
    }
}

internal sealed record CleanupResult(bool Success, string? Error);
