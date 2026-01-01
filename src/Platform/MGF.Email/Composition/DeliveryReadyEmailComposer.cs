namespace MGF.Email.Composition;

using MGF.Contracts.Abstractions.Email;
using MGF.Email.Models;
using MGF.Provisioning;

public sealed class DeliveryReadyEmailComposer : IEmailComposer
{
    private static readonly EmailTemplateRenderer Renderer = EmailTemplateRenderer.CreateDefault();

    public EmailKind Kind => EmailKind.DeliveryReady;

    public EmailMessage Build(object context)
    {
        if (context is not DeliveryReadyEmailContext delivery)
        {
            throw new InvalidOperationException("DeliveryReadyEmailComposer requires DeliveryReadyEmailContext.");
        }

        var subject = BuildSubject(delivery.Tokens);
        var textBody = Renderer.RenderText("delivery_ready.txt", delivery);
        var htmlBody = Renderer.RenderHtml("delivery_ready.html", delivery);
        return new EmailMessage(
            FromAddress: "deliveries@mgfilms.pro",
            FromName: delivery.FromName,
            To: delivery.Recipients,
            Subject: subject,
            BodyText: textBody,
            HtmlBody: htmlBody,
            TemplateVersion: "v1-html",
            ReplyTo: delivery.ReplyTo,
            ProfileKey: EmailProfiles.Deliveries);
    }

    private static string BuildSubject(ProvisioningTokens tokens)
    {
        var code = tokens.ProjectCode ?? "MGF";
        var name = tokens.ProjectName ?? "Delivery";
        return $"Your deliverables are ready \u2014 {code} {name}";
    }
}
