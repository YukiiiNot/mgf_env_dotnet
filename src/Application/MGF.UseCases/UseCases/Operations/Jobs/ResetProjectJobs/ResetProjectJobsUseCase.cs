namespace MGF.UseCases.Operations.Jobs.ResetProjectJobs;

using MGF.Contracts.Abstractions.Operations.Jobs;

public sealed class ResetProjectJobsUseCase : IResetProjectJobsUseCase
{
    private readonly IJobOpsStore jobStore;

    public ResetProjectJobsUseCase(IJobOpsStore jobStore)
    {
        this.jobStore = jobStore;
    }

    public async Task<ResetProjectJobsResult> ExecuteAsync(
        ResetProjectJobsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var rows = await jobStore.ResetJobsAsync(
            request.ProjectId,
            request.JobTypeKey,
            cancellationToken);

        return new ResetProjectJobsResult(rows);
    }
}
