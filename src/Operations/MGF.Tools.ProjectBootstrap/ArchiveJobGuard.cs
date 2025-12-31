internal static class ArchiveJobGuard
{
    public static bool ShouldEnqueue(ExistingJob? existingJob, out string? reason)
    {
        if (existingJob is null)
        {
            reason = null;
            return true;
        }

        reason = $"project.archive already queued/running (job_id={existingJob.JobId}, status={existingJob.StatusKey})";
        return false;
    }
}
