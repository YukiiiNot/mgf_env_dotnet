namespace MGF.Worker;

internal static class JobReaperSql
{
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

    internal const string CountStaleRunningJobs =
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
        """;
}
