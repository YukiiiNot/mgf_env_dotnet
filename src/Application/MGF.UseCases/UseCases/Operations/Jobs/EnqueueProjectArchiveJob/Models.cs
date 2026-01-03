namespace MGF.UseCases.Operations.Jobs.EnqueueProjectArchiveJob;

public sealed record EnqueueProjectArchiveJobRequest(
    string ProjectId,
    IReadOnlyList<string> EditorInitials,
    bool TestMode,
    bool AllowTestCleanup,
    bool AllowNonReal,
    bool Force);

public sealed record EnqueueProjectArchiveJobResult(
    bool Enqueued,
    string? Reason,
    string? JobId,
    string? PayloadJson);
