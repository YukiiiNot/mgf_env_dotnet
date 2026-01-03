namespace MGF.Integrations.Email.Preview;

using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MGF.Contracts.Abstractions.Email;
using MGF.Email.Registry;

public sealed class PreviewEmailSender : IEmailSender
{
    private readonly IConfiguration configuration;
    private readonly ILogger<PreviewEmailSender>? logger;

    public PreviewEmailSender(IConfiguration configuration, ILogger<PreviewEmailSender>? logger = null)
    {
        this.configuration = configuration;
        this.logger = logger;
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
            return Failed(request, $"Preview fromAddress must be {allowed}.");
        }

        var outputRoot = configuration["Integrations:Email:Preview:OutputDir"];
        if (string.IsNullOrWhiteSpace(outputRoot))
        {
            outputRoot = Path.Combine(Directory.GetCurrentDirectory(), "runtime", "email_preview");
        }

        var outputDir = Path.Combine(outputRoot, DateTimeOffset.UtcNow.ToString("yyyyMMdd_HHmmss_fff"));
        Directory.CreateDirectory(outputDir);

        var textPath = Path.Combine(outputDir, "message.txt");
        var htmlPath = Path.Combine(outputDir, "message.html");
        var jsonPath = Path.Combine(outputDir, "message.json");

        await File.WriteAllTextAsync(textPath, request.BodyText, cancellationToken);
        await File.WriteAllTextAsync(htmlPath, request.HtmlBody ?? request.BodyText, cancellationToken);

        var json = JsonSerializer.Serialize(new
        {
            provider = "preview",
            profileKey = request.ProfileKey,
            fromAddress = request.FromAddress,
            fromName = request.FromName,
            to = request.To,
            replyTo = request.ReplyTo,
            subject = request.Subject,
            templateVersion = request.TemplateVersion,
            outputDir
        }, new JsonSerializerOptions { WriteIndented = true });

        await File.WriteAllTextAsync(jsonPath, json, cancellationToken);
        logger?.LogInformation("Email preview written to {PreviewDir}", outputDir);

        return new EmailSendResult(
            Status: "sent",
            Provider: "preview",
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

    private static EmailSendResult Failed(EmailMessage request, string error)
    {
        return new EmailSendResult(
            Status: "failed",
            Provider: "preview",
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
}
