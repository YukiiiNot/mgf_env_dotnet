namespace MGF.Contracts.Abstractions.Email;

public interface IEmailSender
{
    Task<EmailSendResult> SendAsync(EmailMessage request, CancellationToken cancellationToken);
}
