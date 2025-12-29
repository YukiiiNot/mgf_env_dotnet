namespace MGF.Worker.Integrations.Email;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

internal static class EmailSenderFactory
{
    public static IEmailSender Create(IConfiguration configuration, ILogger? logger = null)
    {
        var provider = configuration["Integrations:Email:Provider"]
            ?? configuration["Email:Provider"]
            ?? "smtp";

        if (provider.Equals("gmail", StringComparison.OrdinalIgnoreCase)
            || provider.Equals("gmail_api", StringComparison.OrdinalIgnoreCase))
        {
            return new GmailApiEmailSender(configuration, logger);
        }

        return new SmtpEmailSender(configuration);
    }
}
