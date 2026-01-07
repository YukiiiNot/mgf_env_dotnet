namespace MGF.Contracts.Abstractions.Operations.Jobs;

public interface IJobOpsStore
{
    Task EnsureJobTypeAsync(
        string jobTypeKey,
        string displayName,
        CancellationToken cancellationToken = default);

    Task<ExistingJob?> FindExistingJobAsync(
        string jobTypeKey,
        string entityTypeKey,
        string entityKey,
        CancellationToken cancellationToken = default);

    Task EnqueueJobAsync(
        JobEnqueueRequest request,
        CancellationToken cancellationToken = default);

    Task EnqueueRetryableJobAsync(
        RetryableJobEnqueueRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<JobSummary>> GetJobsAsync(
        string jobTypeKey,
        string entityTypeKey,
        string entityKey,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RootIntegrityJobSummary>> GetRootIntegrityJobsAsync(
        string entityKey,
        int limit,
        CancellationToken cancellationToken = default);

    Task<int> ResetJobsAsync(
        string projectId,
        string jobTypeKey,
        CancellationToken cancellationToken = default);

    Task<int> CountStaleRunningJobsAsync(CancellationToken cancellationToken = default);

    Task<int> RequeueStaleRunningJobsAsync(CancellationToken cancellationToken = default);
}

public sealed record ExistingJob(
    string JobId,
    string StatusKey);

public sealed record JobEnqueueRequest(
    string JobId,
    string JobTypeKey,
    string PayloadJson,
    string EntityTypeKey,
    string EntityKey);

public sealed record RetryableJobEnqueueRequest(
    string JobId,
    string JobTypeKey,
    string PayloadJson,
    string EntityTypeKey,
    string EntityKey,
    int AttemptCount,
    int MaxAttempts);

public sealed record JobSummary(
    string JobId,
    string StatusKey,
    int AttemptCount,
    DateTimeOffset RunAfter,
    DateTimeOffset? LockedUntil);

public sealed record RootIntegrityJobSummary(
    string JobId,
    string StatusKey,
    int AttemptCount,
    DateTimeOffset RunAfter,
    DateTimeOffset? LockedUntil,
    string PayloadJson);
