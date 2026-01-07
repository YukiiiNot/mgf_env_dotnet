namespace MGF.UseCases.Operations.Jobs.EnqueueProjectDeliveryEmailJob;

public sealed record EnqueueProjectDeliveryEmailJobRequest(
    string ProjectId,
    IReadOnlyList<string> EditorInitials,
    IReadOnlyList<string> ToEmails,
    string? ReplyToEmail);

public sealed record EnqueueProjectDeliveryEmailJobResult(
    bool Enqueued,
    string? Reason,
    string? JobId,
    string? PayloadJson);
