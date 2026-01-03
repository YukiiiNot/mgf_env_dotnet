namespace MGF.UseCases.Operations.Jobs.EnqueueProjectDeliveryJob;

public sealed record EnqueueProjectDeliveryJobRequest(
    string ProjectId,
    IReadOnlyList<string> EditorInitials,
    IReadOnlyList<string> ToEmails,
    string? ReplyToEmail,
    bool TestMode,
    bool AllowTestCleanup,
    bool AllowNonReal,
    bool Force,
    bool RefreshShareLink);

public sealed record EnqueueProjectDeliveryJobResult(
    bool Enqueued,
    string? Reason,
    string? JobId,
    string? PayloadJson);
