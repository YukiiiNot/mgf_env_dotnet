namespace MGF.UseCases.Operations.Projects.CreateTestProject;

using System.Text.Json;
using MGF.Contracts.Abstractions.Operations.Projects;
using MGF.Domain.Entities;

public sealed class CreateTestProjectUseCase : ICreateTestProjectUseCase
{
    private readonly IProjectOpsStore projectStore;

    public CreateTestProjectUseCase(IProjectOpsStore projectStore)
    {
        this.projectStore = projectStore;
    }

    public async Task<CreateTestProjectResult> ExecuteAsync(
        CreateTestProjectRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (!request.ForceNew)
        {
            var existing = await projectStore.FindTestProjectAsync(request.TestKey, cancellationToken);
            if (existing is not null)
            {
                return new CreateTestProjectResult(
                    Created: false,
                    ExistingProject: existing,
                    CreatedProject: null);
            }
        }

        var metadataJson = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["test_key"] = request.TestKey,
            ["test_type"] = "bootstrap",
            ["created_by"] = "MGF.ProjectBootstrapCli",
            ["created_at"] = DateTimeOffset.UtcNow.ToString("O")
        });

        var created = await projectStore.CreateTestProjectAsync(
            new MGF.Contracts.Abstractions.Operations.Projects.CreateTestProjectRequest(
                TestKey: request.TestKey,
                ClientName: request.ClientName,
                ProjectName: request.ProjectName,
                EditorFirstName: request.EditorFirstName,
                EditorLastName: request.EditorLastName,
                EditorInitials: request.EditorInitials,
                MetadataJson: metadataJson,
                PersonId: EntityIds.NewPersonId(),
                ClientId: EntityIds.NewClientId(),
                ProjectId: EntityIds.NewProjectId(),
                ProjectMemberId: EntityIds.NewWithPrefix("prm")),
            cancellationToken);

        return new CreateTestProjectResult(
            Created: true,
            ExistingProject: null,
            CreatedProject: created);
    }
}
