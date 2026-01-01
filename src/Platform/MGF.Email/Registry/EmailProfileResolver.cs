namespace MGF.Email.Registry;

using Microsoft.Extensions.Configuration;
using MGF.Contracts.Abstractions.Email;

public static class EmailProfileResolver
{
    public static EmailProfile Resolve(IConfiguration configuration, string? profileKey)
    {
        var key = string.IsNullOrWhiteSpace(profileKey) ? EmailProfiles.Deliveries : profileKey!;
        var basePath = $"Integrations:Email:Profiles:{key}";
        var allowed = ReadAllowedFrom(configuration.GetSection($"{basePath}:AllowedFrom"));

        if (allowed.Count == 0)
        {
            var raw = configuration[$"{basePath}:AllowedFrom"];
            if (!string.IsNullOrWhiteSpace(raw))
            {
                allowed = NormalizeList(raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            }
        }

        if (allowed.Count == 0)
        {
            allowed = key.Equals(EmailProfiles.Deliveries, StringComparison.OrdinalIgnoreCase)
                ? new List<string>
                {
                    "admin@mgfilms.pro",
                    "info@mgfilms.pro",
                    "contact@mgfilms.pro",
                    "billing@mgfilms.pro",
                    "support@mgfilms.pro",
                    "bookings@mgfilms.pro",
                    "deliveries@mgfilms.pro",
                    "ermano.cayard@mgfilms.pro",
                    "creative@mgfilms.pro",
                    "ops@mgfilms.pro",
                    "cayard.ermano@mgfilms.pro",
                    "ermano@mgfilms.pro",
                    "martin.price@mgfilms.pro",
                    "ceo@mgfilms.pro",
                    "price.martin@mgfilms.pro",
                    "dex@mgfilms.pro",
                    "martin@mgfilms.pro"
                }
                : new List<string> { "info@mgfilms.pro" };
        }

        var fromName = configuration[$"{basePath}:FromName"] ?? configuration["Integrations:Email:FromName"];
        var replyTo = configuration[$"{basePath}:ReplyTo"] ?? configuration["Integrations:Email:ReplyTo"];
        var logoUrl = configuration[$"{basePath}:LogoUrl"] ?? configuration["Integrations:Email:Branding:LogoUrl"];
        if (string.IsNullOrWhiteSpace(logoUrl) && key.Equals(EmailProfiles.Deliveries, StringComparison.OrdinalIgnoreCase))
        {
            logoUrl = "https://yukiiinot.github.io/mgf_env_dotnet/assets/email/mg_sig_logo_white.png";
        }

        return new EmailProfile(key, allowed, fromName, replyTo, logoUrl);
    }

    public static bool IsAllowedFrom(EmailProfile profile, string fromAddress)
    {
        return profile.AllowedFrom.Any(addr => string.Equals(addr, fromAddress, StringComparison.OrdinalIgnoreCase));
    }

    public static string AllowedFromDisplay(EmailProfile profile)
    {
        return string.Join(" or ", profile.AllowedFrom);
    }

    private static List<string> ReadAllowedFrom(IConfigurationSection section)
    {
        var values = section.GetChildren()
            .Select(child => child.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .ToList();

        return NormalizeList(values);
    }

    private static List<string> NormalizeList(IEnumerable<string> values)
    {
        return values
            .Select(value => value.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
