namespace MGF.Data.Stores.Jobs;

using System.Data.Common;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MGF.Data.Data;

public sealed class JobQueueStore : IJobQueueStore
{
    private readonly AppDbContext db;

    public JobQueueStore(AppDbContext db)
    {
        this.db = db;
    }

    public async Task<JobClaimRecord?> TryClaimJobAsync(
        string lockedBy,
        TimeSpan lockDuration,
        CancellationToken cancellationToken = default)
    {
        var command = JobQueueSql.BuildTryClaimCommand(lockedBy, (int)lockDuration.TotalSeconds);

        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var cmd = db.Database.GetDbConnection().CreateCommand();
            cmd.CommandText = command.CommandText;
            foreach (var parameter in command.Parameters)
            {
                AddParameter(cmd, parameter.Name, parameter.Value);
            }

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            return new JobClaimRecord(
                JobId: reader.GetString(0),
                JobTypeKey: reader.GetString(1),
                AttemptCount: reader.GetInt32(2),
                MaxAttempts: reader.GetInt32(3),
                PayloadJson: reader.GetString(4));
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    public async Task<int> ReapStaleRunningJobsAsync(CancellationToken cancellationToken = default)
    {
        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var cmd = db.Database.GetDbConnection().CreateCommand();
            cmd.CommandText = JobQueueSql.ReapStaleRunningJobs;

            var result = await cmd.ExecuteScalarAsync(cancellationToken);
            if (result is null || result is DBNull)
            {
                return 0;
            }

            return Convert.ToInt32(result, CultureInfo.InvariantCulture);
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    public Task UpdateJobPayloadAsync(
        string jobId,
        string payloadJson,
        CancellationToken cancellationToken = default)
    {
        return db.Database.ExecuteSqlInterpolatedAsync(
            JobQueueSql.BuildUpdatePayloadCommand(jobId, payloadJson),
            cancellationToken);
    }

    public Task MarkSucceededAsync(string jobId, CancellationToken cancellationToken = default)
    {
        return db.Database.ExecuteSqlInterpolatedAsync(
            JobQueueSql.BuildMarkSucceededCommand(jobId),
            cancellationToken);
    }

    public Task MarkFailedAsync(JobFailureUpdate update, CancellationToken cancellationToken = default)
    {
        return db.Database.ExecuteSqlInterpolatedAsync(
            JobQueueSql.BuildMarkFailedCommand(update),
            cancellationToken);
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}

internal static class JobQueueSql
{
    internal static JobQueueSqlCommand BuildTryClaimCommand(string lockedBy, int lockSeconds)
    {
        return new JobQueueSqlCommand(
            CommandText:
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
                """,
            Parameters: new[]
            {
                new JobQueueSqlParameter("locked_by", lockedBy),
                new JobQueueSqlParameter("lock_seconds", lockSeconds)
            });
    }

    internal const string ReapStaleRunningJobs =
        """
        WITH reset AS (
          UPDATE public.jobs
          SET status_key = 'queued',
              run_after = now(),
              locked_by = NULL,
              locked_until = NULL,
              last_error = CASE
                WHEN locked_until IS NULL
                  THEN 'reaped stale running job (no lock, started_at stale)'
                ELSE 'reaped stale running job (expired lock)'
              END
          WHERE status_key = 'running'
            AND (
              (locked_until IS NOT NULL AND locked_until < now())
              OR (
                locked_until IS NULL
                AND started_at IS NOT NULL
                AND started_at < now() - interval '60 minutes'
              )
            )
          RETURNING 1
        )
        SELECT count(*) FROM reset;
        """;

    internal static FormattableString BuildUpdatePayloadCommand(string jobId, string payloadJson)
    {
        return $"""
        UPDATE public.jobs
        SET payload = {payloadJson}::jsonb
        WHERE job_id = {jobId};
        """;
    }

    internal static FormattableString BuildMarkSucceededCommand(string jobId)
    {
        return $"""
        UPDATE public.jobs
        SET status_key = {"succeeded"},
            finished_at = now(),
            locked_by = NULL,
            locked_until = NULL,
            last_error = NULL
        WHERE job_id = {jobId};
        """;
    }

    internal static FormattableString BuildMarkFailedCommand(JobFailureUpdate update)
    {
        return $"""
        UPDATE public.jobs
        SET attempt_count = attempt_count + 1,
            status_key = {update.StatusKey},
            run_after = {update.RunAfter},
            finished_at = {update.FinishedAt},
            locked_by = NULL,
            locked_until = NULL,
            last_error = {update.ErrorText},
            payload = jsonb_set(payload, ARRAY['lastError'], to_jsonb({update.ErrorText}::text), true)
        WHERE job_id = {update.JobId};
        """;
    }
}

internal sealed record JobQueueSqlCommand(string CommandText, IReadOnlyList<JobQueueSqlParameter> Parameters);

internal sealed record JobQueueSqlParameter(string Name, object Value);
