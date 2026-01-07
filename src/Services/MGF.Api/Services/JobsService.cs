namespace MGF.Api.Services;

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MGF.Data.Data;

public sealed class JobsService
{
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
        JsonElement Payload,
        DateTimeOffset? LockedUntil
    );

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
                        EF.Property<JsonElement>(j, "payload"),
                        EF.Property<DateTimeOffset?>(j, "locked_until")
                    )
            )
            .SingleOrDefaultAsync(cancellationToken);
    }
}


