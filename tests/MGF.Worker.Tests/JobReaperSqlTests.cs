using MGF.Data.Stores.Jobs;
using Xunit;

namespace MGF.Worker.Tests;

public sealed class JobReaperSqlTests
{
    [Fact]
    public void ReaperSqlTargetsRunningWithExpiredLocks()
    {
        var sql = JobQueueSql.ReapStaleRunningJobs;

        Assert.Contains("status_key = 'running'", sql);
        Assert.Contains("locked_until IS NOT NULL", sql);
        Assert.Contains("locked_until < now()", sql);
        Assert.Contains("locked_until IS NULL", sql);
        Assert.Contains("started_at IS NOT NULL", sql);
        Assert.Contains("interval '60 minutes'", sql);
        Assert.Contains("status_key = 'queued'", sql);
        Assert.Contains("run_after = now()", sql);
        Assert.Contains("last_error", sql);
    }
}
