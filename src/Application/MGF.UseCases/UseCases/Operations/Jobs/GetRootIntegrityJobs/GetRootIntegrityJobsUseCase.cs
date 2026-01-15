namespace MGF.UseCases.Operations.Jobs.GetRootIntegrityJobs;

using MGF.Contracts.Abstractions.Operations.Jobs;

public sealed class GetRootIntegrityJobsUseCase : IGetRootIntegrityJobsUseCase
{
    private readonly IJobOpsStore jobStore;

    public GetRootIntegrityJobsUseCase(IJobOpsStore jobStore)
    {
        this.jobStore = jobStore;
    }

    public async Task<GetRootIntegrityJobsResult> ExecuteAsync(
        GetRootIntegrityJobsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var jobs = await jobStore.GetRootIntegrityJobsAsync(
            $"{request.ProviderKey}:{request.RootKey}",
            request.Limit,
            cancellationToken);

        return new GetRootIntegrityJobsResult(jobs);
    }
}
