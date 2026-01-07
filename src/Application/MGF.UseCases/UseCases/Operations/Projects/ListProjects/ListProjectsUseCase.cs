namespace MGF.UseCases.Operations.Projects.ListProjects;

using MGF.Contracts.Abstractions.Operations.Projects;

public sealed class ListProjectsUseCase : IListProjectsUseCase
{
    private readonly IProjectOpsStore projectStore;

    public ListProjectsUseCase(IProjectOpsStore projectStore)
    {
        this.projectStore = projectStore;
    }

    public async Task<ListProjectsResult> ExecuteAsync(
        ListProjectsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var projects = await projectStore.ListProjectsAsync(request.Limit, cancellationToken);
        return new ListProjectsResult(projects);
    }
}
