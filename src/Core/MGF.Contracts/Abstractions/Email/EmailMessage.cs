namespace MGF.Contracts.Abstractions.Email;

public sealed record EmailMessage(
    string FromAddress,
    string? FromName,
    IReadOnlyList<string> To,
    string Subject,
    string BodyText,
    string? HtmlBody,
    string TemplateVersion,
    string? ReplyTo,
    string ProfileKey = EmailProfiles.Deliveries);

public sealed record EmailSendResult(
    string Status,
    string Provider,
    string FromAddress,
    IReadOnlyList<string> To,
    string Subject,
    DateTimeOffset? SentAtUtc,
    string? ProviderMessageId,
    string? Error,
    string TemplateVersion,
    string? ReplyTo);
