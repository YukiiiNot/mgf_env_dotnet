using System.Text.Json;
using MGF.Contracts.Abstractions;
using MGF.Contracts.Abstractions.ProjectBootstrap;
using MGF.Contracts.Abstractions.ProjectWorkflows;
using MGF.Domain.Entities;
using MGF.UseCases.Operations.ProjectBootstrap.BootstrapProject;

namespace MGF.UseCases.Tests;

public sealed class BootstrapProjectLockTests
{
    [Fact]
    public async Task ExecuteAsync_Throws_WhenLockUnavailable()
    {
        var project = BuildProject("prj_bootstrap");
        var store = new FakeBootstrapStore();
        var gateway = new FakeGateway();
        var workflowLock = new FakeWorkflowLock { NextLease = null };

        var useCase = new BootstrapProjectUseCase(
            new FakeProjectRepository(project),
            new FakeClientRepository(new Client(project.ClientId, "Client")),
            store,
            gateway,
            workflowLock);

        var request = BuildRequest(project.ProjectId, "job_bootstrap");

        await Assert.ThrowsAsync<ProjectWorkflowLockUnavailableException>(() => useCase.ExecuteAsync(request));

        Assert.Single(workflowLock.Requests);
        Assert.Equal((StorageMutationScopes.ForProject(project.ProjectId), ProjectWorkflowKinds.StorageMutation, "job_bootstrap"), workflowLock.Requests[0]);
        Assert.Empty(store.Calls);
        Assert.False(gateway.Called);
    }

    [Fact]
    public async Task ExecuteAsync_CallsGateway_WhenLockAcquired()
    {
        var project = BuildProject("prj_bootstrap_ok");
        var store = new FakeBootstrapStore();
        var gateway = new FakeGateway();
        var lease = new FakeWorkflowLease(StorageMutationScopes.ForProject(project.ProjectId), ProjectWorkflowKinds.StorageMutation, "job_bootstrap_ok");
        var workflowLock = new FakeWorkflowLock { NextLease = lease };

        var useCase = new BootstrapProjectUseCase(
            new FakeProjectRepository(project),
            new FakeClientRepository(new Client(project.ClientId, "Client")),
            store,
            gateway,
            workflowLock);

        var result = await useCase.ExecuteAsync(BuildRequest(project.ProjectId, "job_bootstrap_ok"));

        Assert.NotNull(result.RunResult);
        Assert.True(gateway.Called);
        Assert.Contains("status:provisioning", store.Calls);
        Assert.Contains("status:active", store.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_ReleasesLock_WhenGatewayThrows()
    {
        var project = BuildProject("prj_bootstrap_fail");
        var store = new FakeBootstrapStore();
        var gateway = new FakeGateway { Exception = new InvalidOperationException("boom") };
        var lease = new FakeWorkflowLease(StorageMutationScopes.ForProject(project.ProjectId), ProjectWorkflowKinds.StorageMutation, "job_bootstrap_fail");
        var workflowLock = new FakeWorkflowLock { NextLease = lease };

        var useCase = new BootstrapProjectUseCase(
            new FakeProjectRepository(project),
            new FakeClientRepository(new Client(project.ClientId, "Client")),
            store,
            gateway,
            workflowLock);

        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ExecuteAsync(
            BuildRequest(project.ProjectId, "job_bootstrap_fail")));

        Assert.True(lease.Disposed);
    }

    private static Project BuildProject(string projectId)
        => new(
            projectId: projectId,
            projectCode: "MGF99-0101",
            clientId: "cli_bootstrap",
            name: "Bootstrap Project",
            statusKey: "ready_to_provision",
            phaseKey: "planning",
            dataProfile: "real",
            metadata: JsonDocument.Parse("{}").RootElement.Clone());

    private static BootstrapProjectRequest BuildRequest(string projectId, string jobId)
        => new(
            JobId: jobId,
            ProjectId: projectId,
            EditorInitials: Array.Empty<string>(),
            VerifyDomainRoots: true,
            CreateDomainRoots: false,
            ProvisionProjectContainers: true,
            AllowRepair: false,
            ForceSandbox: false,
            AllowNonReal: false,
            Force: false,
            TestMode: false,
            AllowTestCleanup: false);

    private sealed class FakeProjectRepository(Project project) : IProjectRepository
    {
        private readonly Project project = project;

        public Task<Project?> GetByIdAsync(string projectId, CancellationToken cancellationToken = default)
            => Task.FromResult<Project?>(project);

        public Task SaveAsync(Project project, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeClientRepository(Client client) : IClientRepository
    {
        private readonly Client client = client;

        public Task<Client?> GetByIdAsync(string clientId, CancellationToken cancellationToken = default)
            => Task.FromResult<Client?>(client);

        public Task SaveAsync(Client client, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeBootstrapStore : IProjectBootstrapStore
    {
        public List<string> Calls { get; } = new();

        public Task AppendProvisioningRunAsync(
            string projectId,
            JsonElement metadata,
            JsonElement runResult,
            CancellationToken cancellationToken = default)
        {
            Calls.Add("append");
            return Task.CompletedTask;
        }

        public Task UpdateProjectStatusAsync(
            string projectId,
            string statusKey,
            CancellationToken cancellationToken = default)
        {
            Calls.Add($"status:{statusKey}");
            return Task.CompletedTask;
        }

        public Task<string?> UpsertProjectStorageRootAsync(
            string projectId,
            string storageProviderKey,
            string rootKey,
            string folderRelpath,
            CancellationToken cancellationToken = default)
        {
            Calls.Add($"upsert:{storageProviderKey}");
            return Task.FromResult<string?>(null);
        }
    }

    private sealed class FakeGateway : IProjectBootstrapProvisioningGateway
    {
        public bool Called { get; private set; }
        public Exception? Exception { get; init; }

        public Task<ProjectBootstrapExecutionResult> ExecuteAsync(
            ProjectBootstrapContext context,
            BootstrapProjectRequest request,
            CancellationToken cancellationToken = default)
        {
            Called = true;
            if (Exception is not null)
            {
                throw Exception;
            }

            var summary = new ProvisioningSummary(
                Mode: "verify",
                TemplateKey: "template",
                TargetRoot: "C:\\root",
                ManifestPath: "C:\\root\\manifest.json",
                Success: true,
                MissingRequired: Array.Empty<string>(),
                Errors: Array.Empty<string>(),
                Warnings: Array.Empty<string>());

            var domain = new ProjectBootstrapDomainResult(
                DomainKey: "dropbox",
                RootPath: "C:\\root",
                RootState: "root_verified",
                DomainRootProvisioning: summary,
                ProjectContainerProvisioning: summary,
                Notes: Array.Empty<string>());

            var runResult = new ProjectBootstrapRunResult(
                JobId: request.JobId,
                ProjectId: request.ProjectId,
                EditorInitials: request.EditorInitials,
                StartedAtUtc: DateTimeOffset.UtcNow,
                VerifyDomainRoots: request.VerifyDomainRoots,
                CreateDomainRoots: request.CreateDomainRoots,
                ProvisionProjectContainers: request.ProvisionProjectContainers,
                AllowRepair: request.AllowRepair,
                ForceSandbox: request.ForceSandbox,
                AllowNonReal: request.AllowNonReal,
                Force: request.Force,
                TestMode: request.TestMode,
                AllowTestCleanup: request.AllowTestCleanup,
                Domains: new[] { domain },
                HasErrors: false,
                LastError: null);

            return Task.FromResult(new ProjectBootstrapExecutionResult(
                runResult,
                Array.Empty<ProjectBootstrapStorageRootCandidate>(),
                null));
        }

        public ProjectBootstrapRunResult BuildBlockedNonRealResult(
            ProjectBootstrapContext context,
            BootstrapProjectRequest request)
        {
            throw new NotSupportedException("Not used in this test.");
        }

        public ProjectBootstrapRunResult BuildBlockedStatusResult(
            ProjectBootstrapContext context,
            BootstrapProjectRequest request,
            string? statusError,
            bool alreadyProvisioning)
        {
            throw new NotSupportedException("Not used in this test.");
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
