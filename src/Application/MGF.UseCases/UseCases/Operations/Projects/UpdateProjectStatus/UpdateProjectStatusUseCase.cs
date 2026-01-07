namespace MGF.UseCases.Operations.Projects.UpdateProjectStatus;

using MGF.Contracts.Abstractions.Operations.Projects;

public sealed class UpdateProjectStatusUseCase : IUpdateProjectStatusUseCase
{
    private readonly IProjectOpsStore projectStore;

    public UpdateProjectStatusUseCase(IProjectOpsStore projectStore)
    {
        this.projectStore = projectStore;
    }

    public async Task<UpdateProjectStatusResult> ExecuteAsync(
        UpdateProjectStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var rows = await projectStore.UpdateProjectStatusAsync(
            request.ProjectId,
            request.StatusKey,
            cancellationToken);

        return new UpdateProjectStatusResult(rows);
    }
}
