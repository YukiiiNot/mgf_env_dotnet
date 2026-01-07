namespace MGF.Worker.Tests;

using MGF.Data.Stores.Jobs;

public sealed class JobQueueSqlTests
{
    [Fact]
    public void BuildTryClaimCommand_UsesExpectedParameters()
    {
        var command = JobQueueSql.BuildTryClaimCommand("worker_123", 120);

        Assert.Contains("WITH candidate AS", command.CommandText);
        Assert.Contains("locked_by = @locked_by", command.CommandText);
        Assert.Contains("locked_until = now() + (@lock_seconds * interval '1 second')", command.CommandText);

        Assert.Collection(
            command.Parameters,
            parameter =>
            {
                Assert.Equal("locked_by", parameter.Name);
                Assert.Equal("worker_123", parameter.Value);
            },
            parameter =>
            {
                Assert.Equal("lock_seconds", parameter.Name);
                Assert.Equal(120, parameter.Value);
            });
    }

    [Fact]
    public void BuildMarkFailedCommand_UsesExpectedArguments()
    {
        var runAfter = new DateTimeOffset(2024, 4, 12, 10, 0, 0, TimeSpan.Zero);
        var finishedAt = new DateTimeOffset(2024, 4, 12, 10, 1, 0, TimeSpan.Zero);
        var update = new JobFailureUpdate("job_123", "failed", runAfter, finishedAt, "error text");

        var command = JobQueueSql.BuildMarkFailedCommand(update);

        Assert.Contains("UPDATE public.jobs", command.Format);
        Assert.Contains("payload = jsonb_set", command.Format);

        var args = command.GetArguments();
        Assert.Equal("failed", args[0]);
        Assert.Equal(runAfter, args[1]);
        Assert.Equal(finishedAt, args[2]);
        Assert.Equal("error text", args[3]);
        Assert.Equal("error text", args[4]);
        Assert.Equal("job_123", args[5]);
    }

    [Fact]
    public void BuildDeferCommand_DoesNotIncrementAttempts()
    {
        var runAfter = new DateTimeOffset(2024, 5, 1, 8, 30, 0, TimeSpan.Zero);
        var command = JobQueueSql.BuildDeferCommand("job_456", runAfter, "lock busy");

        Assert.Contains("status_key =", command.Format);
        Assert.DoesNotContain("attempt_count = attempt_count + 1", command.Format);

        var args = command.GetArguments();
        Assert.Equal("queued", args[0]);
        Assert.Equal(runAfter, args[1]);
        Assert.Equal("lock busy", args[2]);
        Assert.Equal("lock busy", args[3]);
        Assert.Equal("job_456", args[4]);
    }
}
