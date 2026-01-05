using System.Text.Json;
using MGF.Contracts.Abstractions;
using MGF.Contracts.Abstractions.ProjectArchive;
using MGF.Contracts.Abstractions.ProjectWorkflows;
using MGF.Domain.Entities;
using MGF.UseCases.Operations.ProjectArchive.RunProjectArchive;

namespace MGF.UseCases.Tests;

public sealed class RunProjectArchiveLockTests
{
    [Fact]
    public async Task ExecuteAsync_Throws_WhenLockUnavailable()
    {
        var project = BuildProject("prj_archive");
        var store = new FakeArchiveStore();
        var executor = new FakeArchiveExecutor();
        var data = new FakeArchiveData();
        var workflowLock = new FakeWorkflowLock { NextLease = null };

        var useCase = new RunProjectArchiveUseCase(
            new FakeProjectRepository(project),
            new FakeClientRepository(new Client(project.ClientId, "Client")),
            data,
            store,
            executor,
            workflowLock);

        await Assert.ThrowsAsync<ProjectWorkflowLockUnavailableException>(() => useCase.ExecuteAsync(
            new RunProjectArchiveRequest(BuildPayload(project.ProjectId), "job_archive")));

        Assert.Single(workflowLock.Requests);
        Assert.Equal((StorageMutationScopes.ForProject(project.ProjectId), ProjectWorkflowKinds.StorageMutation, "job_archive"), workflowLock.Requests[0]);
        Assert.Empty(store.StatusUpdates);
        Assert.False(executor.Called);
    }

    [Fact]
    public async Task ExecuteAsync_CallsExecutor_WhenLockAcquired()
    {
        var project = BuildProject("prj_archive_ok");
        var store = new FakeArchiveStore();
        var executor = new FakeArchiveExecutor();
        var data = new FakeArchiveData();
        var lease = new FakeWorkflowLease(StorageMutationScopes.ForProject(project.ProjectId), ProjectWorkflowKinds.StorageMutation, "job_archive_ok");
        var workflowLock = new FakeWorkflowLock { NextLease = lease };

        var useCase = new RunProjectArchiveUseCase(
            new FakeProjectRepository(project),
            new FakeClientRepository(new Client(project.ClientId, "Client")),
            data,
            store,
            executor,
            workflowLock);

        var result = await useCase.ExecuteAsync(
            new RunProjectArchiveRequest(BuildPayload(project.ProjectId), "job_archive_ok"));

        Assert.NotNull(result.Result);
        Assert.True(executor.Called);
        Assert.Contains(store.StatusUpdates, update => update.StatusKey == "archiving");
        Assert.Contains(store.StatusUpdates, update => update.StatusKey == "archived");
    }

    [Fact]
    public async Task ExecuteAsync_ReleasesLock_WhenExecutorThrows()
    {
        var project = BuildProject("prj_archive_fail");
        var store = new FakeArchiveStore();
        var executor = new FakeArchiveExecutor { Exception = new InvalidOperationException("boom") };
        var data = new FakeArchiveData();
        var lease = new FakeWorkflowLease(StorageMutationScopes.ForProject(project.ProjectId), ProjectWorkflowKinds.StorageMutation, "job_archive_fail");
        var workflowLock = new FakeWorkflowLock { NextLease = lease };

        var useCase = new RunProjectArchiveUseCase(
            new FakeProjectRepository(project),
            new FakeClientRepository(new Client(project.ClientId, "Client")),
            data,
            store,
            executor,
            workflowLock);

        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ExecuteAsync(
            new RunProjectArchiveRequest(BuildPayload(project.ProjectId), "job_archive_fail")));

        Assert.True(lease.Disposed);
    }

    private static Project BuildProject(string projectId)
        => new(
            projectId: projectId,
            projectCode: "MGF99-0201",
            clientId: "cli_archive",
            name: "Archive Project",
            statusKey: "to_archive",
            phaseKey: "planning",
            dataProfile: "real",
            metadata: JsonDocument.Parse("{}").RootElement.Clone());

    private static ProjectArchivePayload BuildPayload(string projectId)
        => new(
            ProjectId: projectId,
            EditorInitials: Array.Empty<string>(),
            TestMode: false,
            AllowTestCleanup: false,
            AllowNonReal: false,
            Force: false);

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

    private sealed class FakeArchiveData : IProjectArchiveData
    {
        public Task<ProjectArchivePathTemplates> GetArchivePathTemplatesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ProjectArchivePathTemplates(
                DropboxActiveRelpath: "Active",
                DropboxToArchiveRelpath: "ToArchive",
                DropboxArchiveRelpath: "Archive",
                NasArchiveRelpath: "NasArchive"));
        }
    }

    private sealed class FakeArchiveStore : IProjectArchiveStore
    {
        public List<(string ProjectId, string StatusKey)> StatusUpdates { get; } = new();

        public Task AppendArchiveRunAsync(
            string projectId,
            JsonElement metadata,
            JsonElement runResult,
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

    private sealed class FakeArchiveExecutor : IProjectArchiveExecutor
    {
        public bool Called { get; private set; }
        public Exception? Exception { get; init; }

        public Task<string> ResolveProjectFolderNameAsync(
            ProjectArchiveTokens tokens,
            CancellationToken cancellationToken = default)
        {
            Called = true;
            if (Exception is not null)
            {
                throw Exception;
            }

            return Task.FromResult("ProjectFolder");
        }

        public Task<ProjectArchiveDomainResult> ProcessDropboxAsync(
            ProjectArchivePayload payload,
            string projectFolderName,
            ProjectArchivePathTemplates pathTemplates,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(BuildResult("dropbox"));
        }

        public Task<ProjectArchiveDomainResult> FinalizeDropboxArchiveAsync(
            ProjectArchiveDomainResult dropboxResult,
            ProjectArchivePayload payload,
            string projectFolderName,
            ProjectArchivePathTemplates pathTemplates,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(dropboxResult);
        }

        public Task<ProjectArchiveDomainResult> ProcessLucidlinkAsync(
            ProjectArchivePayload payload,
            string projectFolderName)
        {
            return Task.FromResult(BuildResult("lucidlink"));
        }

        public Task<ProjectArchiveDomainResult> ProcessNasAsync(
            ProjectArchivePayload payload,
            string projectFolderName,
            ProjectArchiveDomainResult lucidlinkResult,
            ProjectArchiveTokens tokens,
            ProjectArchivePathTemplates pathTemplates,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(BuildResult("nas"));
        }

        private static ProjectArchiveDomainResult BuildResult(string domainKey)
        {
            return new ProjectArchiveDomainResult(
                DomainKey: domainKey,
                RootPath: "C:\\archive",
                RootState: "archived",
                DomainRootProvisioning: null,
                TargetProvisioning: null,
                Actions: Array.Empty<ArchiveActionSummary>(),
                Notes: Array.Empty<string>());
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
