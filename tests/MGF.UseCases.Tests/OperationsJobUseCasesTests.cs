using System.Text.Json;
using MGF.Contracts.Abstractions.Operations.Jobs;
using MGF.UseCases.Operations.Jobs.EnqueueProjectArchiveJob;
using MGF.UseCases.Operations.Jobs.EnqueueProjectBootstrapJob;
using MGF.UseCases.Operations.Jobs.EnqueueProjectDeliveryEmailJob;
using MGF.UseCases.Operations.Jobs.EnqueueProjectDeliveryJob;
using MGF.UseCases.Operations.Jobs.EnqueueRootIntegrityJob;
using MGF.UseCases.Operations.Jobs.GetRootIntegrityJobs;
using MGF.UseCases.Operations.Jobs.RequeueStaleJobs;
using MGF.UseCases.Operations.Jobs.ResetProjectJobs;

namespace MGF.UseCases.Tests;

public sealed class OperationsJobUseCasesTests
{
    [Fact]
    public async Task EnqueueProjectBootstrapJob_ReturnsNotEnqueued_WhenExistingJob()
    {
        var store = new FakeJobStore
        {
            ExistingJob = new ExistingJob("job_123", "queued")
        };
        var useCase = new EnqueueProjectBootstrapJobUseCase(store);

        var result = await useCase.ExecuteAsync(new EnqueueProjectBootstrapJobRequest(
            ProjectId: "prj_123",
            EditorInitials: Array.Empty<string>(),
            VerifyDomainRoots: true,
            CreateDomainRoots: false,
            ProvisionProjectContainers: false,
            AllowRepair: false,
            ForceSandbox: false,
            AllowNonReal: false,
            Force: false,
            TestMode: false,
            AllowTestCleanup: false));

        Assert.False(result.Enqueued);
        Assert.NotNull(result.Reason);
        Assert.Null(store.EnqueueRequest);
    }

    [Fact]
    public async Task EnqueueProjectDeliveryJob_EnqueuesRetryableJob()
    {
        var store = new FakeJobStore();
        var useCase = new EnqueueProjectDeliveryJobUseCase(store);

        var result = await useCase.ExecuteAsync(new EnqueueProjectDeliveryJobRequest(
            ProjectId: "prj_456",
            EditorInitials: new[] { "TE" },
            ToEmails: new[] { "test@example.com" },
            ReplyToEmail: "reply@example.com",
            TestMode: false,
            AllowTestCleanup: false,
            AllowNonReal: false,
            Force: false,
            RefreshShareLink: true));

        Assert.True(result.Enqueued);
        Assert.NotNull(result.JobId);
        Assert.StartsWith("job", result.JobId, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(store.RetryableRequest);
        Assert.Equal("project.delivery", store.RetryableRequest!.JobTypeKey);
        Assert.Equal("project", store.RetryableRequest.EntityTypeKey);
        Assert.Equal("prj_456", store.RetryableRequest.EntityKey);
        Assert.Equal(0, store.RetryableRequest.AttemptCount);
        Assert.Equal(5, store.RetryableRequest.MaxAttempts);

        using var doc = JsonDocument.Parse(result.PayloadJson ?? "{}");
        Assert.True(doc.RootElement.TryGetProperty("projectId", out _));
    }

    [Fact]
    public async Task EnqueueProjectArchiveJob_EnsuresJobTypeAndEnqueues()
    {
        var store = new FakeJobStore();
        var useCase = new EnqueueProjectArchiveJobUseCase(store);

        var result = await useCase.ExecuteAsync(new EnqueueProjectArchiveJobRequest(
            ProjectId: "prj_789",
            EditorInitials: new[] { "TE" },
            TestMode: false,
            AllowTestCleanup: false,
            AllowNonReal: false,
            Force: false));

        Assert.True(result.Enqueued);
        Assert.Single(store.EnsureRequests);
        Assert.Equal("project.archive", store.EnsureRequests[0].JobTypeKey);
        Assert.Equal("Project: Archive", store.EnsureRequests[0].DisplayName);
        Assert.NotNull(store.EnqueueRequest);
        Assert.Equal("project.archive", store.EnqueueRequest!.JobTypeKey);
        Assert.Equal("project", store.EnqueueRequest.EntityTypeKey);
        Assert.Equal("prj_789", store.EnqueueRequest.EntityKey);
    }

    [Fact]
    public async Task EnqueueProjectDeliveryEmailJob_UsesRetryableEnqueue()
    {
        var store = new FakeJobStore();
        var useCase = new EnqueueProjectDeliveryEmailJobUseCase(store);

        var result = await useCase.ExecuteAsync(new EnqueueProjectDeliveryEmailJobRequest(
            ProjectId: "prj_999",
            EditorInitials: new[] { "TE" },
            ToEmails: new[] { "info@mgfilms.pro" },
            ReplyToEmail: "info@mgfilms.pro"));

        Assert.True(result.Enqueued);
        Assert.NotNull(store.RetryableRequest);
        Assert.Equal("project.delivery_email", store.RetryableRequest!.JobTypeKey);
        Assert.Equal("project", store.RetryableRequest.EntityTypeKey);
        Assert.Equal("prj_999", store.RetryableRequest.EntityKey);
        Assert.Equal(0, store.RetryableRequest.AttemptCount);
        Assert.Equal(5, store.RetryableRequest.MaxAttempts);
    }

    [Fact]
    public async Task EnqueueRootIntegrityJob_EnsuresJobTypeAndEnqueues()
    {
        var store = new FakeJobStore();
        var useCase = new EnqueueRootIntegrityJobUseCase(store);

        var result = await useCase.ExecuteAsync(new EnqueueRootIntegrityJobRequest(
            ProviderKey: "dropbox",
            RootKey: "root",
            Mode: "audit",
            DryRun: true,
            QuarantineRelpath: null,
            MaxItems: 10,
            MaxBytes: 2048));

        Assert.Single(store.EnsureRequests);
        Assert.Equal("domain.root_integrity", store.EnsureRequests[0].JobTypeKey);
        Assert.NotNull(store.EnqueueRequest);
        Assert.Equal("domain.root_integrity", store.EnqueueRequest!.JobTypeKey);
        Assert.Equal("storage_root", store.EnqueueRequest.EntityTypeKey);
        Assert.Equal("dropbox:root", store.EnqueueRequest.EntityKey);
        Assert.Equal(result.JobId, store.EnqueueRequest.JobId);
    }

    [Fact]
    public async Task ResetProjectJobs_ReturnsAffectedRows()
    {
        var store = new FakeJobStore { ResetJobsResult = 2 };
        var useCase = new ResetProjectJobsUseCase(store);

        var result = await useCase.ExecuteAsync(new ResetProjectJobsRequest(
            ProjectId: "prj_reset",
            JobTypeKey: "project.bootstrap"));

        Assert.Equal(2, result.RowsAffected);
        Assert.Equal(("prj_reset", "project.bootstrap"), store.ResetRequest);
    }

    [Fact]
    public async Task RequeueStaleJobs_DryRun_UsesCount()
    {
        var store = new FakeJobStore { StaleCountResult = 7 };
        var useCase = new RequeueStaleJobsUseCase(store);

        var result = await useCase.ExecuteAsync(new RequeueStaleJobsRequest(DryRun: true));

        Assert.True(result.WasDryRun);
        Assert.Equal(7, result.Count);
    }

    [Fact]
    public async Task RequeueStaleJobs_Apply_UsesRequeue()
    {
        var store = new FakeJobStore { RequeueResult = 4 };
        var useCase = new RequeueStaleJobsUseCase(store);

        var result = await useCase.ExecuteAsync(new RequeueStaleJobsRequest(DryRun: false));

        Assert.False(result.WasDryRun);
        Assert.Equal(4, result.Count);
    }

    [Fact]
    public async Task GetRootIntegrityJobs_ReturnsStoreResults()
    {
        var summary = new RootIntegrityJobSummary(
            JobId: "job_root",
            StatusKey: "queued",
            AttemptCount: 0,
            RunAfter: DateTimeOffset.UtcNow,
            LockedUntil: null,
            PayloadJson: "{}");
        var store = new FakeJobStore
        {
            RootIntegrityJobs = new[] { summary }
        };
        var useCase = new GetRootIntegrityJobsUseCase(store);

        var result = await useCase.ExecuteAsync(new GetRootIntegrityJobsRequest(
            ProviderKey: "dropbox",
            RootKey: "root",
            Limit: 1));

        Assert.Single(store.RootIntegrityRequests);
        Assert.Equal(("dropbox:root", 1), store.RootIntegrityRequests[0]);
        Assert.Single(result.Jobs);
        Assert.Equal(summary.JobId, result.Jobs[0].JobId);
    }

    private sealed class FakeJobStore : IJobOpsStore
    {
        public ExistingJob? ExistingJob { get; set; }
        public JobEnqueueRequest? EnqueueRequest { get; private set; }
        public RetryableJobEnqueueRequest? RetryableRequest { get; private set; }
        public List<(string JobTypeKey, string DisplayName)> EnsureRequests { get; } = new();
        public List<(string JobTypeKey, string EntityTypeKey, string EntityKey)> FindRequests { get; } = new();
        public List<(string EntityKey, int Limit)> RootIntegrityRequests { get; } = new();
        public IReadOnlyList<JobSummary> Jobs { get; set; } = Array.Empty<JobSummary>();
        public IReadOnlyList<RootIntegrityJobSummary> RootIntegrityJobs { get; set; } = Array.Empty<RootIntegrityJobSummary>();
        public int ResetJobsResult { get; set; }
        public int StaleCountResult { get; set; }
        public int RequeueResult { get; set; }
        public (string ProjectId, string JobTypeKey)? ResetRequest { get; private set; }

        public Task EnsureJobTypeAsync(
            string jobTypeKey,
            string displayName,
            CancellationToken cancellationToken = default)
        {
            EnsureRequests.Add((jobTypeKey, displayName));
            return Task.CompletedTask;
        }

        public Task<ExistingJob?> FindExistingJobAsync(
            string jobTypeKey,
            string entityTypeKey,
            string entityKey,
            CancellationToken cancellationToken = default)
        {
            FindRequests.Add((jobTypeKey, entityTypeKey, entityKey));
            return Task.FromResult(ExistingJob);
        }

        public Task EnqueueJobAsync(
            JobEnqueueRequest request,
            CancellationToken cancellationToken = default)
        {
            EnqueueRequest = request;
            return Task.CompletedTask;
        }

        public Task EnqueueRetryableJobAsync(
            RetryableJobEnqueueRequest request,
            CancellationToken cancellationToken = default)
        {
            RetryableRequest = request;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<JobSummary>> GetJobsAsync(
            string jobTypeKey,
            string entityTypeKey,
            string entityKey,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Jobs);
        }

        public Task<IReadOnlyList<RootIntegrityJobSummary>> GetRootIntegrityJobsAsync(
            string entityKey,
            int limit,
            CancellationToken cancellationToken = default)
        {
            RootIntegrityRequests.Add((entityKey, limit));
            return Task.FromResult(RootIntegrityJobs);
        }

        public Task<int> ResetJobsAsync(
            string projectId,
            string jobTypeKey,
            CancellationToken cancellationToken = default)
        {
            ResetRequest = (projectId, jobTypeKey);
            return Task.FromResult(ResetJobsResult);
        }

        public Task<int> CountStaleRunningJobsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(StaleCountResult);
        }

        public Task<int> RequeueStaleRunningJobsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(RequeueResult);
        }
    }
}
