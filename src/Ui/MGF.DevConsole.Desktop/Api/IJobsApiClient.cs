namespace MGF.DevConsole.Desktop.Api;

public interface IJobsApiClient
{
    Task<JobsApiClient.JobsListResponseDto> GetJobsAsync(
        DateTimeOffset sinceUtc,
        int limit,
        JobsApiClient.JobsListCursorDto? cursor,
        string? statusKey,
        string? jobTypeKey,
        CancellationToken cancellationToken);

    Task<JobsApiClient.JobDetailDto> GetJobAsync(string jobId, CancellationToken cancellationToken);
}
