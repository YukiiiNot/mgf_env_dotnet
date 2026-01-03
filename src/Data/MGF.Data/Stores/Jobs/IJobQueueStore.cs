namespace MGF.Data.Stores.Jobs;

public interface IJobQueueStore
{
    Task<JobClaimRecord?> TryClaimJobAsync(
        string lockedBy,
        TimeSpan lockDuration,
        CancellationToken cancellationToken = default);

    Task<int> ReapStaleRunningJobsAsync(CancellationToken cancellationToken = default);

    Task UpdateJobPayloadAsync(
        string jobId,
        string payloadJson,
        CancellationToken cancellationToken = default);

    Task MarkSucceededAsync(string jobId, CancellationToken cancellationToken = default);

    Task MarkFailedAsync(JobFailureUpdate update, CancellationToken cancellationToken = default);
}

public sealed record JobClaimRecord(
    string JobId,
    string JobTypeKey,
    int AttemptCount,
    int MaxAttempts,
    string PayloadJson);

public sealed record JobFailureUpdate(
    string JobId,
    string StatusKey,
    DateTimeOffset RunAfter,
    DateTimeOffset? FinishedAt,
    string ErrorText);
