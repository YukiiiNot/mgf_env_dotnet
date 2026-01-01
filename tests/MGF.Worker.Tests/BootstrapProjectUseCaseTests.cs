using System.Text.Json;
using MGF.Contracts.Abstractions;
using MGF.Domain.Entities;
using MGF.UseCases.ProjectBootstrap;

namespace MGF.Worker.Tests;

public sealed class BootstrapProjectUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_OrdersStoreWritesAroundProvisioning()
    {
        var projectId = "proj_123";
        var clientId = "client_123";
        var metadata = JsonDocument.Parse("{}").RootElement.Clone();
        var project = new Project(
            projectId: projectId,
            projectCode: "P-001",
            clientId: clientId,
            name: "Demo",
            statusKey: "ready_to_provision",
            phaseKey: "planning",
            dataProfile: "real",
            metadata: metadata);

        var client = new Client(clientId, "Client Demo");

        var store = new FakeBootstrapStore();
        var gateway = new FakeGateway();
        var useCase = new BootstrapProjectUseCase(
            new FakeProjectRepository(project),
            new FakeClientRepository(client),
            store,
            gateway);

        var request = new BootstrapProjectRequest(
            JobId: "job_123",
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
            AllowTestCleanup: false
        );

        _ = await useCase.ExecuteAsync(request);

        Assert.Equal(
            new[]
            {
                "status:provisioning",
                "upsert:dropbox",
                "append",
                "status:active"
            },
            store.Calls);
    }

    private sealed class FakeProjectRepository : IProjectRepository
    {
        private readonly Project project;

        public FakeProjectRepository(Project project)
        {
            this.project = project;
        }

        public Task<Project?> GetByIdAsync(string projectId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(projectId == project.ProjectId ? project : null);
        }

        public Task SaveAsync(Project project, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeClientRepository : IClientRepository
    {
        private readonly Client client;

        public FakeClientRepository(Client client)
        {
            this.client = client;
        }

        public Task<Client?> GetByIdAsync(string clientId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(clientId == client.ClientId ? client : null);
        }

        public Task SaveAsync(Client client, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
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
        public Task<ProjectBootstrapExecutionResult> ExecuteAsync(
            ProjectBootstrapContext context,
            BootstrapProjectRequest request,
            CancellationToken cancellationToken = default)
        {
            var summary = new ProvisioningSummary(
                Mode: "verify",
                TemplateKey: "template",
                TargetRoot: "C:\\root",
                ManifestPath: "C:\\root\\manifest.json",
                Success: true,
                MissingRequired: Array.Empty<string>(),
                Errors: Array.Empty<string>(),
                Warnings: Array.Empty<string>()
            );

            var domain = new ProjectBootstrapDomainResult(
                DomainKey: "dropbox",
                RootPath: "C:\\root",
                RootState: "root_verified",
                DomainRootProvisioning: summary,
                ProjectContainerProvisioning: summary,
                Notes: Array.Empty<string>()
            );

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
                LastError: null
            );

            var candidate = new ProjectBootstrapStorageRootCandidate(
                DomainKey: "dropbox",
                StorageProviderKey: "dropbox",
                RootKey: "project_container",
                FolderRelpath: "02_Projects_Active\\Demo"
            );

            return Task.FromResult(new ProjectBootstrapExecutionResult(runResult, new[] { candidate }, null));
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
}
