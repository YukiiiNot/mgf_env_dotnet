using System.IO;
using MGF.Storage.ProjectBootstrap;
using Xunit;

namespace MGF.Worker.Tests;

public sealed class ProjectBootstrapCleanupHelperTests
{
    [Fact]
    public async Task DeletesAfterTransientFailures()
    {
        var attempts = 0;
        void Delete(string _) 
        {
            attempts++;
            if (attempts < 3)
            {
                throw new IOException("locked");
            }
        }

        var delays = new List<TimeSpan>();
        Task Delay(TimeSpan span, CancellationToken _) 
        {
            delays.Add(span);
            return Task.CompletedTask;
        }

        var result = await ProjectBootstrapCleanupHelper.TryDeleteWithRetryAsync(
            targetPath: @"C:\temp\test",
            cancellationToken: CancellationToken.None,
            deleteAction: Delete,
            delay: Delay
        );

        Assert.True(result.Success, result.Error);
        Assert.Equal(3, attempts);
        Assert.Equal(2, delays.Count);
    }

    [Fact]
    public async Task FailsAfterRetryLimitForLockedTargets()
    {
        var attempts = 0;
        void Delete(string _) 
        {
            attempts++;
            throw new IOException("still locked");
        }

        Task Delay(TimeSpan _, CancellationToken __) => Task.CompletedTask;

        var result = await ProjectBootstrapCleanupHelper.TryDeleteWithRetryAsync(
            targetPath: @"C:\temp\test",
            cancellationToken: CancellationToken.None,
            deleteAction: Delete,
            delay: Delay
        );

        Assert.False(result.Success);
        Assert.Contains("locked", result.Error ?? string.Empty);
        Assert.Equal(ProjectBootstrapCleanupHelper.DefaultBackoff.Length, attempts);
    }

    [Fact]
    public async Task StopsImmediatelyForNonTransientErrors()
    {
        var attempts = 0;
        void Delete(string _) 
        {
            attempts++;
            throw new InvalidOperationException("boom");
        }

        Task Delay(TimeSpan _, CancellationToken __) => Task.CompletedTask;

        var result = await ProjectBootstrapCleanupHelper.TryDeleteWithRetryAsync(
            targetPath: @"C:\temp\test",
            cancellationToken: CancellationToken.None,
            deleteAction: Delete,
            delay: Delay
        );

        Assert.False(result.Success);
        Assert.Contains("boom", result.Error ?? string.Empty);
        Assert.Equal(1, attempts);
    }
}
