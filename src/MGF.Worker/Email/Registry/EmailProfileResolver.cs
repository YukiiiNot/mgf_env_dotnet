namespace MGF.Worker.Email.Registry;

using Microsoft.Extensions.Configuration;
using MGF.Worker.Email.Models;

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
                ? new List<string> { "deliveries@mgfilms.pro", "info@mgfilms.pro" }
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
