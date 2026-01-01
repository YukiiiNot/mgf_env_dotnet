namespace MGF.Worker.Email.Sending;

using Microsoft.Extensions.Configuration;
using MGF.Contracts.Abstractions.Email;

internal static class EmailSenderFactory
{
    public static IEmailSender Create(
        IConfiguration configuration,
        IEmailSender gmailSender,
        IEmailSender smtpSender)
    {
        var provider = configuration["Integrations:Email:Provider"]
            ?? configuration["Email:Provider"]
            ?? "smtp";

        if (provider.Equals("gmail", StringComparison.OrdinalIgnoreCase)
            || provider.Equals("gmail_api", StringComparison.OrdinalIgnoreCase))
        {
            return gmailSender;
        }

        return smtpSender;
    }
}
