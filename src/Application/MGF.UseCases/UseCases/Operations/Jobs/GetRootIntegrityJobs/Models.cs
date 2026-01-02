namespace MGF.UseCases.Operations.Jobs.GetRootIntegrityJobs;

using MGF.Contracts.Abstractions.Operations.Jobs;

public sealed record GetRootIntegrityJobsRequest(
    string ProviderKey,
    string RootKey,
    int Limit);

public sealed record GetRootIntegrityJobsResult(
    IReadOnlyList<RootIntegrityJobSummary> Jobs);
