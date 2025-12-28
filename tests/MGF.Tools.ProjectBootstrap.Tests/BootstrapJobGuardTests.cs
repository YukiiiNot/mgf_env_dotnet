public sealed class BootstrapJobGuardTests
{
    [Fact]
    public void ShouldEnqueue_ReturnsFalseWhenExistingJobPresent()
    {
        var existing = new ExistingJob("job_123", "queued");
        var allowed = BootstrapJobGuard.ShouldEnqueue(existing, out var reason);

        Assert.False(allowed);
        Assert.NotNull(reason);
    }

    [Fact]
    public void ShouldEnqueue_ReturnsTrueWhenNoExistingJob()
    {
        var allowed = BootstrapJobGuard.ShouldEnqueue(null, out var reason);

        Assert.True(allowed);
        Assert.Null(reason);
    }
}
