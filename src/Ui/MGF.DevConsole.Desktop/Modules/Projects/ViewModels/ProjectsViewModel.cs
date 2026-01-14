namespace MGF.DevConsole.Desktop.Modules.Projects.ViewModels;

using System.Collections.ObjectModel;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using MGF.DevConsole.Desktop.Api;
using MGF.DevConsole.Desktop.Hosting.Connection;

// Lifecycle: started by host when the main window opens; stopped on window close.
public sealed class ProjectsViewModel : ObservableObject
{
    private readonly IProjectsApiClient projectsApi;
    private readonly IApiConnectionStateStore connectionStore;
    private readonly TimeSpan pollInterval = TimeSpan.FromSeconds(5);
    private CancellationTokenSource? pollCts;
    private CancellationTokenSource? detailCts;
    private bool started;
    private int stopRequested;
    private ProjectListItem? selectedProject;
    private ApiConnectionState currentConnectionState = ApiConnectionState.Initial();
    private string connectionStatusLine = "Disconnected";
    private string? lastError;
    private string? detailError;
    private string? detailProjectId;
    private string? detailProjectCode;
    private string? detailName;
    private string? detailClientId;
    private string? detailStatusKey;
    private string? detailPhaseKey;
    private string? detailPriorityKey;
    private string? detailDueDate;
    private string? detailArchivedAt;
    private string? detailDataProfile;
    private string? detailCurrentInvoiceId;
    private string? detailCreatedAt;
    private string? detailUpdatedAt;

    public ProjectsViewModel(IProjectsApiClient projectsApi, IApiConnectionStateStore connectionStore)
    {
        this.projectsApi = projectsApi;
        this.connectionStore = connectionStore;
        Projects = new ObservableCollection<ProjectListItem>();
    }

    public ObservableCollection<ProjectListItem> Projects { get; }

    public ProjectListItem? SelectedProject
    {
        get => selectedProject;
        set
        {
            if (selectedProject?.ProjectId == value?.ProjectId)
            {
                selectedProject = value;
                OnPropertyChanged();
                return;
            }

            selectedProject = value;
            OnPropertyChanged();

            DetailError = null;
            ClearDetailFields();

            if (selectedProject is null)
            {
                return;
            }

            if (currentConnectionState.Status != ApiConnectionStatus.Connected)
            {
                DetailError = currentConnectionState.Message;
                return;
            }

            _ = FetchDetailAsync(selectedProject.ProjectId);
        }
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

    public string? DetailError
    {
        get => detailError;
        private set => SetProperty(ref detailError, value);
    }

    public string? DetailProjectId
    {
        get => detailProjectId;
        private set => SetProperty(ref detailProjectId, value);
    }

    public string? DetailProjectCode
    {
        get => detailProjectCode;
        private set => SetProperty(ref detailProjectCode, value);
    }

    public string? DetailName
    {
        get => detailName;
        private set => SetProperty(ref detailName, value);
    }

    public string? DetailClientId
    {
        get => detailClientId;
        private set => SetProperty(ref detailClientId, value);
    }

    public string? DetailStatusKey
    {
        get => detailStatusKey;
        private set => SetProperty(ref detailStatusKey, value);
    }

    public string? DetailPhaseKey
    {
        get => detailPhaseKey;
        private set => SetProperty(ref detailPhaseKey, value);
    }

    public string? DetailPriorityKey
    {
        get => detailPriorityKey;
        private set => SetProperty(ref detailPriorityKey, value);
    }

    public string? DetailDueDate
    {
        get => detailDueDate;
        private set => SetProperty(ref detailDueDate, value);
    }

    public string? DetailArchivedAt
    {
        get => detailArchivedAt;
        private set => SetProperty(ref detailArchivedAt, value);
    }

    public string? DetailDataProfile
    {
        get => detailDataProfile;
        private set => SetProperty(ref detailDataProfile, value);
    }

    public string? DetailCurrentInvoiceId
    {
        get => detailCurrentInvoiceId;
        private set => SetProperty(ref detailCurrentInvoiceId, value);
    }

    public string? DetailCreatedAt
    {
        get => detailCreatedAt;
        private set => SetProperty(ref detailCreatedAt, value);
    }

    public string? DetailUpdatedAt
    {
        get => detailUpdatedAt;
        private set => SetProperty(ref detailUpdatedAt, value);
    }

    public void Start()
    {
        if (started)
        {
            return;
        }

        if (Volatile.Read(ref stopRequested) == 1)
        {
            return;
        }

        started = true;
        connectionStore.StateChanged += OnConnectionStateChanged;
        UpdateUi(() => HandleConnectionState(connectionStore.CurrentState));
    }

    public void Stop()
    {
        if (!started)
        {
            return;
        }

        started = false;
        Interlocked.Exchange(ref stopRequested, 1);
        connectionStore.StateChanged -= OnConnectionStateChanged;
        StopPolling();
        StopDetail();
    }

    private void OnConnectionStateChanged(object? sender, ApiConnectionState state)
    {
        UpdateUi(() => HandleConnectionState(state));
    }

    private void HandleConnectionState(ApiConnectionState state)
    {
        currentConnectionState = state;
        ConnectionStatusLine = FormatStatus(state.Status);

        if (state.Status == ApiConnectionStatus.Connected)
        {
            LastError = null;
            DetailError = null;
            StartPolling();
            return;
        }

        LastError = state.Message;
        DetailError = state.Message;
        StopPolling();
        StopDetail();
        ClearDetailFields();
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

    private void StartPolling()
    {
        if (pollCts is not null || Volatile.Read(ref stopRequested) == 1)
        {
            return;
        }

        pollCts = new CancellationTokenSource();
        _ = RunAsync(pollCts.Token);
    }

    private void StopPolling()
    {
        var poll = Interlocked.Exchange(ref pollCts, null);
        if (poll is null)
        {
            return;
        }

        poll.Cancel();
        poll.Dispose();
    }

    private void StopDetail()
    {
        var detail = Interlocked.Exchange(ref detailCts, null);
        if (detail is null)
        {
            return;
        }

        detail.Cancel();
        detail.Dispose();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await PollOnceAsync(cancellationToken);
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

    private async Task PollOnceAsync(CancellationToken cancellationToken)
    {
        ProjectsApiClient.ProjectsListResponseDto response;
        try
        {
            response = await projectsApi.GetProjectsAsync(200, null, null, null, cancellationToken);
        }
        catch (ProjectsApiException ex)
        {
            UpdateUi(() =>
            {
                LastError = ex.Failure == ProjectsApiFailure.Unauthorized
                    ? "Unauthorized (X-MGF-API-KEY rejected)."
                    : ex.Message;
            });
            return;
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            UpdateUi(() =>
            {
                LastError = ex is TaskCanceledException
                    ? "API request timed out."
                    : ex.Message;
            });
            return;
        }

        var items = response.Items
            .Select(item => new ProjectListItem(
                item.ProjectId,
                item.ProjectCode,
                item.Name,
                item.ClientId,
                item.StatusKey,
                item.PhaseKey,
                item.PriorityKey,
                item.DueDate,
                item.ArchivedAt,
                item.CreatedAt))
            .ToList();

        UpdateUi(() =>
        {
            var selectedId = SelectedProject?.ProjectId;

            Projects.Clear();
            foreach (var item in items)
            {
                Projects.Add(item);
            }

            LastError = null;

            if (selectedId is null)
            {
                SelectedProject = null;
                return;
            }

            var stillSelected = Projects.FirstOrDefault(project => project.ProjectId == selectedId);
            SelectedProject = stillSelected;
            if (stillSelected is null)
            {
                DetailError = null;
                ClearDetailFields();
            }
        });
    }

    private async Task FetchDetailAsync(string projectId)
    {
        if (Volatile.Read(ref stopRequested) == 1)
        {
            return;
        }

        var cts = new CancellationTokenSource();
        var previous = Interlocked.Exchange(ref detailCts, cts);
        if (previous is not null)
        {
            previous.Cancel();
            previous.Dispose();
        }

        ProjectsApiClient.ProjectDetailDto detail;
        try
        {
            detail = await projectsApi.GetProjectAsync(projectId, cts.Token);
        }
        catch (ProjectsApiException ex)
        {
            UpdateUi(() =>
            {
                DetailError = ex.Failure == ProjectsApiFailure.Unauthorized
                    ? "Unauthorized (X-MGF-API-KEY rejected)."
                    : ex.Message;
                ClearDetailFields();
            });
            return;
        }
        catch (TaskCanceledException) when (cts.Token.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            UpdateUi(() =>
            {
                DetailError = ex is TaskCanceledException
                    ? "API request timed out."
                    : ex.Message;
                ClearDetailFields();
            });
            return;
        }

        UpdateUi(() =>
        {
            if (SelectedProject?.ProjectId != projectId)
            {
                return;
            }

            DetailError = null;
            DetailProjectId = detail.ProjectId;
            DetailProjectCode = detail.ProjectCode;
            DetailName = detail.Name;
            DetailClientId = detail.ClientId;
            DetailStatusKey = detail.StatusKey;
            DetailPhaseKey = detail.PhaseKey;
            DetailPriorityKey = detail.PriorityKey ?? string.Empty;
            DetailDueDate = FormatDateOnly(detail.DueDate);
            DetailArchivedAt = FormatOffset(detail.ArchivedAt);
            DetailDataProfile = detail.DataProfile;
            DetailCurrentInvoiceId = detail.CurrentInvoiceId ?? string.Empty;
            DetailCreatedAt = detail.CreatedAt.ToString("O");
            DetailUpdatedAt = FormatOffset(detail.UpdatedAt);
        });
    }

    private static string? FormatDateOnly(DateOnly? value)
    {
        return value?.ToString("yyyy-MM-dd");
    }

    private static string? FormatOffset(DateTimeOffset? value)
    {
        return value?.ToString("O") ?? string.Empty;
    }

    private void ClearDetailFields()
    {
        DetailProjectId = null;
        DetailProjectCode = null;
        DetailName = null;
        DetailClientId = null;
        DetailStatusKey = null;
        DetailPhaseKey = null;
        DetailPriorityKey = null;
        DetailDueDate = null;
        DetailArchivedAt = null;
        DetailDataProfile = null;
        DetailCurrentInvoiceId = null;
        DetailCreatedAt = null;
        DetailUpdatedAt = null;
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

    public sealed record ProjectListItem(
        string ProjectId,
        string ProjectCode,
        string Name,
        string ClientId,
        string StatusKey,
        string PhaseKey,
        string? PriorityKey,
        DateOnly? DueDate,
        DateTimeOffset? ArchivedAt,
        DateTimeOffset CreatedAt
    );
}
