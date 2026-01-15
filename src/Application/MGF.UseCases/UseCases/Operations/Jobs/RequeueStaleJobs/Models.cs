namespace MGF.UseCases.Operations.Jobs.RequeueStaleJobs;

public sealed record RequeueStaleJobsRequest(
    bool DryRun);

public sealed record RequeueStaleJobsResult(
    bool WasDryRun,
    int Count);
