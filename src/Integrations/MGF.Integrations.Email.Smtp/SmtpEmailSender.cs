namespace MGF.Integrations.Email.Smtp;

using System.Text;
using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using Microsoft.Extensions.Configuration;
using MGF.Contracts.Abstractions.Email;

public sealed class SmtpEmailSender : IEmailSender
{
    private readonly IConfiguration configuration;

    public SmtpEmailSender(IConfiguration configuration)
    {
        this.configuration = configuration;
    }

    public async Task<EmailSendResult> SendAsync(
        EmailMessage request,
        CancellationToken cancellationToken)
    {
        var enabled = configuration.GetValue("Integrations:Email:Enabled", false);
        if (!enabled)
        {
            return Failed(request, "Email sending disabled (Integrations:Email:Enabled=false).");
        }

        var profile = EmailProfileResolver.Resolve(configuration, request.ProfileKey);
        if (!EmailProfileResolver.IsAllowedFrom(profile, request.FromAddress))
        {
            var allowed = EmailProfileResolver.AllowedFromDisplay(profile);
            return Failed(request, $"SMTP fromAddress must be {allowed}.");
        }

        var host = configuration["Integrations:Email:Smtp:Host"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(host))
        {
            return Failed(request, "SMTP host not configured (Integrations:Email:Smtp:Host).");
        }

        var port = configuration.GetValue("Integrations:Email:Smtp:Port", 587);
        var useSsl = configuration.GetValue("Integrations:Email:Smtp:UseSsl", true);
        var user = configuration["Integrations:Email:Smtp:User"];
        var password = configuration["Integrations:Email:Smtp:Password"];

        cancellationToken.ThrowIfCancellationRequested();

        using var message = BuildMessage(request);

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = useSsl,
            UseDefaultCredentials = false
        };

        if (!string.IsNullOrWhiteSpace(user) && !string.IsNullOrWhiteSpace(password))
        {
            client.Credentials = new NetworkCredential(user, password);
        }

        try
        {
            await client.SendMailAsync(message);
            return new EmailSendResult(
                Status: "sent",
                Provider: "smtp",
                FromAddress: request.FromAddress,
                To: request.To,
                Subject: request.Subject,
                SentAtUtc: DateTimeOffset.UtcNow,
                ProviderMessageId: null,
                Error: null,
                TemplateVersion: request.TemplateVersion,
                ReplyTo: request.ReplyTo
            );
        }
        catch (Exception ex)
        {
            return Failed(request, $"SMTP send failed: {ex.Message}");
        }
    }

    private static EmailSendResult Failed(EmailMessage request, string error)
    {
        return new EmailSendResult(
            Status: "failed",
            Provider: "smtp",
            FromAddress: request.FromAddress,
            To: request.To,
            Subject: request.Subject,
            SentAtUtc: null,
            ProviderMessageId: null,
            Error: error,
            TemplateVersion: request.TemplateVersion,
            ReplyTo: request.ReplyTo
        );
    }

    public static MailMessage BuildMessage(EmailMessage request)
    {
        var message = new MailMessage
        {
            From = string.IsNullOrWhiteSpace(request.FromName)
                ? new MailAddress(request.FromAddress)
                : new MailAddress(request.FromAddress, request.FromName),
            Subject = request.Subject
        };

        foreach (var recipient in request.To)
        {
            if (!string.IsNullOrWhiteSpace(recipient))
            {
                message.To.Add(recipient);
            }
        }

        if (!string.IsNullOrWhiteSpace(request.ReplyTo))
        {
            message.ReplyToList.Add(new MailAddress(request.ReplyTo));
        }

        if (!string.IsNullOrWhiteSpace(request.HtmlBody))
        {
            message.Body = string.Empty;
            message.IsBodyHtml = false;
            message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
                request.BodyText,
                Encoding.UTF8,
                MediaTypeNames.Text.Plain));
            message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
                request.HtmlBody,
                Encoding.UTF8,
                MediaTypeNames.Text.Html));
        }
        else
        {
            message.Body = request.BodyText;
            message.IsBodyHtml = false;
        }

        return message;
    }
}
