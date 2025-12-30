namespace MGF.Worker.Email.Composition;

using MGF.Worker.Email.Models;

public interface IEmailComposer
{
    EmailKind Kind { get; }
    DeliveryEmailRequest Build(object context);
}
