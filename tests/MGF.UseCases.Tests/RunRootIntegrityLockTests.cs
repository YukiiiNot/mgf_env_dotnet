using MGF.Contracts.Abstractions.ProjectWorkflows;
using MGF.Contracts.Abstractions.RootIntegrity;
using MGF.UseCases.Operations.RootIntegrity.RunRootIntegrity;

namespace MGF.UseCases.Tests;

public sealed class RunRootIntegrityLockTests
{
    [Fact]
    public async Task ExecuteAsync_Throws_WhenLockUnavailable()
    {
        var store = new FakeRootIntegrityStore(BuildContract());
        var executor = new FakeRootIntegrityExecutor();
        var workflowLock = new FakeWorkflowLock { NextLease = null };

        var useCase = new RunRootIntegrityUseCase(store, executor, workflowLock);

        await Assert.ThrowsAsync<ProjectWorkflowLockUnavailableException>(() => useCase.ExecuteAsync(
            new RunRootIntegrityRequest(BuildPayload(), "job_root")));

        Assert.Single(workflowLock.Requests);
        Assert.Equal(("root_integrity:dropbox:root", ProjectWorkflowKinds.StorageMutation, "job_root"), workflowLock.Requests[0]);
        Assert.False(executor.Called);
    }

    [Fact]
    public async Task ExecuteAsync_CallsExecutor_WhenLockAcquired()
    {
        var store = new FakeRootIntegrityStore(BuildContract());
        var executor = new FakeRootIntegrityExecutor();
        var lease = new FakeWorkflowLease("root_integrity:dropbox:root", ProjectWorkflowKinds.StorageMutation, "job_root_ok");
        var workflowLock = new FakeWorkflowLock { NextLease = lease };

        var useCase = new RunRootIntegrityUseCase(store, executor, workflowLock);

        var result = await useCase.ExecuteAsync(new RunRootIntegrityRequest(BuildPayload(), "job_root_ok"));

        Assert.NotNull(result.Result);
        Assert.True(executor.Called);
    }

    [Fact]
    public async Task ExecuteAsync_ReleasesLock_WhenExecutorThrows()
    {
        var store = new FakeRootIntegrityStore(BuildContract());
        var executor = new FakeRootIntegrityExecutor { Exception = new InvalidOperationException("boom") };
        var lease = new FakeWorkflowLease("root_integrity:dropbox:root", ProjectWorkflowKinds.StorageMutation, "job_root_fail");
        var workflowLock = new FakeWorkflowLock { NextLease = lease };

        var useCase = new RunRootIntegrityUseCase(store, executor, workflowLock);

        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ExecuteAsync(
            new RunRootIntegrityRequest(BuildPayload(), "job_root_fail")));

        Assert.True(lease.Disposed);
    }

    private static RootIntegrityPayload BuildPayload()
        => new(
            ProviderKey: "dropbox",
            RootKey: "root",
            Mode: "report",
            DryRun: true,
            QuarantineRelpath: null,
            MaxItems: null,
            MaxBytes: null,
            AllowedExtras: null,
            AllowedRootFiles: null);

    private static RootIntegrityContract BuildContract()
        => new(
            ProviderKey: "dropbox",
            RootKey: "root",
            ContractKey: "dropbox_root",
            RequiredFolders: Array.Empty<string>(),
            OptionalFolders: Array.Empty<string>(),
            AllowedExtras: Array.Empty<string>(),
            AllowedRootFiles: Array.Empty<string>(),
            QuarantineRelpath: null,
            MaxItems: null,
            MaxBytes: null,
            IsActive: true);

    private sealed class FakeRootIntegrityStore : IRootIntegrityStore
    {
        private readonly RootIntegrityContract contract;

        public FakeRootIntegrityStore(RootIntegrityContract contract)
        {
            this.contract = contract;
        }

        public Task<RootIntegrityContract?> GetContractAsync(
            string providerKey,
            string rootKey,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<RootIntegrityContract?>(contract);
        }
    }

    private sealed class FakeRootIntegrityExecutor : IRootIntegrityExecutor
    {
        public bool Called { get; private set; }
        public Exception? Exception { get; init; }

        public Task<RootIntegrityResult> ExecuteAsync(
            RootIntegrityPayload payload,
            RootIntegrityContract contract,
            string jobId,
            CancellationToken cancellationToken = default)
        {
            Called = true;
            if (Exception is not null)
            {
                throw Exception;
            }

            var result = new RootIntegrityResult(
                ProviderKey: payload.ProviderKey,
                RootKey: payload.RootKey,
                RootPath: "C:\\root",
                Mode: payload.Mode,
                DryRun: payload.DryRun,
                StartedAt: DateTimeOffset.UtcNow,
                FinishedAt: DateTimeOffset.UtcNow,
                MissingRequired: Array.Empty<string>(),
                MissingOptional: Array.Empty<string>(),
                UnknownEntries: Array.Empty<RootIntegrityEntry>(),
                RootFiles: Array.Empty<RootIntegrityEntry>(),
                QuarantinePlan: Array.Empty<RootIntegrityMovePlan>(),
                GuardrailBlocks: Array.Empty<RootIntegrityMovePlan>(),
                Actions: Array.Empty<RootIntegrityAction>(),
                Warnings: Array.Empty<string>(),
                Errors: Array.Empty<string>());

            return Task.FromResult(result);
        }
    }

    private sealed class FakeWorkflowLock : IProjectWorkflowLock
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
