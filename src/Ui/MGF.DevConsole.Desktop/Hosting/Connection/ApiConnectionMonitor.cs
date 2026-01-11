namespace MGF.DevConsole.Desktop.Hosting.Connection;

using System.Threading;
using System.Threading.Tasks;

public sealed class ApiConnectionMonitor : IApiConnectionMonitor
{
    private static readonly TimeSpan ConnectedInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan DisconnectedInterval = TimeSpan.FromSeconds(15);

    private readonly IApiConnectionProbe probe;
    private readonly ApiConnectionStateStore store;
    private readonly SemaphoreSlim signal = new(0);
    private CancellationTokenSource? loopCts;
    private int stopRequested;

    public ApiConnectionMonitor(IApiConnectionProbe probe, ApiConnectionStateStore store)
    {
        this.probe = probe;
        this.store = store;
    }

    public void Start()
    {
        if (loopCts is not null || Volatile.Read(ref stopRequested) == 1)
        {
            return;
        }

        loopCts = new CancellationTokenSource();
        _ = RunAsync(loopCts.Token);
    }

    public void Stop()
    {
        Interlocked.Exchange(ref stopRequested, 1);
        var cts = Interlocked.Exchange(ref loopCts, null);
        if (cts is null)
        {
            return;
        }

        cts.Cancel();
        cts.Dispose();
    }

    public void RequestProbe()
    {
        if (Volatile.Read(ref stopRequested) == 1)
        {
            return;
        }

        signal.Release();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            ApiConnectionState state;
            try
            {
                state = await probe.ProbeAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            store.UpdateState(state);

            var delay = state.Status == ApiConnectionStatus.Connected
                ? ConnectedInterval
                : DisconnectedInterval;

            try
            {
                var delayTask = Task.Delay(delay, cancellationToken);
                var signalTask = signal.WaitAsync(cancellationToken);
                await Task.WhenAny(delayTask, signalTask);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
