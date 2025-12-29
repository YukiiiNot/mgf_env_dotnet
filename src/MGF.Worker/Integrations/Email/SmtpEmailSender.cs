namespace MGF.Worker.Integrations.Email;

using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;

internal sealed class SmtpEmailSender : IEmailSender
{
    private readonly IConfiguration configuration;

    public SmtpEmailSender(IConfiguration configuration)
    {
        this.configuration = configuration;
    }

    public async Task<DeliveryEmailResult> SendAsync(
        DeliveryEmailRequest request,
        CancellationToken cancellationToken)
    {
        var enabled = configuration.GetValue("Integrations:Email:Enabled", false);
        if (!enabled)
        {
            return Failed(request, "Email sending disabled (Integrations:Email:Enabled=false).");
        }

        if (!IsAllowedFromAddress(request.FromAddress))
        {
            return Failed(request, "SMTP fromAddress must be deliveries@mgfilms.pro or info@mgfilms.pro.");
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

        using var message = new MailMessage
        {
            From = new MailAddress(request.FromAddress),
            Subject = request.Subject,
            Body = request.BodyText,
            IsBodyHtml = false
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
            return new DeliveryEmailResult(
                Status: "sent",
                Provider: "smtp",
                FromAddress: request.FromAddress,
                To: request.To,
                Subject: request.Subject,
                SentAtUtc: DateTimeOffset.UtcNow,
                ProviderMessageId: null,
                Error: null,
                ReplyTo: request.ReplyTo
            );
        }
        catch (Exception ex)
        {
            return Failed(request, $"SMTP send failed: {ex.Message}");
        }
    }

    private static DeliveryEmailResult Failed(DeliveryEmailRequest request, string error)
    {
        return new DeliveryEmailResult(
            Status: "failed",
            Provider: "smtp",
            FromAddress: request.FromAddress,
            To: request.To,
            Subject: request.Subject,
            SentAtUtc: null,
            ProviderMessageId: null,
            Error: error,
            ReplyTo: request.ReplyTo
        );
    }

    private static bool IsAllowedFromAddress(string value)
    {
        return string.Equals(value, "deliveries@mgfilms.pro", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "info@mgfilms.pro", StringComparison.OrdinalIgnoreCase);
    }
}
