namespace MGF.UseCases.Operations.Jobs;

using MGF.Contracts.Abstractions.Operations.Jobs;

internal static class BootstrapJobGuard
{
    public static bool ShouldEnqueue(ExistingJob? existingJob, out string? reason)
    {
        if (existingJob is null)
        {
            reason = null;
            return true;
        }

        reason = $"project.bootstrap already queued/running (job_id={existingJob.JobId}, status={existingJob.StatusKey})";
        return false;
    }
}

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

internal static class DeliveryJobGuard
{
    public static bool ShouldEnqueue(ExistingJob? existingJob, out string? reason)
    {
        if (existingJob is null)
        {
            reason = null;
            return true;
        }

        reason = $"project.delivery already queued/running (job_id={existingJob.JobId}, status={existingJob.StatusKey})";
        return false;
    }
}

internal static class DeliveryEmailJobGuard
{
    public static bool ShouldEnqueue(ExistingJob? existingJob, out string? reason)
    {
        if (existingJob is null)
        {
            reason = null;
            return true;
        }

        reason = $"project.delivery_email already queued/running (job_id={existingJob.JobId}, status={existingJob.StatusKey})";
        return false;
    }
}
