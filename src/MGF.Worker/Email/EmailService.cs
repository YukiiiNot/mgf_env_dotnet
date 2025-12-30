namespace MGF.Worker.Email;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MGF.Worker.Email.Models;
using MGF.Worker.Email.Registry;
using MGF.Worker.Email.Sending;

public sealed class EmailService
{
    private readonly IConfiguration configuration;
    private readonly EmailComposerRegistry registry;
    private readonly IEmailSender sender;
    private readonly ILogger? logger;

    public EmailService(
        IConfiguration configuration,
        EmailComposerRegistry? registry = null,
        IEmailSender? sender = null,
        ILogger? logger = null)
    {
        this.configuration = configuration;
        this.registry = registry ?? EmailComposerRegistry.CreateDefault();
        this.sender = sender ?? EmailSenderFactory.Create(configuration, logger);
        this.logger = logger;
    }

    public Task<DeliveryEmailResult> SendAsync(
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
