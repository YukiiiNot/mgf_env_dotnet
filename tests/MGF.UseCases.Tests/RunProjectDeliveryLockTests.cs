using System.Text.Json;
using MGF.Contracts.Abstractions;
using MGF.Contracts.Abstractions.Email;
using MGF.Contracts.Abstractions.ProjectDelivery;
using MGF.Contracts.Abstractions.ProjectWorkflows;
using MGF.Domain.Entities;
using MGF.UseCases.Operations.ProjectDelivery.RunProjectDelivery;

namespace MGF.UseCases.Tests;

public sealed class RunProjectDeliveryLockTests
{
    [Fact]
    public async Task ExecuteAsync_Throws_WhenLockUnavailable()
    {
        var payload = BuildPayload("prj_1");
        var leaseStore = new FakeProjectWorkflowLock { NextLease = null };
        var store = new FakeProjectDeliveryStore();
        var executor = new FakeProjectDeliveryExecutor { ThrowOnResolve = true };

        var useCase = new RunProjectDeliveryUseCase(
            new FakeProjectRepository(BuildProject("prj_1")),
            new FakeClientRepository(new Client("cli_1", "Client")),
            new FakeProjectDeliveryData(),
            store,
            executor,
            leaseStore);

        await Assert.ThrowsAsync<ProjectWorkflowLockUnavailableException>(() => useCase.ExecuteAsync(
            new RunProjectDeliveryRequest(payload, "job_1")));

        Assert.Single(leaseStore.Requests);
        Assert.Equal(("prj_1", ProjectWorkflowKinds.StorageMutation, "job_1"), leaseStore.Requests[0]);
        Assert.Empty(store.StatusUpdates);
        Assert.False(executor.ResolveCalled);
    }

    [Fact]
    public async Task ExecuteAsync_CallsExecutor_WhenLockAcquired()
    {
        var payload = BuildPayload("prj_3");
        var lease = new FakeWorkflowLease("prj_3", ProjectWorkflowKinds.StorageMutation, "job_3");
        var leaseStore = new FakeProjectWorkflowLock { NextLease = lease };
        var store = new FakeProjectDeliveryStore();
        var executor = new FakeProjectDeliveryExecutor();

        var useCase = new RunProjectDeliveryUseCase(
            new FakeProjectRepository(BuildProject("prj_3")),
            new FakeClientRepository(new Client("cli_3", "Client")),
            new FakeProjectDeliveryData(),
            store,
            executor,
            leaseStore);

        var result = await useCase.ExecuteAsync(new RunProjectDeliveryRequest(payload, "job_3"));

        Assert.NotNull(result.Result);
        Assert.True(executor.ResolveCalled);
        Assert.Contains(store.StatusUpdates, update => update.StatusKey == "delivering");
        Assert.Contains(store.StatusUpdates, update => update.StatusKey == "delivered");
    }

    [Fact]
    public async Task ExecuteAsync_ReleasesLock_WhenExecutorThrows()
    {
        var payload = BuildPayload("prj_2");
        var lease = new FakeWorkflowLease("prj_2", ProjectWorkflowKinds.StorageMutation, "job_2");
        var leaseStore = new FakeProjectWorkflowLock { NextLease = lease };
        var store = new FakeProjectDeliveryStore();
        var executor = new FakeProjectDeliveryExecutor { ResolveException = new InvalidOperationException("boom") };

        var useCase = new RunProjectDeliveryUseCase(
            new FakeProjectRepository(BuildProject("prj_2")),
            new FakeClientRepository(new Client("cli_2", "Client")),
            new FakeProjectDeliveryData(),
            store,
            executor,
            leaseStore);

        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ExecuteAsync(
            new RunProjectDeliveryRequest(payload, "job_2")));

        Assert.True(lease.Disposed);
    }

    private static Project BuildProject(string projectId)
        => new(
            projectId: projectId,
            projectCode: "MGF99-0001",
            clientId: "cli_1",
            name: "Project",
            statusKey: "ready_to_deliver",
            phaseKey: "planning",
            dataProfile: "real");

    private static ProjectDeliveryPayload BuildPayload(string projectId)
        => new(
            ProjectId: projectId,
            EditorInitials: new[] { "TE" },
            ToEmails: new[] { "test@example.com" },
            ReplyToEmail: "reply@example.com",
            TestMode: false,
            AllowTestCleanup: false,
            AllowNonReal: false,
            Force: false,
            RefreshShareLink: false);

    private sealed class FakeProjectRepository(Project project) : IProjectRepository
    {
        private readonly Project project = project;

        public Task<Project?> GetByIdAsync(string projectId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<Project?>(project);
        }

        public Task SaveAsync(Project project, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeClientRepository(Client client) : IClientRepository
    {
        private readonly Client client = client;

        public Task<Client?> GetByIdAsync(string clientId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<Client?>(client);
        }

        public Task SaveAsync(Client client, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeProjectDeliveryData : IProjectDeliveryData
    {
        public string DropboxRelpath { get; init; } = "dropbox";
        public string? StorageRelpath { get; init; } = "lucidlink";

        public Task<string?> GetProjectStorageRootRelpathAsync(
            string projectId,
            string storageProviderKey,
            bool testMode,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(StorageRelpath);
        }

        public Task<string> GetDropboxDeliveryRelpathAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(DropboxRelpath);
        }
    }

    private sealed class FakeProjectDeliveryStore : IProjectDeliveryStore
    {
        public List<(string ProjectId, string StatusKey)> StatusUpdates { get; } = new();

        public Task AppendDeliveryRunAsync(
            string projectId,
            JsonElement metadata,
            JsonElement runResult,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task AppendDeliveryEmailAsync(
            string projectId,
            JsonElement metadata,
            JsonElement emailResult,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task UpdateProjectStatusAsync(
            string projectId,
            string statusKey,
            CancellationToken cancellationToken = default)
        {
            StatusUpdates.Add((projectId, statusKey));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeProjectDeliveryExecutor : IProjectDeliveryExecutor
    {
        public bool ThrowOnResolve { get; init; }
        public Exception? ResolveException { get; init; }
        public bool ResolveCalled { get; private set; }

        public Task<ProjectDeliverySourceResult> ResolveLucidlinkSourceAsync(
            ProjectDeliveryPayload payload,
            string? storageRelpath,
            CancellationToken cancellationToken = default)
        {
            ResolveCalled = true;
            if (ThrowOnResolve)
            {
                throw new InvalidOperationException("Resolve should not be called.");
            }

            if (ResolveException is not null)
            {
                throw ResolveException;
            }

            var domain = new ProjectDeliveryDomainResult(
                DomainKey: "lucidlink",
                RootPath: storageRelpath ?? string.Empty,
                RootState: "source_ready",
                DeliveryContainerProvisioning: null,
                Deliverables: Array.Empty<DeliveryFileSummary>(),
                VersionLabel: null,
                DestinationPath: null,
                Notes: Array.Empty<string>());

            return Task.FromResult(new ProjectDeliverySourceResult(
                SourcePath: storageRelpath,
                Files: Array.Empty<DeliveryFile>(),
                DomainResult: domain));
        }

        public Task<ProjectDeliveryTargetResult> ProcessDropboxAsync(
            ProjectDeliveryPayload payload,
            DeliveryTokens tokens,
            ProjectDeliverySourceResult source,
            string dropboxDeliveryRelpath,
            JsonElement projectMetadata,
            CancellationToken cancellationToken = default)
        {
            var domain = new ProjectDeliveryDomainResult(
                DomainKey: "dropbox",
                RootPath: dropboxDeliveryRelpath,
                RootState: "delivery_ready",
                DeliveryContainerProvisioning: null,
                Deliverables: Array.Empty<DeliveryFileSummary>(),
                VersionLabel: null,
                DestinationPath: null,
                Notes: Array.Empty<string>());

            return Task.FromResult(new ProjectDeliveryTargetResult(
                DestinationPath: null,
                ApiStablePath: null,
                ApiVersionPath: null,
                VersionLabel: null,
                RetentionUntilUtc: null,
                DomainResult: domain,
                ShareStatus: null,
                ShareUrl: null,
                ShareId: null,
                ShareError: null));
        }

        public Task<EmailSendResult> SendDeliveryEmailAsync(
            ProjectDeliveryPayload payload,
            DeliveryTokens tokens,
            ProjectDeliverySourceResult source,
            ProjectDeliveryTargetResult target,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new EmailSendResult(
                Status: "sent",
                Provider: "preview",
                FromAddress: "preview@example.com",
                To: payload.ToEmails,
                Subject: "Test",
                SentAtUtc: DateTimeOffset.UtcNow,
                ProviderMessageId: null,
                Error: null,
                TemplateVersion: "test",
                ReplyTo: payload.ReplyToEmail));
        }
    }

    private sealed class FakeProjectWorkflowLock : IProjectWorkflowLock
    {
        public IProjectWorkflowLease? NextLease { get; init; }
        public List<(string ProjectId, string WorkflowKind, string HolderId)> Requests { get; } = new();

        public Task<IProjectWorkflowLease?> TryAcquireAsync(
            string projectId,
            string workflowKind,
            string holderId,
            CancellationToken cancellationToken = default)
        {
            Requests.Add((projectId, workflowKind, holderId));
            return Task.FromResult(NextLease);
        }
    }

    private sealed class FakeWorkflowLease : IProjectWorkflowLease
    {
        public FakeWorkflowLease(string projectId, string workflowKind, string holderId)
        {
            ProjectId = projectId;
            WorkflowKind = workflowKind;
            HolderId = holderId;
        }

        public string ProjectId { get; }
        public string WorkflowKind { get; }
        public string HolderId { get; }
        public bool Disposed { get; private set; }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
