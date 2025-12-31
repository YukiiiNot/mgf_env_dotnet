namespace MGF.Tools.SquareImport.Importers;

using Microsoft.EntityFrameworkCore;
using MGF.Tools.SquareImport.Normalization;

internal sealed partial class CustomersImporter
{
    private async Task UpsertClientBillingProfileAsync(
        DbSet<Dictionary<string, object>> billingProfiles,
        string clientId,
        string? billingEmail,
        string? billingPhone,
        string? addressLine1,
        string? addressLine2,
        string? addressCity,
        string? addressRegion,
        string? addressPostalCode,
        CustomersImportStats stats,
        CancellationToken cancellationToken,
        bool dryRun
    )
    {
        var email = IdentityKeys.NormalizeEmail(billingEmail);
        var phone = IdentityKeys.NormalizePhone(billingPhone);

        var line1 = IdentityKeys.NormalizeName(addressLine1);
        var line2 = IdentityKeys.NormalizeName(addressLine2);
        var city = IdentityKeys.NormalizeName(addressCity);
        var region = IdentityKeys.NormalizeName(addressRegion);
        var postal = IdentityKeys.NormalizeName(addressPostalCode);

        if (email is null && phone is null && line1 is null && line2 is null && city is null && region is null && postal is null)
        {
            stats.BillingProfilesSkipped++;
            return;
        }

        var existing = await (dryRun ? billingProfiles.AsNoTracking() : billingProfiles)
            .SingleOrDefaultAsync(x => EF.Property<string>(x, "client_id") == clientId, cancellationToken);

        if (existing is null)
        {
            stats.BillingProfilesInserted++;
            if (dryRun)
            {
                return;
            }

            await billingProfiles.AddAsync(
                new Dictionary<string, object>
                {
                    ["client_id"] = clientId,
                    ["address_city"] = city!,
                    ["address_country"] = null!,
                    ["address_line1"] = line1!,
                    ["address_line2"] = line2!,
                    ["address_postal_code"] = postal!,
                    ["address_region"] = region!,
                    ["billing_email"] = email!,
                    ["billing_phone"] = phone!,
                    ["created_at"] = DateTimeOffset.UtcNow,
                    ["tax_id"] = null!,
                    ["updated_at"] = DateTimeOffset.UtcNow,
                },
                cancellationToken
            );

            return;
        }

        var changed = false;
        changed |= SetIfDifferentWhenNotNullOrWhiteSpace(existing, "billing_email", email);
        changed |= SetIfDifferentWhenNotNullOrWhiteSpace(existing, "billing_phone", phone);
        changed |= SetIfDifferentWhenNotNullOrWhiteSpace(existing, "address_line1", line1);
        changed |= SetIfDifferentWhenNotNullOrWhiteSpace(existing, "address_line2", line2);
        changed |= SetIfDifferentWhenNotNullOrWhiteSpace(existing, "address_city", city);
        changed |= SetIfDifferentWhenNotNullOrWhiteSpace(existing, "address_region", region);
        changed |= SetIfDifferentWhenNotNullOrWhiteSpace(existing, "address_postal_code", postal);

        if (!changed)
        {
            stats.BillingProfilesSkipped++;
            return;
        }

        stats.BillingProfilesUpdated++;
        if (dryRun)
        {
            return;
        }

        existing["updated_at"] = DateTimeOffset.UtcNow;
    }

    private static bool SetIfDifferentWhenNotNullOrWhiteSpace(Dictionary<string, object> row, string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var existing = GetString(row, key);
        if (string.Equals(existing, value, StringComparison.Ordinal))
        {
            return false;
        }

        row[key] = value;
        return true;
    }
}

