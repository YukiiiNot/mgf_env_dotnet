namespace MGF.Worker.Email.Composition;

using MGF.Worker.Email.Models;
using MGF.Worker.ProjectDelivery;

public sealed class DeliveryReadyEmailComposer : IEmailComposer
{
    private static readonly EmailTemplateRenderer Renderer = EmailTemplateRenderer.CreateDefault();

    public EmailKind Kind => EmailKind.DeliveryReady;

    public DeliveryEmailRequest Build(object context)
    {
        if (context is not DeliveryReadyEmailContext delivery)
        {
            throw new InvalidOperationException("DeliveryReadyEmailComposer requires DeliveryReadyEmailContext.");
        }

        var subject = ProjectDeliverer.BuildDeliverySubject(delivery.Tokens);
        var textBody = Renderer.RenderText("delivery_ready.txt", delivery);
        var htmlBody = Renderer.RenderHtml("delivery_ready.html", delivery);
        return new DeliveryEmailRequest(
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
}
