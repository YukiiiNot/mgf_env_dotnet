namespace MGF.DevConsole.Desktop.Modules.Status.ViewModels;

using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;
using MGF.DevConsole.Desktop.Hosting.Connection;

// Lifecycle: started by host when the main window opens; stopped on window close.
public sealed class StatusViewModel : ObservableObject
{
    private readonly IConfiguration config;
    private readonly IApiConnectionStateStore stateStore;
    private readonly IApiConnectionMonitor connectionMonitor;
    private int stopRequested;
    private string environmentLine = "Loading environment...";
    private string connectionStatusLine = "Checking API...";
    private string? lastError;

    public StatusViewModel(
        IConfiguration config,
        IApiConnectionStateStore stateStore,
        IApiConnectionMonitor connectionMonitor)
    {
        this.config = config;
        this.stateStore = stateStore;
        this.connectionMonitor = connectionMonitor;
        RetryCommand = new RelayCommand(() => connectionMonitor.RequestProbe());
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

    public ICommand RetryCommand { get; }

    public void Start()
    {
        if (Volatile.Read(ref stopRequested) == 1)
        {
            return;
        }

        stateStore.StateChanged += OnStateChanged;
        UpdateUi(() => UpdateFromState(stateStore.CurrentState));
    }

    public void Stop()
    {
        Interlocked.Exchange(ref stopRequested, 1);
        stateStore.StateChanged -= OnStateChanged;
    }

    private void OnStateChanged(object? sender, ApiConnectionState state)
    {
        UpdateUi(() => UpdateFromState(state));
    }

    private void UpdateFromState(ApiConnectionState state)
    {
        var localEnv = Environment.GetEnvironmentVariable("MGF_ENV") ?? "(unset)";
        var baseUrl = config["Api:BaseUrl"] ?? "(missing)";
        var apiEnv = string.IsNullOrWhiteSpace(state.ApiEnvironment) ? "unknown" : state.ApiEnvironment;

        EnvironmentLine = $"Local MGF_ENV={localEnv} | API MGF_ENV={apiEnv} | Api:BaseUrl={baseUrl}";
        ConnectionStatusLine = FormatStatus(state.Status);
        LastError = state.Status == ApiConnectionStatus.Connected ? null : state.Message;
    }

    private static string FormatStatus(ApiConnectionStatus status)
    {
        return status switch
        {
            ApiConnectionStatus.Connected => "Connected",
            ApiConnectionStatus.Unauthorized => "Unauthorized",
            ApiConnectionStatus.Misconfigured => "Misconfigured",
            ApiConnectionStatus.Offline => "Offline",
            ApiConnectionStatus.Degraded => "Degraded",
            _ => "Unknown"
        };
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
