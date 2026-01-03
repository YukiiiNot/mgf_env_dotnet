namespace MGF.UseCases.Operations.Jobs.EnqueueRootIntegrityJob;

public sealed record EnqueueRootIntegrityJobRequest(
    string ProviderKey,
    string RootKey,
    string Mode,
    bool DryRun,
    string? QuarantineRelpath,
    int? MaxItems,
    long? MaxBytes);

public sealed record EnqueueRootIntegrityJobResult(
    string JobId,
    string PayloadJson);
