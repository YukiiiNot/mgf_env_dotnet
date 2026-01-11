namespace MGF.DevConsole.Desktop.Api;

using System.Net;
using System.Net.Http;
using System.Net.Http.Json;

public sealed class JobsApiClient : IJobsApiClient
{
    private readonly HttpClient httpClient;

    public JobsApiClient(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    public sealed record JobListItemDto(
        string JobId,
        string JobTypeKey,
        string StatusKey,
        int AttemptCount,
        DateTimeOffset CreatedAt,
        DateTimeOffset RunAfter,
        DateTimeOffset? LockedUntil,
        string? EntityTypeKey,
        string? EntityKey,
        bool HasError,
        string? LastErrorPreview
    );

    public sealed record JobsListCursorDto(
        DateTimeOffset CreatedAt,
        string JobId
    );

    public sealed record JobsListResponseDto(
        IReadOnlyList<JobListItemDto> Items,
        JobsListCursorDto? NextCursor
    );

    public sealed record JobDetailDto(
        string JobId,
        string JobTypeKey,
        string StatusKey,
        int AttemptCount,
        DateTimeOffset CreatedAt,
        DateTimeOffset RunAfter,
        DateTimeOffset? StartedAt,
        DateTimeOffset? FinishedAt,
        string? LastError,
        string? EntityTypeKey,
        string? EntityKey,
        System.Text.Json.JsonElement Payload,
        DateTimeOffset? LockedUntil
    );

    public async Task<JobsListResponseDto> GetJobsAsync(
        DateTimeOffset sinceUtc,
        int limit,
        JobsListCursorDto? cursor,
        string? statusKey,
        string? jobTypeKey,
        CancellationToken cancellationToken)
    {
        var url = BuildJobsListUrl(sinceUtc, limit, cursor, statusKey, jobTypeKey);
        using var response = await httpClient.GetAsync(url, cancellationToken);
        return await ReadResponseAsync<JobsListResponseDto>(response, cancellationToken, "jobs list");
    }

    public async Task<JobDetailDto> GetJobAsync(string jobId, CancellationToken cancellationToken)
    {
        var encodedId = Uri.EscapeDataString(jobId);
        using var response = await httpClient.GetAsync($"api/jobs/{encodedId}", cancellationToken);
        return await ReadResponseAsync<JobDetailDto>(response, cancellationToken, "job detail");
    }

    private static string BuildJobsListUrl(
        DateTimeOffset sinceUtc,
        int limit,
        JobsListCursorDto? cursor,
        string? statusKey,
        string? jobTypeKey)
    {
        var query = new List<string>
        {
            $"since={Uri.EscapeDataString(sinceUtc.ToString("O"))}",
            $"limit={limit}"
        };

        if (cursor is not null)
        {
            query.Add($"cursorCreatedAt={Uri.EscapeDataString(cursor.CreatedAt.ToString("O"))}");
            query.Add($"cursorJobId={Uri.EscapeDataString(cursor.JobId)}");
        }

        if (!string.IsNullOrWhiteSpace(statusKey))
        {
            query.Add($"statusKey={Uri.EscapeDataString(statusKey)}");
        }

        if (!string.IsNullOrWhiteSpace(jobTypeKey))
        {
            query.Add($"jobTypeKey={Uri.EscapeDataString(jobTypeKey)}");
        }

        return "api/jobs?" + string.Join("&", query);
    }

    private static async Task<T> ReadResponseAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken,
        string operation)
    {
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new JobsApiException(JobsApiFailure.Unauthorized, "Unauthorized (X-MGF-API-KEY rejected).");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new JobsApiException(
                JobsApiFailure.HttpError,
                $"Unexpected status code {(int)response.StatusCode} during {operation}.");
        }

        var result = await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
        if (result is null)
        {
            throw new JobsApiException(JobsApiFailure.InvalidResponse, $"Missing or invalid {operation} response.");
        }

        return result;
    }
}

public sealed class JobsApiException : Exception
{
    public JobsApiException(JobsApiFailure failure, string message)
        : base(message)
    {
        Failure = failure;
    }

    public JobsApiFailure Failure { get; }
}

public enum JobsApiFailure
{
    Unauthorized,
    HttpError,
    InvalidResponse
}
