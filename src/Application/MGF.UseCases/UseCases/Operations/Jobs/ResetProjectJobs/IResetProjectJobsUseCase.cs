namespace MGF.UseCases.Operations.Jobs.ResetProjectJobs;

public interface IResetProjectJobsUseCase
{
    Task<ResetProjectJobsResult> ExecuteAsync(
        ResetProjectJobsRequest request,
        CancellationToken cancellationToken = default);
}
