using MGF.Contracts.Abstractions.Operations.Jobs;
using MGF.Contracts.Abstractions.Operations.Projects;
using MGF.UseCases.Operations.Projects.CreateTestProject;
using MGF.UseCases.Operations.Projects.GetDeliveryEmailPreviewData;
using MGF.UseCases.Operations.Projects.GetProjectSnapshot;
using MGF.UseCases.Operations.Projects.ListProjects;
using MGF.UseCases.Operations.Projects.UpdateProjectStatus;
using StoreCreateTestProjectRequest = MGF.Contracts.Abstractions.Operations.Projects.CreateTestProjectRequest;
using UseCaseCreateTestProjectRequest = MGF.UseCases.Operations.Projects.CreateTestProject.CreateTestProjectRequest;

namespace MGF.UseCases.Tests;

public sealed class OperationsProjectUseCasesTests
{
    [Fact]
    public async Task GetProjectSnapshot_ReturnsNull_WhenMissing()
    {
        var jobStore = new FakeJobStore();
        var projectStore = new FakeProjectStore();
        var useCase = new GetProjectSnapshotUseCase(jobStore, projectStore);

        var result = await useCase.ExecuteAsync(new GetProjectSnapshotRequest(
            ProjectId: "prj_missing",
            IncludeStorageRoots: true));

        Assert.Null(result);
    }

    [Fact]
    public async Task GetProjectSnapshot_ReturnsProjectWithRootsAndJobs()
    {
        var jobStore = new FakeJobStore
        {
            Jobs = new[]
            {
                new JobSummary("job_1", "queued", 0, DateTimeOffset.UtcNow, null)
            }
        };
        var projectStore = new FakeProjectStore
        {
            Project = new ProjectInfo(
                ProjectId: "prj_123",
                ProjectCode: "MGF25-0001",
                ProjectName: "Test Project",
                StatusKey: "active",
                DataProfile: "real",
                MetadataJson: "{}",
                ClientId: "cli_1"),
            Roots = new[]
            {
                new ProjectStorageRootInfo("psr_1", "dropbox", "root", "path", true, DateTimeOffset.UtcNow)
            }
        };

        var useCase = new GetProjectSnapshotUseCase(jobStore, projectStore);

        var result = await useCase.ExecuteAsync(new GetProjectSnapshotRequest(
            ProjectId: "prj_123",
            IncludeStorageRoots: true));

        Assert.NotNull(result);
        Assert.Equal("prj_123", result.Project.ProjectId);
        Assert.Single(result.StorageRoots);
        Assert.Equal(3, jobStore.JobRequests.Count);
        Assert.Contains(jobStore.JobRequests, request => request.JobTypeKey == "project.bootstrap");
        Assert.Contains(jobStore.JobRequests, request => request.JobTypeKey == "project.archive");
        Assert.Contains(jobStore.JobRequests, request => request.JobTypeKey == "project.delivery");
    }

    [Fact]
    public async Task ListProjects_ReturnsStoreResults()
    {
        var projectStore = new FakeProjectStore
        {
            Projects = new[]
            {
                new ProjectListItem("prj_1", "MGF25-0002", "Project One")
            }
        };
        var useCase = new ListProjectsUseCase(projectStore);

        var result = await useCase.ExecuteAsync(new ListProjectsRequest(10));

        Assert.Single(result.Projects);
        Assert.Equal("prj_1", result.Projects[0].ProjectId);
        Assert.Equal(10, projectStore.ListLimit);
    }

    [Fact]
    public async Task UpdateProjectStatus_ReturnsRows()
    {
        var projectStore = new FakeProjectStore { UpdateStatusRows = 2 };
        var useCase = new UpdateProjectStatusUseCase(projectStore);

        var result = await useCase.ExecuteAsync(new UpdateProjectStatusRequest(
            ProjectId: "prj_status",
            StatusKey: "ready_to_deliver"));

        Assert.Equal(2, result.RowsAffected);
        Assert.Equal(("prj_status", "ready_to_deliver"), projectStore.LastStatusUpdate);
    }

    [Fact]
    public async Task GetDeliveryEmailPreviewData_ReturnsProjectAndClient()
    {
        var projectStore = new FakeProjectStore
        {
            Project = new ProjectInfo(
                ProjectId: "prj_email",
                ProjectCode: "MGF25-0003",
                ProjectName: "Delivery Project",
                StatusKey: "active",
                DataProfile: "real",
                MetadataJson: "{}",
                ClientId: "cli_123"),
            ClientName = "Client Name"
        };
        var useCase = new GetDeliveryEmailPreviewDataUseCase(projectStore);

        var result = await useCase.ExecuteAsync(new GetDeliveryEmailPreviewDataRequest("prj_email"));

        Assert.NotNull(result);
        Assert.Equal("Client Name", result!.Project.ClientName);
    }

    [Fact]
    public async Task CreateTestProject_ReturnsExisting_WhenFound()
    {
        var projectStore = new FakeProjectStore
        {
            ExistingTestProject = new TestProjectInfo("prj_existing", "MGF25-0004", "Existing", "cli_existing")
        };
        var useCase = new CreateTestProjectUseCase(projectStore);

        var result = await useCase.ExecuteAsync(new UseCaseCreateTestProjectRequest(
            TestKey: "bootstrap_test",
            ClientName: "Client",
            ProjectName: "Project",
            EditorFirstName: "Test",
            EditorLastName: "Editor",
            EditorInitials: "TE",
            ForceNew: false));

        Assert.False(result.Created);
        Assert.NotNull(result.ExistingProject);
        Assert.Null(projectStore.CreateRequest);
    }

    [Fact]
    public async Task CreateTestProject_UsesStore_WhenForced()
    {
        var projectStore = new FakeProjectStore
        {
            ExistingTestProject = new TestProjectInfo("prj_existing", "MGF25-0004", "Existing", "cli_existing"),
            CreatedTestProject = new CreatedTestProject(
                ProjectId: "prj_new",
                ProjectCode: "MGF25-0005",
                ProjectName: "New Project",
                ClientId: "cli_new",
                PersonId: "per_new",
                EditorInitials: "TE")
        };
        var useCase = new CreateTestProjectUseCase(projectStore);

        var result = await useCase.ExecuteAsync(new UseCaseCreateTestProjectRequest(
            TestKey: "bootstrap_test",
            ClientName: "Client",
            ProjectName: "Project",
            EditorFirstName: "Test",
            EditorLastName: "Editor",
            EditorInitials: "TE",
            ForceNew: true));

        Assert.True(result.Created);
        Assert.Equal("prj_new", result.CreatedProject?.ProjectId);
        Assert.NotNull(projectStore.CreateRequest);
        Assert.Contains("\"test_key\":\"bootstrap_test\"", projectStore.CreateRequest!.MetadataJson);
    }

    private sealed class FakeJobStore : IJobOpsStore
    {
        public IReadOnlyList<JobSummary> Jobs { get; set; } = Array.Empty<JobSummary>();
        public List<(string JobTypeKey, string EntityTypeKey, string EntityKey)> JobRequests { get; } = new();

        public Task EnsureJobTypeAsync(string jobTypeKey, string displayName, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<ExistingJob?> FindExistingJobAsync(
            string jobTypeKey,
            string entityTypeKey,
            string entityKey,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<ExistingJob?>(null);
        }

        public Task EnqueueJobAsync(JobEnqueueRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Not used in this test.");
        }

        public Task EnqueueRetryableJobAsync(RetryableJobEnqueueRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Not used in this test.");
        }

        public Task<IReadOnlyList<JobSummary>> GetJobsAsync(
            string jobTypeKey,
            string entityTypeKey,
            string entityKey,
            CancellationToken cancellationToken = default)
        {
            JobRequests.Add((jobTypeKey, entityTypeKey, entityKey));
            return Task.FromResult(Jobs);
        }

        public Task<IReadOnlyList<RootIntegrityJobSummary>> GetRootIntegrityJobsAsync(
            string entityKey,
            int limit,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Not used in this test.");
        }

        public Task<int> ResetJobsAsync(string projectId, string jobTypeKey, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Not used in this test.");
        }

        public Task<int> CountStaleRunningJobsAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Not used in this test.");
        }

        public Task<int> RequeueStaleRunningJobsAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Not used in this test.");
        }
    }

    private sealed class FakeProjectStore : IProjectOpsStore
    {
        public ProjectInfo? Project { get; set; }
        public IReadOnlyList<ProjectStorageRootInfo> Roots { get; set; } = Array.Empty<ProjectStorageRootInfo>();
        public IReadOnlyList<ProjectListItem> Projects { get; set; } = Array.Empty<ProjectListItem>();
        public int UpdateStatusRows { get; set; }
        public string? ClientName { get; set; }
        public TestProjectInfo? ExistingTestProject { get; set; }
        public CreatedTestProject? CreatedTestProject { get; set; }
        public StoreCreateTestProjectRequest? CreateRequest { get; private set; }
        public int? ListLimit { get; private set; }
        public (string ProjectId, string StatusKey)? LastStatusUpdate { get; private set; }

        public Task<ProjectInfo?> GetProjectAsync(string projectId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Project);
        }

        public Task<IReadOnlyList<ProjectStorageRootInfo>> GetProjectStorageRootsAsync(
            string projectId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Roots);
        }

        public Task<IReadOnlyList<ProjectListItem>> ListProjectsAsync(
            int limit,
            CancellationToken cancellationToken = default)
        {
            ListLimit = limit;
            return Task.FromResult(Projects);
        }

        public Task<int> UpdateProjectStatusAsync(
            string projectId,
            string statusKey,
            CancellationToken cancellationToken = default)
        {
            LastStatusUpdate = (projectId, statusKey);
            return Task.FromResult(UpdateStatusRows);
        }

        public Task<string?> GetClientNameAsync(
            string clientId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ClientName);
        }

        public Task<TestProjectInfo?> FindTestProjectAsync(
            string testKey,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ExistingTestProject);
        }

        public Task<CreatedTestProject> CreateTestProjectAsync(
            StoreCreateTestProjectRequest request,
            CancellationToken cancellationToken = default)
        {
            CreateRequest = request;
            return Task.FromResult(CreatedTestProject ?? new CreatedTestProject(
                ProjectId: request.ProjectId,
                ProjectCode: "MGF25-0000",
                ProjectName: request.ProjectName,
                ClientId: request.ClientId,
                PersonId: request.PersonId,
                EditorInitials: request.EditorInitials));
        }
    }
}
