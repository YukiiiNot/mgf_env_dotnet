namespace MGF.UseCases.Operations.Jobs.EnqueueProjectBootstrapJob;

public sealed record EnqueueProjectBootstrapJobRequest(
    string ProjectId,
    IReadOnlyList<string> EditorInitials,
    bool VerifyDomainRoots,
    bool CreateDomainRoots,
    bool ProvisionProjectContainers,
    bool AllowRepair,
    bool ForceSandbox,
    bool AllowNonReal,
    bool Force,
    bool TestMode,
    bool AllowTestCleanup);

public sealed record EnqueueProjectBootstrapJobResult(
    bool Enqueued,
    string? Reason,
    string? JobId,
    string? PayloadJson);
