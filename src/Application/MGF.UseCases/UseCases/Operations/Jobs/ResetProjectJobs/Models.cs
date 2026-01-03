namespace MGF.UseCases.Operations.Jobs.ResetProjectJobs;

public sealed record ResetProjectJobsRequest(
    string ProjectId,
    string JobTypeKey);

public sealed record ResetProjectJobsResult(
    int RowsAffected);
