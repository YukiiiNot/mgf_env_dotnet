namespace MGF.Worker.Email;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MGF.Contracts.Abstractions.Email;
using MGF.Email.Models;
using MGF.Email.Registry;

public sealed class EmailService
{
    private readonly IConfiguration configuration;
    private readonly EmailComposerRegistry registry;
    private readonly IEmailSender sender;
    private readonly ILogger? logger;

    public EmailService(
        IConfiguration configuration,
        IEmailSender sender,
        EmailComposerRegistry? registry = null,
        ILogger? logger = null)
    {
        this.configuration = configuration;
        this.registry = registry ?? EmailComposerRegistry.CreateDefault();
        this.sender = sender;
        this.logger = logger;
    }

    public Task<EmailSendResult> SendAsync(
        EmailKind kind,
        object context,
        CancellationToken cancellationToken)
    {
        var composer = registry.Get(kind);
        var message = composer.Build(context);
        logger?.LogInformation("MGF.Worker.Email: sending {Kind} email to {RecipientCount} recipients", kind, message.To.Count);
        return sender.SendAsync(message, cancellationToken);
    }
}
