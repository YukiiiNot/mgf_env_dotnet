using System.Threading.Tasks;
using MGF.DevConsole.Desktop.Api;
using MGF.DevConsole.Desktop.Hosting.Connection;
using MGF.DevConsole.Desktop.Modules.Jobs.ViewModels;

public sealed class JobsViewModelTests
{
    [Fact]
    public async Task Start_DoesNotPoll_WhenDisconnected()
    {
        var store = new ApiConnectionStateStore();
        store.UpdateState(new ApiConnectionState(
            ApiConnectionStatus.Offline,
            "API not reachable.",
            DateTimeOffset.UtcNow,
            null,
            null));

        var client = new FakeJobsApiClient();
        var viewModel = new JobsViewModel(client, store);

        viewModel.Start();
        await Task.Delay(150);

        Assert.Equal(0, client.ListCalls);
        viewModel.Stop();
    }

    [Fact]
    public async Task Polling_Starts_WhenStateBecomesConnected()
    {
        var store = new ApiConnectionStateStore();
        store.UpdateState(new ApiConnectionState(
            ApiConnectionStatus.Offline,
            "API not reachable.",
            DateTimeOffset.UtcNow,
            null,
            null));

        var client = new FakeJobsApiClient();
        var viewModel = new JobsViewModel(client, store);

        viewModel.Start();
        store.UpdateState(new ApiConnectionState(
            ApiConnectionStatus.Connected,
            "Connected",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            "Dev"));

        await client.ListCalled.Task.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.Equal(1, client.ListCalls);
        viewModel.Stop();
    }

    private sealed class FakeJobsApiClient : IJobsApiClient
    {
        private int listCalls;

        public int ListCalls => listCalls;

        public TaskCompletionSource<bool> ListCalled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<JobsApiClient.JobsListResponseDto> GetJobsAsync(
            DateTimeOffset sinceUtc,
            int limit,
            JobsApiClient.JobsListCursorDto? cursor,
            string? statusKey,
            string? jobTypeKey,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref listCalls);
            ListCalled.TrySetResult(true);
            return Task.FromResult(new JobsApiClient.JobsListResponseDto(Array.Empty<JobsApiClient.JobListItemDto>(), null));
        }

        public Task<JobsApiClient.JobDetailDto> GetJobAsync(string jobId, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Detail calls are not expected in this test.");
        }
    }
}
