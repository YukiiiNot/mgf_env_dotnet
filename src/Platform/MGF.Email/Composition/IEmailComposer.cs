namespace MGF.Email.Composition;

using MGF.Contracts.Abstractions.Email;
using MGF.Email.Models;

public interface IEmailComposer
{
    EmailKind Kind { get; }
    EmailMessage Build(object context);
}
