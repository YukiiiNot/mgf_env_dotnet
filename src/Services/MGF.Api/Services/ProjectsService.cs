namespace MGF.Api.Services;

using MGF.UseCases.Projects.CreateProject;

public sealed class ProjectsService
{
    private readonly ICreateProjectUseCase createProjectUseCase;
    private readonly ILogger<ProjectsService> logger;

    public ProjectsService(
        ICreateProjectUseCase createProjectUseCase,
        ILogger<ProjectsService> logger)
    {
        this.createProjectUseCase = createProjectUseCase;
        this.logger = logger;
    }

    public sealed record CreateProjectRequest(string ClientId, string ProjectName, string EditorPersonId, string TemplateKey);

    public sealed record CreateProjectResponse(string ProjectId, string JobId);

    public async Task<CreateProjectResponse?> CreateProjectAsync(CreateProjectRequest request, CancellationToken cancellationToken)
    {
        var result = await createProjectUseCase.ExecuteAsync(
            new MGF.UseCases.Projects.CreateProject.CreateProjectRequest(
                request.ClientId,
                request.ProjectName,
                request.EditorPersonId,
                request.TemplateKey),
            cancellationToken);

        if (result is null)
        {
            return null;
        }

        logger.LogInformation("MGF.Api: created project {ProjectId} and enqueued job {JobId}", result.ProjectId, result.JobId);

        return new CreateProjectResponse(result.ProjectId, result.JobId);
    }
}


