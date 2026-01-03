namespace MGF.UseCases.DeliveryEmail.RenderDeliveryEmailPreview;

using MGF.Contracts.Abstractions.Email;
using MGF.Email.Composition;
using MGF.Email.Models;
using MGF.Email.Registry;

public sealed class RenderDeliveryEmailPreviewUseCase : IRenderDeliveryEmailPreviewUseCase
{
    private readonly EmailComposerRegistry registry;

    public RenderDeliveryEmailPreviewUseCase(EmailComposerRegistry? registry = null)
    {
        this.registry = registry ?? EmailComposerRegistry.CreateDefault();
    }

    public Task<RenderDeliveryEmailPreviewResult> ExecuteAsync(
        RenderDeliveryEmailPreviewRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var context = new DeliveryReadyEmailContext(
            request.Tokens,
            request.ShareUrl,
            request.VersionLabel,
            request.RetentionUntilUtc,
            request.Files,
            request.Recipients,
            request.ReplyTo,
            request.LogoUrl,
            request.FromName);

        var composer = registry.Get(EmailKind.DeliveryReady);
        var message = composer.Build(context);
        return Task.FromResult(new RenderDeliveryEmailPreviewResult(message));
    }
}
