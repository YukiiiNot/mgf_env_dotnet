namespace MGF.UseCases.Projects.CreateProject;

public interface ICreateProjectUseCase
{
    Task<CreateProjectResult?> ExecuteAsync(
        CreateProjectRequest request,
        CancellationToken cancellationToken = default);
}
