namespace MGF.UseCases.Operations.Jobs.RequeueStaleJobs;

using MGF.Contracts.Abstractions.Operations.Jobs;

public sealed class RequeueStaleJobsUseCase : IRequeueStaleJobsUseCase
{
    private readonly IJobOpsStore jobStore;

    public RequeueStaleJobsUseCase(IJobOpsStore jobStore)
    {
        this.jobStore = jobStore;
    }

    public async Task<RequeueStaleJobsResult> ExecuteAsync(
        RequeueStaleJobsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (request.DryRun)
        {
            var count = await jobStore.CountStaleRunningJobsAsync(cancellationToken);
            return new RequeueStaleJobsResult(WasDryRun: true, Count: count);
        }

        var updated = await jobStore.RequeueStaleRunningJobsAsync(cancellationToken);
        return new RequeueStaleJobsResult(WasDryRun: false, Count: updated);
    }
}
