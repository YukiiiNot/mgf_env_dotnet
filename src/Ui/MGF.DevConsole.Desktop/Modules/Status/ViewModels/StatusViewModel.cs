namespace MGF.DevConsole.Desktop.Modules.Status.ViewModels;

using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Configuration;
using MGF.DevConsole.Desktop.Api;

public sealed class StatusViewModel : ObservableObject
{
    private readonly IConfiguration config;
    private readonly MetaApiClient metaClient;
    private readonly TimeSpan pollInterval = TimeSpan.FromSeconds(5);
    private SynchronizationContext? uiContext;
    private CancellationTokenSource? pollCts;
    private bool started;
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
        if (started)
        {
            return;
        }

        started = true;
        uiContext = SynchronizationContext.Current;
        pollCts = new CancellationTokenSource();
        _ = RunAsync(pollCts.Token);
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await UpdateOnceAsync(cancellationToken);
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
        if (uiContext is null)
        {
            update();
            return;
        }

        uiContext.Post(_ => update(), null);
    }
}
