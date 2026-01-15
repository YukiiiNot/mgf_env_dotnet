namespace MGF.UseCases.Operations.Projects.CreateTestProject;

public interface ICreateTestProjectUseCase
{
    Task<CreateTestProjectResult> ExecuteAsync(
        CreateTestProjectRequest request,
        CancellationToken cancellationToken = default);
}
