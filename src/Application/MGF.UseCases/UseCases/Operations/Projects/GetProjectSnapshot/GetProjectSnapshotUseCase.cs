namespace MGF.UseCases.Operations.Projects.GetProjectSnapshot;

using MGF.Contracts.Abstractions.Operations.Jobs;
using MGF.Contracts.Abstractions.Operations.Projects;

public sealed class GetProjectSnapshotUseCase : IGetProjectSnapshotUseCase
{
    private readonly IJobOpsStore jobStore;
    private readonly IProjectOpsStore projectStore;

    public GetProjectSnapshotUseCase(IJobOpsStore jobStore, IProjectOpsStore projectStore)
    {
        this.jobStore = jobStore;
        this.projectStore = projectStore;
    }

    public async Task<GetProjectSnapshotResult?> ExecuteAsync(
        GetProjectSnapshotRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var project = await projectStore.GetProjectAsync(request.ProjectId, cancellationToken);
        if (project is null)
        {
            return null;
        }

        var roots = request.IncludeStorageRoots
            ? await projectStore.GetProjectStorageRootsAsync(request.ProjectId, cancellationToken)
            : Array.Empty<ProjectStorageRootInfo>();

        var bootstrapJobs = await jobStore.GetJobsAsync(
            "project.bootstrap",
            "project",
            request.ProjectId,
            cancellationToken);

        var archiveJobs = await jobStore.GetJobsAsync(
            "project.archive",
            "project",
            request.ProjectId,
            cancellationToken);

        var deliveryJobs = await jobStore.GetJobsAsync(
            "project.delivery",
            "project",
            request.ProjectId,
            cancellationToken);

        return new GetProjectSnapshotResult(
            project,
            roots,
            bootstrapJobs,
            archiveJobs,
            deliveryJobs);
    }
}
