namespace MGF.Api.Services;

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MGF.Data.Data;

public sealed class JobsService
{
    private const int MaxListLimit = 200;
    private readonly AppDbContext db;

    public JobsService(AppDbContext db)
    {
        this.db = db;
    }

    public sealed record JobDto(
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
        JsonElement Payload,
        DateTimeOffset? LockedUntil
    );

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

    public sealed record JobsListResponseDto(
        IReadOnlyList<JobListItemDto> Items,
        JobsListCursorDto? NextCursor
    );

    public sealed record JobsListCursorDto(
        DateTimeOffset CreatedAt,
        string JobId
    );

    public async Task<JobsListResponseDto> GetJobsAsync(
        DateTimeOffset since,
        int limit,
        DateTimeOffset? cursorCreatedAt,
        string? cursorJobId,
        string? statusKey,
        string? jobTypeKey,
        CancellationToken cancellationToken)
    {
        var boundedLimit = Math.Clamp(limit, 1, MaxListLimit);

        var query = db
            .Set<Dictionary<string, object>>("jobs")
            .AsNoTracking()
            .Where(j => EF.Property<DateTimeOffset>(j, "created_at") >= since);

        if (!string.IsNullOrWhiteSpace(statusKey))
        {
            query = query.Where(j => EF.Property<string>(j, "status_key") == statusKey);
        }

        if (!string.IsNullOrWhiteSpace(jobTypeKey))
        {
            query = query.Where(j => EF.Property<string>(j, "job_type_key") == jobTypeKey);
        }

        if (cursorCreatedAt.HasValue && !string.IsNullOrWhiteSpace(cursorJobId))
        {
            var createdAt = cursorCreatedAt.Value;
            var jobId = cursorJobId;
            query = query.Where(j =>
                EF.Property<DateTimeOffset>(j, "created_at") < createdAt
                || (EF.Property<DateTimeOffset>(j, "created_at") == createdAt
                    && string.Compare(EF.Property<string>(j, "job_id"), jobId) < 0));
        }

        var results = await query
            .OrderByDescending(j => EF.Property<DateTimeOffset>(j, "created_at"))
            .ThenByDescending(j => EF.Property<string>(j, "job_id"))
            .Select(
                j => new JobListItemProjection(
                    EF.Property<string>(j, "job_id"),
                    EF.Property<string>(j, "job_type_key"),
                    EF.Property<string>(j, "status_key"),
                    EF.Property<int>(j, "attempt_count"),
                    EF.Property<DateTimeOffset>(j, "created_at"),
                    EF.Property<DateTimeOffset>(j, "run_after"),
                    EF.Property<DateTimeOffset?>(j, "locked_until"),
                    EF.Property<string?>(j, "entity_type_key"),
                    EF.Property<string?>(j, "entity_key"),
                    EF.Property<string?>(j, "last_error")
                )
            )
            .Take(boundedLimit)
            .ToListAsync(cancellationToken);

        var items = results
            .Select(
                job =>
                {
                    var hasError = !string.IsNullOrWhiteSpace(job.LastError);
                    return new JobListItemDto(
                        job.JobId,
                        job.JobTypeKey,
                        job.StatusKey,
                        job.AttemptCount,
                        job.CreatedAt,
                        job.RunAfter,
                        job.LockedUntil,
                        job.EntityTypeKey,
                        job.EntityKey,
                        hasError,
                        hasError ? GetErrorPreview(job.LastError) : null
                    );
                })
            .ToList();

        JobsListCursorDto? nextCursor = null;
        if (items.Count == boundedLimit)
        {
            var last = items[^1];
            nextCursor = new JobsListCursorDto(last.CreatedAt, last.JobId);
        }

        return new JobsListResponseDto(items, nextCursor);
    }

    public async Task<JobDto?> GetJobAsync(string jobId, CancellationToken cancellationToken)
    {
        return await db
            .Set<Dictionary<string, object>>("jobs")
            .AsNoTracking()
            .Where(j => EF.Property<string>(j, "job_id") == jobId)
            .Select(
                j =>
                    new JobDto(
                        EF.Property<string>(j, "job_id"),
                        EF.Property<string>(j, "job_type_key"),
                        EF.Property<string>(j, "status_key"),
                        EF.Property<int>(j, "attempt_count"),
                        EF.Property<DateTimeOffset>(j, "created_at"),
                        EF.Property<DateTimeOffset>(j, "run_after"),
                        EF.Property<DateTimeOffset?>(j, "started_at"),
                        EF.Property<DateTimeOffset?>(j, "finished_at"),
                        EF.Property<string?>(j, "last_error"),
                        EF.Property<string?>(j, "entity_type_key"),
                        EF.Property<string?>(j, "entity_key"),
                        EF.Property<JsonElement>(j, "payload"),
                        EF.Property<DateTimeOffset?>(j, "locked_until")
                    )
            )
            .SingleOrDefaultAsync(cancellationToken);
    }

    private static string? GetErrorPreview(string? lastError)
    {
        if (string.IsNullOrWhiteSpace(lastError))
        {
            return null;
        }

        return lastError.Length <= 200 ? lastError : lastError[..200];
    }

    private sealed record JobListItemProjection(
        string JobId,
        string JobTypeKey,
        string StatusKey,
        int AttemptCount,
        DateTimeOffset CreatedAt,
        DateTimeOffset RunAfter,
        DateTimeOffset? LockedUntil,
        string? EntityTypeKey,
        string? EntityKey,
        string? LastError
    );
}


