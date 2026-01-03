namespace MGF.Email.Composition;

using Microsoft.Extensions.Configuration;
using MGF.Contracts.Abstractions.Email;

public static class EmailSenderFactory
{
    public static IEmailSender Create(
        IConfiguration configuration,
        IEmailSender gmailSender,
        IEmailSender smtpSender,
        IEmailSender previewSender)
    {
        var provider = configuration["Integrations:Email:Provider"]
            ?? configuration["Email:Provider"]
            ?? "smtp";

        if (provider.Equals("preview", StringComparison.OrdinalIgnoreCase))
        {
            return previewSender;
        }

        if (provider.Equals("gmail", StringComparison.OrdinalIgnoreCase)
            || provider.Equals("gmail_api", StringComparison.OrdinalIgnoreCase))
        {
            return gmailSender;
        }

        return smtpSender;
    }
}
