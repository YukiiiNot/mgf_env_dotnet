namespace MGF.UseCases.Operations.Projects.GetProjectSnapshot;

public interface IGetProjectSnapshotUseCase
{
    Task<GetProjectSnapshotResult?> ExecuteAsync(
        GetProjectSnapshotRequest request,
        CancellationToken cancellationToken = default);
}
