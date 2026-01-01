namespace MGF.Worker.Email;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MGF.Contracts.Abstractions;
using MGF.UseCases.DeliveryEmail.SendDeliveryEmail;
using MGF.Worker.Email.Composition;
using MGF.Worker.Email.Models;
using MGF.Worker.Email.Registry;
using MGF.Worker.Email.Sending;
using MGF.Worker.ProjectDelivery;

public sealed class WorkerEmailGateway : IWorkerEmailGateway
{
    private const string DefaultReplyToAddress = "info@mgfilms.pro";
    private const string DefaultFromAddress = "deliveries@mgfilms.pro";
    private const string DefaultSubject = "MGF Delivery";
    private const string DefaultTemplateVersion = "v1-html";

    private readonly IConfiguration configuration;
    private readonly ILogger<WorkerEmailGateway> logger;
    private readonly EmailService emailService;

    public WorkerEmailGateway(IConfiguration configuration, ILogger<WorkerEmailGateway> logger)
    {
        this.configuration = configuration;
        this.logger = logger;
        emailService = new EmailService(configuration, logger: logger);
    }

    public async Task<DeliveryEmailAudit> SendDeliveryReadyAsync(
        WorkerDeliveryEmailRequest request,
        CancellationToken cancellationToken = default)
    {
        var profile = EmailProfileResolver.Resolve(configuration, EmailProfiles.Deliveries);
        var replyTo = profile.DefaultReplyTo ?? DefaultReplyToAddress;

        if (!string.IsNullOrWhiteSpace(request.ObservedReplyTo)
            && !string.Equals(request.ObservedReplyTo, replyTo, StringComparison.OrdinalIgnoreCase))
        {
            return BuildFailure(
                request.Recipients,
                $"Delivery email reply-to does not match canonical reply-to ({replyTo}).");
        }

        var logoUrl = profile.LogoUrl;
        var fromName = profile.DefaultFromName ?? "MG Films";
        var context = new DeliveryReadyEmailContext(
            request.Tokens,
            request.ShareUrl,
            request.VersionLabel,
            request.RetentionUntilUtc,
            request.Files.Select(ToSummary).ToArray(),
            request.Recipients,
            replyTo,
            logoUrl,
            fromName);

        DeliveryEmailResult result;
        if (request.Mode == DeliveryEmailMode.PreviewOnly)
        {
            var previewService = new EmailService(configuration, sender: new PreviewEmailSender(), logger: logger);
            result = await previewService.SendAsync(EmailKind.DeliveryReady, context, cancellationToken);
        }
        else
        {
            result = await emailService.SendAsync(EmailKind.DeliveryReady, context, cancellationToken);
        }

        return ToAudit(result);
    }

    private static DeliveryEmailAudit BuildFailure(IReadOnlyList<string> recipients, string error)
    {
        return new DeliveryEmailAudit(
            Status: "failed",
            Provider: "email",
            FromAddress: DefaultFromAddress,
            To: recipients,
            Subject: DefaultSubject,
            SentAtUtc: null,
            ProviderMessageId: null,
            Error: error,
            TemplateVersion: DefaultTemplateVersion,
            ReplyTo: null);
    }

    private static DeliveryEmailAudit ToAudit(DeliveryEmailResult result)
    {
        return new DeliveryEmailAudit(
            result.Status,
            result.Provider,
            result.FromAddress,
            result.To,
            result.Subject,
            result.SentAtUtc,
            result.ProviderMessageId,
            result.Error,
            result.TemplateVersion,
            result.ReplyTo);
    }

    private static DeliveryFileSummary ToSummary(DeliveryEmailFile file)
    {
        return new DeliveryFileSummary(file.RelativePath, file.SizeBytes, file.LastWriteTimeUtc);
    }

    private sealed class PreviewEmailSender : IEmailSender
    {
        public Task<DeliveryEmailResult> SendAsync(
            DeliveryEmailRequest request,
            CancellationToken cancellationToken)
        {
            var result = new DeliveryEmailResult(
                Status: "preview",
                Provider: "preview",
                FromAddress: request.FromAddress,
                To: request.To,
                Subject: request.Subject,
                SentAtUtc: null,
                ProviderMessageId: null,
                Error: null,
                TemplateVersion: request.TemplateVersion,
                ReplyTo: request.ReplyTo);

            return Task.FromResult(result);
        }
    }
}
