public sealed class DeliveryJobGuardTests
{
    [Fact]
    public void ShouldEnqueue_ReturnsFalseWhenExistingJobPresent()
    {
        var existing = new ExistingJob("job_123", "queued");
        var allowed = DeliveryJobGuard.ShouldEnqueue(existing, out var reason);

        Assert.False(allowed);
        Assert.NotNull(reason);
    }

    [Fact]
    public void ShouldEnqueue_ReturnsTrueWhenNoExistingJob()
    {
        var allowed = DeliveryJobGuard.ShouldEnqueue(null, out var reason);

        Assert.True(allowed);
        Assert.Null(reason);
    }
}
