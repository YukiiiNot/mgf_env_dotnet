namespace MGF.DevConsole.Desktop.Modules.Jobs.ViewModels;

using System.Collections.ObjectModel;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using MGF.DevConsole.Desktop.Api;

// Lifecycle: started by host when the main window opens; stopped on window close.
public sealed class JobsViewModel : ObservableObject
{
    private const int MaxPayloadChars = 50 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly JobsApiClient jobsApi;
    private readonly TimeSpan pollInterval = TimeSpan.FromSeconds(3);
    private CancellationTokenSource? pollCts;
    private CancellationTokenSource? detailCts;
    private int stopRequested;
    private JobListItem? selectedJob;
    private bool showPayload;
    private JsonElement? payloadElement;
    private string connectionStatusLine = "Disconnected";
    private string? lastError;
    private string? detailError;
    private string? detailJobId;
    private string? detailStatusKey;
    private string? detailJobTypeKey;
    private string? detailAttemptCount;
    private string? detailCreatedAt;
    private string? detailRunAfter;
    private string? detailStartedAt;
    private string? detailFinishedAt;
    private string? detailLockedUntil;
    private string? detailLastError;
    private string? detailEntityTypeKey;
    private string? detailEntityKey;
    private string? jobDetailJson;

    public JobsViewModel(JobsApiClient jobsApi)
    {
        this.jobsApi = jobsApi;
        Jobs = new ObservableCollection<JobListItem>();
    }

    public ObservableCollection<JobListItem> Jobs { get; }

    public JobListItem? SelectedJob
    {
        get => selectedJob;
        set
        {
            if (selectedJob?.JobId == value?.JobId)
            {
                selectedJob = value;
                OnPropertyChanged();
                return;
            }

            selectedJob = value;
            OnPropertyChanged();

            ShowPayload = false;
            DetailError = null;
            ClearDetailFields();

            if (selectedJob is null)
            {
                return;
            }

            _ = FetchDetailAsync(selectedJob.JobId);
        }
    }

    public bool ShowPayload
    {
        get => showPayload;
        set
        {
            if (!SetProperty(ref showPayload, value))
            {
                return;
            }

            UpdatePayloadDisplay();
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

    public string? DetailJobId
    {
        get => detailJobId;
        private set => SetProperty(ref detailJobId, value);
    }

    public string? DetailStatusKey
    {
        get => detailStatusKey;
        private set => SetProperty(ref detailStatusKey, value);
    }

    public string? DetailJobTypeKey
    {
        get => detailJobTypeKey;
        private set => SetProperty(ref detailJobTypeKey, value);
    }

    public string? DetailAttemptCount
    {
        get => detailAttemptCount;
        private set => SetProperty(ref detailAttemptCount, value);
    }

    public string? DetailCreatedAt
    {
        get => detailCreatedAt;
        private set => SetProperty(ref detailCreatedAt, value);
    }

    public string? DetailRunAfter
    {
        get => detailRunAfter;
        private set => SetProperty(ref detailRunAfter, value);
    }

    public string? DetailStartedAt
    {
        get => detailStartedAt;
        private set => SetProperty(ref detailStartedAt, value);
    }

    public string? DetailFinishedAt
    {
        get => detailFinishedAt;
        private set => SetProperty(ref detailFinishedAt, value);
    }

    public string? DetailLockedUntil
    {
        get => detailLockedUntil;
        private set => SetProperty(ref detailLockedUntil, value);
    }

    public string? DetailLastError
    {
        get => detailLastError;
        private set => SetProperty(ref detailLastError, value);
    }

    public string? DetailEntityTypeKey
    {
        get => detailEntityTypeKey;
        private set => SetProperty(ref detailEntityTypeKey, value);
    }

    public string? DetailEntityKey
    {
        get => detailEntityKey;
        private set => SetProperty(ref detailEntityKey, value);
    }

    public string? JobDetailJson
    {
        get => jobDetailJson;
        private set => SetProperty(ref jobDetailJson, value);
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

        var poll = Interlocked.Exchange(ref pollCts, null);
        if (poll is not null)
        {
            poll.Cancel();
            poll.Dispose();
        }

        var detail = Interlocked.Exchange(ref detailCts, null);
        if (detail is not null)
        {
            detail.Cancel();
            detail.Dispose();
        }
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
        // Rolling window: compute sinceUtc on each poll.
        var sinceUtc = DateTimeOffset.UtcNow - TimeSpan.FromHours(24);

        JobsApiClient.JobsListResponseDto response;
        try
        {
            response = await jobsApi.GetJobsAsync(sinceUtc, 200, null, null, null, cancellationToken);
        }
        catch (JobsApiException ex)
        {
            UpdateUi(() =>
            {
                ConnectionStatusLine = "Disconnected";
                LastError = ex.Failure == JobsApiFailure.Unauthorized
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
                ConnectionStatusLine = "Disconnected";
                LastError = ex is TaskCanceledException
                    ? "API request timed out."
                    : ex.Message;
            });
            return;
        }

        var items = response.Items
            .Select(item => new JobListItem(
                item.JobId,
                item.StatusKey,
                item.JobTypeKey,
                item.AttemptCount,
                item.CreatedAt,
                item.LockedUntil))
            .ToList();

        UpdateUi(() =>
        {
            var selectedId = SelectedJob?.JobId;

            Jobs.Clear();
            foreach (var item in items)
            {
                Jobs.Add(item);
            }

            ConnectionStatusLine = "Connected";
            LastError = null;

            if (selectedId is null)
            {
                SelectedJob = null;
                return;
            }

            var stillSelected = Jobs.FirstOrDefault(job => job.JobId == selectedId);
            SelectedJob = stillSelected;
            if (stillSelected is null)
            {
                DetailError = null;
                ClearDetailFields();
                ShowPayload = false;
            }
        });
    }

    private async Task FetchDetailAsync(string jobId)
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

        JobsApiClient.JobDetailDto detail;
        try
        {
            detail = await jobsApi.GetJobAsync(jobId, cts.Token);
        }
        catch (JobsApiException ex)
        {
            UpdateUi(() =>
            {
                DetailError = ex.Failure == JobsApiFailure.Unauthorized
                    ? "Unauthorized (X-MGF-API-KEY rejected)."
                    : ex.Message;
                ClearDetailFields();
                ShowPayload = false;
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
                ShowPayload = false;
            });
            return;
        }

        UpdateUi(() =>
        {
            if (SelectedJob?.JobId != jobId)
            {
                return;
            }

            DetailError = null;
            DetailJobId = detail.JobId;
            DetailStatusKey = detail.StatusKey;
            DetailJobTypeKey = detail.JobTypeKey;
            DetailAttemptCount = detail.AttemptCount.ToString();
            DetailCreatedAt = detail.CreatedAt.ToString("O");
            DetailRunAfter = detail.RunAfter.ToString("O");
            DetailStartedAt = detail.StartedAt?.ToString("O") ?? string.Empty;
            DetailFinishedAt = detail.FinishedAt?.ToString("O") ?? string.Empty;
            DetailLockedUntil = detail.LockedUntil?.ToString("O") ?? string.Empty;
            DetailLastError = detail.LastError ?? string.Empty;
            DetailEntityTypeKey = detail.EntityTypeKey ?? string.Empty;
            DetailEntityKey = detail.EntityKey ?? string.Empty;
            payloadElement = detail.Payload;
            UpdatePayloadDisplay();
        });
    }

    private void UpdatePayloadDisplay()
    {
        if (!ShowPayload)
        {
            JobDetailJson = null;
            return;
        }

        if (payloadElement is null)
        {
            JobDetailJson = null;
            return;
        }

        JobDetailJson = FormatPayload(payloadElement.Value);
    }

    private string FormatPayload(JsonElement payload)
    {
        var formatted = JsonSerializer.Serialize(payload, JsonOptions);
        if (formatted.Length <= MaxPayloadChars)
        {
            return formatted;
        }

        return formatted[..MaxPayloadChars] + "...(truncated)";
    }

    private void ClearDetailFields()
    {
        payloadElement = null;
        DetailJobId = null;
        DetailStatusKey = null;
        DetailJobTypeKey = null;
        DetailAttemptCount = null;
        DetailCreatedAt = null;
        DetailRunAfter = null;
        DetailStartedAt = null;
        DetailFinishedAt = null;
        DetailLockedUntil = null;
        DetailLastError = null;
        DetailEntityTypeKey = null;
        DetailEntityKey = null;
        JobDetailJson = null;
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

    public sealed record JobListItem(
        string JobId,
        string StatusKey,
        string JobTypeKey,
        int AttemptCount,
        DateTimeOffset CreatedAt,
        DateTimeOffset? LockedUntil
    );
}
