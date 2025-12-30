namespace MGF.Worker.Email.Registry;

using MGF.Worker.Email.Composition;
using MGF.Worker.Email.Models;

public sealed class EmailComposerRegistry
{
    private readonly Dictionary<EmailKind, IEmailComposer> composers;

    public EmailComposerRegistry(IEnumerable<IEmailComposer> composers)
    {
        this.composers = composers.ToDictionary(c => c.Kind);
    }

    public static EmailComposerRegistry CreateDefault()
    {
        return new EmailComposerRegistry(new IEmailComposer[]
        {
            new DeliveryReadyEmailComposer()
        });
    }

    public IEmailComposer Get(EmailKind kind)
    {
        if (composers.TryGetValue(kind, out var composer))
        {
            return composer;
        }

        throw new InvalidOperationException($"Email composer not registered for kind {kind}.");
    }
}
