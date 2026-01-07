namespace MGF.UseCases.Operations.Projects.GetProjectSnapshot;

using MGF.Contracts.Abstractions.Operations.Jobs;
using MGF.Contracts.Abstractions.Operations.Projects;

public sealed record GetProjectSnapshotRequest(
    string ProjectId,
    bool IncludeStorageRoots);

public sealed record GetProjectSnapshotResult(
    ProjectInfo Project,
    IReadOnlyList<ProjectStorageRootInfo> StorageRoots,
    IReadOnlyList<JobSummary> BootstrapJobs,
    IReadOnlyList<JobSummary> ArchiveJobs,
    IReadOnlyList<JobSummary> DeliveryJobs);
