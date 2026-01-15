namespace MGF.UseCases.Projects.CreateProject;

using System.Text.Json;
using MGF.Contracts.Abstractions.Projects;
using MGF.Domain.Entities;

public sealed class CreateProjectUseCase : ICreateProjectUseCase
{
    private readonly IProjectCreationStore projectStore;

    public CreateProjectUseCase(IProjectCreationStore projectStore)
    {
        this.projectStore = projectStore;
    }

    public async Task<CreateProjectResult?> ExecuteAsync(
        CreateProjectRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.ClientId))
        {
            return null;
        }
        if (string.IsNullOrWhiteSpace(request.ProjectName))
        {
            return null;
        }
        if (string.IsNullOrWhiteSpace(request.EditorPersonId))
        {
            return null;
        }
        if (string.IsNullOrWhiteSpace(request.TemplateKey))
        {
            return null;
        }

        var clientExists = await projectStore.ClientExistsAsync(request.ClientId, cancellationToken);
        if (!clientExists)
        {
            return null;
        }

        var editorExists = await projectStore.PersonExistsAsync(request.EditorPersonId, cancellationToken);
        if (!editorExists)
        {
            return null;
        }

        var isEditor = await projectStore.PersonHasRoleAsync(request.EditorPersonId, "editor", cancellationToken);
        if (!isEditor)
        {
            return null;
        }

        var roles = await projectStore.GetPersonRolesAsync(request.EditorPersonId, cancellationToken);
        var additionalRoles = roles
            .Where(role => role.Equals("producer", StringComparison.OrdinalIgnoreCase)
                || role.Equals("admin", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var projectId = EntityIds.NewProjectId();
        var jobId = EntityIds.NewWithPrefix("job");

        var payloadJson = JsonSerializer.Serialize(new
        {
            projectId,
            clientId = request.ClientId,
            templateKey = request.TemplateKey
        });

        await projectStore.CreateProjectAsync(
            new CreateProjectCommand(
                ProjectId: projectId,
                ClientId: request.ClientId,
                ProjectName: request.ProjectName,
                EditorPersonId: request.EditorPersonId,
                JobId: jobId,
                PayloadJson: payloadJson,
                AdditionalRoleKeys: additionalRoles),
            cancellationToken);

        return new CreateProjectResult(projectId, jobId);
    }
}
