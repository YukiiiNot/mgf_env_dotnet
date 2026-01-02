namespace MGF.UseCases.Operations.Projects.ListProjects;

public interface IListProjectsUseCase
{
    Task<ListProjectsResult> ExecuteAsync(
        ListProjectsRequest request,
        CancellationToken cancellationToken = default);
}
