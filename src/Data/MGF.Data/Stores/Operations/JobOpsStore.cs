namespace MGF.Data.Stores.Operations;

using System.Globalization;
using Microsoft.Extensions.Configuration;
using Npgsql;
using MGF.Contracts.Abstractions.Operations.Jobs;
using MGF.Data.Configuration;

public sealed class JobOpsStore : IJobOpsStore
{
    private readonly string connectionString;

    public JobOpsStore(IConfiguration configuration)
    {
        connectionString = DatabaseConnection.ResolveConnectionString(configuration);
    }

    public async Task EnsureJobTypeAsync(
        string jobTypeKey,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new NpgsqlCommand(
            """
            INSERT INTO public.job_types (job_type_key, display_name)
            VALUES (@job_type_key, @display_name)
            ON CONFLICT (job_type_key) DO UPDATE
            SET display_name = EXCLUDED.display_name;
            """,
            conn
        );

        cmd.Parameters.AddWithValue("job_type_key", jobTypeKey);
        cmd.Parameters.AddWithValue("display_name", displayName);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<ExistingJob?> FindExistingJobAsync(
        string jobTypeKey,
        string entityTypeKey,
        string entityKey,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new NpgsqlCommand(
            """
            SELECT job_id, status_key
            FROM public.jobs
            WHERE job_type_key = @job_type_key
              AND entity_type_key = @entity_type_key
              AND entity_key = @entity_key
              AND status_key IN ('queued', 'running')
            ORDER BY created_at DESC
            LIMIT 1;
            """,
            conn
        );

        cmd.Parameters.AddWithValue("job_type_key", jobTypeKey);
        cmd.Parameters.AddWithValue("entity_type_key", entityTypeKey);
        cmd.Parameters.AddWithValue("entity_key", entityKey);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new ExistingJob(reader.GetString(0), reader.GetString(1));
    }

    public async Task EnqueueJobAsync(
        JobEnqueueRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new NpgsqlCommand(
            """
            INSERT INTO public.jobs (job_id, job_type_key, payload, status_key, run_after, entity_type_key, entity_key)
            VALUES (@job_id, @job_type_key, @payload::jsonb, 'queued', now(), @entity_type_key, @entity_key);
            """,
            conn
        );

        cmd.Parameters.AddWithValue("job_id", request.JobId);
        cmd.Parameters.AddWithValue("job_type_key", request.JobTypeKey);
        cmd.Parameters.AddWithValue("payload", request.PayloadJson);
        cmd.Parameters.AddWithValue("entity_type_key", request.EntityTypeKey);
        cmd.Parameters.AddWithValue("entity_key", request.EntityKey);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task EnqueueRetryableJobAsync(
        RetryableJobEnqueueRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new NpgsqlCommand(
            """
            INSERT INTO public.jobs (
                job_id,
                job_type_key,
                status_key,
                attempt_count,
                max_attempts,
                run_after,
                payload,
                entity_type_key,
                entity_key
            )
            VALUES (
                @job_id,
                @job_type_key,
                'queued',
                @attempt_count,
                @max_attempts,
                now(),
                @payload::jsonb,
                @entity_type_key,
                @entity_key
            );
            """,
            conn
        );

        cmd.Parameters.AddWithValue("job_id", request.JobId);
        cmd.Parameters.AddWithValue("job_type_key", request.JobTypeKey);
        cmd.Parameters.AddWithValue("attempt_count", request.AttemptCount);
        cmd.Parameters.AddWithValue("max_attempts", request.MaxAttempts);
        cmd.Parameters.AddWithValue("payload", request.PayloadJson);
        cmd.Parameters.AddWithValue("entity_type_key", request.EntityTypeKey);
        cmd.Parameters.AddWithValue("entity_key", request.EntityKey);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<JobSummary>> GetJobsAsync(
        string jobTypeKey,
        string entityTypeKey,
        string entityKey,
        CancellationToken cancellationToken = default)
    {
        var jobs = new List<JobSummary>();

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new NpgsqlCommand(
            """
            SELECT job_id, status_key, attempt_count, run_after, locked_until
            FROM public.jobs
            WHERE job_type_key = @job_type_key
              AND entity_type_key = @entity_type_key
              AND entity_key = @entity_key
            ORDER BY created_at DESC;
            """,
            conn
        );

        cmd.Parameters.AddWithValue("job_type_key", jobTypeKey);
        cmd.Parameters.AddWithValue("entity_type_key", entityTypeKey);
        cmd.Parameters.AddWithValue("entity_key", entityKey);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var jobId = reader.GetString(0);
            var status = reader.GetString(1);
            var attempt = reader.GetInt32(2);
            var runAfter = reader.GetFieldValue<DateTimeOffset>(3);
            var lockedUntil = reader.IsDBNull(4) ? (DateTimeOffset?)null : reader.GetFieldValue<DateTimeOffset>(4);
            jobs.Add(new JobSummary(jobId, status, attempt, runAfter, lockedUntil));
        }

        return jobs;
    }

    public async Task<IReadOnlyList<RootIntegrityJobSummary>> GetRootIntegrityJobsAsync(
        string entityKey,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var jobs = new List<RootIntegrityJobSummary>();

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new NpgsqlCommand(
            """
            SELECT job_id, status_key, attempt_count, run_after, locked_until, payload::text
            FROM public.jobs
            WHERE job_type_key = 'domain.root_integrity'
              AND entity_type_key = 'storage_root'
              AND entity_key = @entity_key
            ORDER BY created_at DESC
            LIMIT @limit;
            """,
            conn
        );

        cmd.Parameters.AddWithValue("entity_key", entityKey);
        cmd.Parameters.AddWithValue("limit", limit);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var jobId = reader.GetString(0);
            var status = reader.GetString(1);
            var attempt = reader.GetInt32(2);
            var runAfter = reader.GetFieldValue<DateTimeOffset>(3);
            var lockedUntil = reader.IsDBNull(4) ? (DateTimeOffset?)null : reader.GetFieldValue<DateTimeOffset>(4);
            var payloadJson = reader.GetString(5);
            jobs.Add(new RootIntegrityJobSummary(jobId, status, attempt, runAfter, lockedUntil, payloadJson));
        }

        return jobs;
    }

    public async Task<int> ResetJobsAsync(
        string projectId,
        string jobTypeKey,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new NpgsqlCommand(
            """
            UPDATE public.jobs
            SET status_key = 'queued',
                run_after = now(),
                locked_by = NULL,
                locked_until = NULL
            WHERE job_type_key = @job_type_key
              AND entity_type_key = 'project'
              AND entity_key = @project_id
              AND status_key IN ('queued','running');
            """,
            conn
        );

        cmd.Parameters.AddWithValue("project_id", projectId);
        cmd.Parameters.AddWithValue("job_type_key", jobTypeKey);

        return await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<int> CountStaleRunningJobsAsync(CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);

        await using var countCmd = new NpgsqlCommand(
            """
            SELECT count(*)
            FROM public.jobs
            WHERE status_key = 'running'
              AND (
                (locked_until IS NOT NULL AND locked_until < now())
                OR (
                  locked_until IS NULL
                  AND started_at IS NOT NULL
                  AND started_at < now() - interval '60 minutes'
                )
              );
            """,
            conn
        );

        var result = await countCmd.ExecuteScalarAsync(cancellationToken);
        return result is null || result is DBNull ? 0 : Convert.ToInt32(result, CultureInfo.InvariantCulture);
    }

    public async Task<int> RequeueStaleRunningJobsAsync(CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new NpgsqlCommand(
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
            """,
            conn
        );

        var updated = await cmd.ExecuteScalarAsync(cancellationToken);
        return updated is null || updated is DBNull ? 0 : Convert.ToInt32(updated, CultureInfo.InvariantCulture);
    }
}
