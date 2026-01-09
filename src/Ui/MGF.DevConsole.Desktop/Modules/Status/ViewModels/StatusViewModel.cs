namespace MGF.DevConsole.Desktop.Modules.Status.ViewModels;

using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Configuration;
using MGF.DevConsole.Desktop.Api;

// Lifecycle: started by host when the main window opens; stopped on window close.
public sealed class StatusViewModel : ObservableObject
{
    private readonly IConfiguration config;
    private readonly MetaApiClient metaClient;
    private readonly TimeSpan pollInterval = TimeSpan.FromSeconds(5);
    private CancellationTokenSource? pollCts;
    private int stopRequested;
    private string environmentLine = "Loading environment...";
    private string connectionStatusLine = "Disconnected";
    private string? lastError;
    private string? lastKnownApiEnv;

    public StatusViewModel(IConfiguration config, MetaApiClient metaClient)
    {
        this.config = config;
        this.metaClient = metaClient;
    }

    public string EnvironmentLine
    {
        get => environmentLine;
        private set => SetProperty(ref environmentLine, value);
    }

    public string ConnectionStatusLine
    {
        get => connectionStatusLine;
        private set => SetProperty(ref connectionStatusLine, value);
    }

    public string? LastError
    {
        get => lastError;
        private set => SetProperty(ref lastError, value);
    }

    public void Start()
    {
        if (pollCts is not null || Volatile.Read(ref stopRequested) == 1)
        {
            return;
        }

        pollCts = new CancellationTokenSource();
        _ = RunAsync(pollCts.Token);
    }

    public void Stop()
    {
        Interlocked.Exchange(ref stopRequested, 1);
        var cts = Interlocked.Exchange(ref pollCts, null);
        if (cts is null)
        {
            return;
        }

        cts.Cancel();
        cts.Dispose();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await UpdateOnceAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                UpdateUi(() =>
                {
                    ConnectionStatusLine = "Disconnected";
                    LastError = $"Polling failed: {ex.Message}";
                });
            }

            try
            {
                await Task.Delay(pollInterval, cancellationToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    private async Task UpdateOnceAsync(CancellationToken cancellationToken)
    {
        var localEnv = Environment.GetEnvironmentVariable("MGF_ENV") ?? "(unset)";
        var baseUrl = config["Api:BaseUrl"] ?? "(missing)";

        try
        {
            var meta = await metaClient.GetMetaAsync(cancellationToken);
            lastKnownApiEnv = meta.MgfEnv;

            UpdateUi(() =>
            {
                ConnectionStatusLine = "Connected";
                LastError = null;
                EnvironmentLine = $"Local MGF_ENV={localEnv} | API MGF_ENV={meta.MgfEnv} | Api:BaseUrl={baseUrl}";
            });
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is MetaApiException or HttpRequestException or TaskCanceledException)
        {
            var apiEnv = string.IsNullOrWhiteSpace(lastKnownApiEnv) ? "unknown" : lastKnownApiEnv;
            var message = ex switch
            {
                MetaApiException metaEx => metaEx.Message,
                TaskCanceledException => "API request timed out.",
                HttpRequestException httpEx => httpEx.Message,
                _ => ex.Message
            };

            UpdateUi(() =>
            {
                ConnectionStatusLine = "Disconnected";
                LastError = message;
                EnvironmentLine = $"Local MGF_ENV={localEnv} | API MGF_ENV={apiEnv} | Api:BaseUrl={baseUrl}";
            });
        }
    }

    private void UpdateUi(Action update)
    {
        if (Volatile.Read(ref stopRequested) == 1)
        {
            return;
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            update();
            return;
        }

        if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
        {
            return;
        }

        _ = dispatcher.InvokeAsync(update);
    }
}
