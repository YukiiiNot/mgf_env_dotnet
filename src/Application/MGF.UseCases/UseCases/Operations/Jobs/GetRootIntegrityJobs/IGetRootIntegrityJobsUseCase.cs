namespace MGF.UseCases.Operations.Jobs.GetRootIntegrityJobs;

public interface IGetRootIntegrityJobsUseCase
{
    Task<GetRootIntegrityJobsResult> ExecuteAsync(
        GetRootIntegrityJobsRequest request,
        CancellationToken cancellationToken = default);
}
