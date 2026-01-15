namespace MGF.UseCases.Operations.Jobs.RequeueStaleJobs;

public interface IRequeueStaleJobsUseCase
{
    Task<RequeueStaleJobsResult> ExecuteAsync(
        RequeueStaleJobsRequest request,
        CancellationToken cancellationToken = default);
}
