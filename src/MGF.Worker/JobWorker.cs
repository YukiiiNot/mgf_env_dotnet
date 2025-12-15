namespace MGF.Worker;

using System.Data.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MGF.Infrastructure.Data;

public sealed class JobWorker : BackgroundService
{
    private static readonly TimeSpan DefaultPollInterval = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan ErrorBackoff = TimeSpan.FromSeconds(5);

    private readonly IServiceScopeFactory scopeFactory;
    private readonly ILogger<JobWorker> logger;
    private readonly string workerId;

    public JobWorker(IServiceScopeFactory scopeFactory, ILogger<JobWorker> logger)
    {
        this.scopeFactory = scopeFactory;
        this.logger = logger;
        workerId = $"mgf_worker_{Environment.MachineName}_{Guid.NewGuid():N}";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("MGF.Worker: starting (worker_id={WorkerId})", workerId);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var job = await TryClaimJobAsync(db, stoppingToken);
                if (job is null)
                {
                    await Task.Delay(DefaultPollInterval, stoppingToken);
                    continue;
                }

                await RunJobAsync(db, job, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "MGF.Worker: loop error");
                await Task.Delay(ErrorBackoff, stoppingToken);
            }
        }
    }

    private sealed record ClaimedJob(
        string JobId,
        string JobTypeKey,
        int AttemptCount,
        int MaxAttempts,
        string PayloadJson
    );

    private async Task<ClaimedJob?> TryClaimJobAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var cmd = db.Database.GetDbConnection().CreateCommand();
            cmd.CommandText =
                """
                WITH candidate AS (
                  SELECT job_id
                  FROM public.jobs
                  WHERE status_key = 'queued'
                    AND run_after <= now()
                    AND (locked_until IS NULL OR locked_until < now())
                  ORDER BY created_at
                  LIMIT 1
                  FOR UPDATE SKIP LOCKED
                ),
                locked AS (
                  UPDATE public.jobs j
                  SET status_key = 'running',
                      locked_by = @locked_by,
                      locked_until = now() + (@lock_seconds * interval '1 second'),
                      started_at = COALESCE(j.started_at, now()),
                      last_error = NULL
                  FROM candidate
                  WHERE j.job_id = candidate.job_id
                  RETURNING j.job_id, j.job_type_key, j.attempt_count, j.max_attempts, j.payload
                )
                SELECT job_id, job_type_key, attempt_count, max_attempts, payload::text AS payload_json
                FROM locked;
                """;

            AddParameter(cmd, "locked_by", workerId);
            AddParameter(cmd, "lock_seconds", (int)LockDuration.TotalSeconds);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            var jobId = reader.GetString(0);
            var jobTypeKey = reader.GetString(1);
            var attemptCount = reader.GetInt32(2);
            var maxAttempts = reader.GetInt32(3);
            var payloadJson = reader.GetString(4);

            logger.LogInformation(
                "MGF.Worker: claimed job {JobId} (type={JobTypeKey}, attempt={Attempt}/{Max})",
                jobId,
                jobTypeKey,
                attemptCount + 1,
                maxAttempts
            );

            return new ClaimedJob(jobId, jobTypeKey, attemptCount, maxAttempts, payloadJson);
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private async Task RunJobAsync(AppDbContext db, ClaimedJob job, CancellationToken cancellationToken)
    {
        try
        {
            if (string.Equals(job.JobTypeKey, "dropbox.create_project_structure", StringComparison.Ordinal))
            {
                await HandleDropboxCreateProjectStructureAsync(job, cancellationToken);
                await MarkSucceededAsync(db, job.JobId, cancellationToken);
                return;
            }

            throw new InvalidOperationException($"Unknown job_type_key: {job.JobTypeKey}");
        }
        catch (Exception ex)
        {
            await MarkFailedAsync(db, job, ex, cancellationToken);
        }
    }

    private Task HandleDropboxCreateProjectStructureAsync(ClaimedJob job, CancellationToken cancellationToken)
    {
        var payload = JsonDocument.Parse(job.PayloadJson);
        var root = payload.RootElement;

        var projectId = root.TryGetProperty("projectId", out var projectIdElement) ? projectIdElement.GetString() : null;
        var clientId = root.TryGetProperty("clientId", out var clientIdElement) ? clientIdElement.GetString() : null;
        var templateKey =
            root.TryGetProperty("templateKey", out var templateKeyElement) ? templateKeyElement.GetString() : null;

        logger.LogInformation(
            "MGF.Worker: dry run job {JobId}: would create Dropbox structure (projectId={ProjectId}, clientId={ClientId}, templateKey={TemplateKey})",
            job.JobId,
            projectId,
            clientId,
            templateKey
        );

        return Task.CompletedTask;
    }

    private async Task MarkSucceededAsync(AppDbContext db, string jobId, CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE public.jobs
            SET status_key = {"succeeded"},
                finished_at = now(),
                locked_by = NULL,
                locked_until = NULL,
                last_error = NULL
            WHERE job_id = {jobId};
            """,
            cancellationToken
        );

        logger.LogInformation("MGF.Worker: job {JobId} succeeded", jobId);
    }

    private async Task MarkFailedAsync(AppDbContext db, ClaimedJob job, Exception ex, CancellationToken cancellationToken)
    {
        var errorText = ex.ToString();
        var newAttemptCount = job.AttemptCount + 1;
        var shouldRetry = newAttemptCount < job.MaxAttempts;

        var delay = shouldRetry ? ComputeBackoffDelay(newAttemptCount) : TimeSpan.Zero;
        var statusKey = shouldRetry ? "queued" : "failed";
        var runAfter = shouldRetry ? DateTimeOffset.UtcNow.Add(delay) : DateTimeOffset.UtcNow;
        DateTimeOffset? finishedAt = shouldRetry ? null : DateTimeOffset.UtcNow;

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE public.jobs
            SET attempt_count = attempt_count + 1,
                status_key = {statusKey},
                run_after = {runAfter},
                finished_at = {finishedAt},
                locked_by = NULL,
                locked_until = NULL,
                last_error = {errorText},
                payload = jsonb_set(payload, ARRAY['lastError'], to_jsonb({errorText}::text), true)
            WHERE job_id = {job.JobId};
            """,
            cancellationToken
        );

        if (shouldRetry)
        {
            logger.LogWarning(
                ex,
                "MGF.Worker: job {JobId} failed (attempt {Attempt}/{Max}); requeued for {DelaySeconds:0.0}s",
                job.JobId,
                newAttemptCount,
                job.MaxAttempts,
                delay.TotalSeconds
            );
        }
        else
        {
            logger.LogError(
                ex,
                "MGF.Worker: job {JobId} failed permanently (attempt {Attempt}/{Max})",
                job.JobId,
                newAttemptCount,
                job.MaxAttempts
            );
        }
    }

    private static TimeSpan ComputeBackoffDelay(int attemptCount)
    {
        var seconds = Math.Pow(2, Math.Clamp(attemptCount, 1, 10));
        return TimeSpan.FromSeconds(Math.Min(5 * seconds, 15 * 60));
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
