namespace MGF.Tools.SquareImport.Importers;

using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using MGF.Domain.Entities;
using MGF.Tools.SquareImport.Normalization;
using MGF.Tools.SquareImport.Reporting;

internal sealed partial class CustomersImporter
{
    private async Task<ImportSummary> ImportFromAppliedReportAsync(
        string filePath,
        CustomersImportOptions options,
        bool dryRun,
        CancellationToken cancellationToken
    )
    {
        var rows = LoadAppliedReport(filePath);
        Console.WriteLine($"square-import customers: mode=applied parsed rows={rows.Count} (dry-run={dryRun}).");

        if (options.WriteReports)
        {
            Console.WriteLine("square-import customers: mode=applied skipping report writing to avoid overwriting inputs.");
        }

        var stats = new CustomersImportStats
        {
            TotalRows = rows.Count,
            ClassifiedOrganizations = rows.Count(r => r.ProposedClientTypeKey.Equals("organization", StringComparison.OrdinalIgnoreCase)),
            ClassifiedIndividuals = rows.Count(r => r.ProposedClientTypeKey.Equals("individual", StringComparison.OrdinalIgnoreCase)),
        };

        var deduped = new Dictionary<string, CustomersImportReportRow>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(row.SquareCustomerId))
            {
                stats.Errors++;
                continue;
            }

            var squareCustomerId = row.SquareCustomerId.Trim();
            if (!deduped.TryAdd(squareCustomerId, row))
            {
                stats.DuplicateSquareCustomerIdsSkipped++;
            }
        }

        var appliedRows = deduped.Values
            .OrderBy(r => r.RowNumber)
            .ThenBy(r => r.SquareCustomerId, StringComparer.Ordinal)
            .ToList();

        for (var offset = 0; offset < appliedRows.Count; offset += DefaultBatchSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var batch = appliedRows.Skip(offset).Take(DefaultBatchSize).ToList();

            if (!dryRun)
            {
                await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
                await ProcessAppliedBatchAsync(batch, stats, cancellationToken, dryRun);
                await db.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);
            }
            else
            {
                await ProcessAppliedBatchAsync(batch, stats, cancellationToken, dryRun);
            }
        }

        var emptyHard = new HardDuplicates(
            rowCount: 0,
            bySquareCustomerId: new Dictionary<string, HardDuplicateRowInfo>(StringComparer.Ordinal),
            duplicateEmails: Array.Empty<(string Email, IReadOnlyList<string> SquareCustomerIds)>(),
            duplicatePhones: Array.Empty<(string Phone, IReadOnlyList<string> SquareCustomerIds)>()
        );
        var emptySoft = new SoftDuplicates(rowCount: 0, bySquareCustomerId: new Dictionary<string, SoftDuplicateRowInfo>(StringComparer.Ordinal));
        WriteDetailedSummary(stats, emptyHard, emptySoft, reportDirPath: "(n/a)", reportsWritten: false);

        return new ImportSummary(
            Inserted: stats.TotalInserted,
            Updated: stats.TotalUpdated,
            Skipped: stats.TotalSkipped + stats.DuplicateSquareCustomerIdsSkipped,
            Errors: stats.Errors
        );
    }

    private static List<CustomersImportReportRow> LoadAppliedReport(string path)
    {
        using var reader = new StreamReader(path);
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            PrepareHeaderForMatch = args => args.Header?.Trim() ?? string.Empty,
            TrimOptions = TrimOptions.Trim,
            IgnoreBlankLines = true,
            BadDataFound = null,
            MissingFieldFound = null,
            HeaderValidated = null,
        };

        using var csv = new CsvReader(reader, config);
        return csv.GetRecords<CustomersImportReportRow>().ToList();
    }

    private async Task ProcessAppliedBatchAsync(
        IReadOnlyList<CustomersImportReportRow> batch,
        CustomersImportStats stats,
        CancellationToken cancellationToken,
        bool dryRun
    )
    {
        var clientIntegrationsSquare = db.Set<Dictionary<string, object>>("client_integrations_square");
        var billingProfiles = db.Set<Dictionary<string, object>>("client_billing_profiles");
        var personContacts = db.Set<Dictionary<string, object>>("person_contacts");
        var clientContacts = db.Set<Dictionary<string, object>>("client_contacts");

        var squareCustomerIds = batch
            .Select(r => r.SquareCustomerId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var matchedClientIds = batch
            .Select(r => r.MatchedClientId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var matchedPersonIds = batch
            .Select(r => r.MatchedPersonId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var integrationsBySquareCustomerId = squareCustomerIds.Length == 0
            ? new Dictionary<string, Dictionary<string, object>>(StringComparer.Ordinal)
            : (
                await clientIntegrationsSquare.AsNoTracking()
                    .Where(
                        cis =>
                            EF.Property<string?>(cis, "square_customer_id") != null
                            && squareCustomerIds.Contains(EF.Property<string?>(cis, "square_customer_id")!)
                    )
                    .Select(
                        cis =>
                            new
                            {
                                SquareCustomerId = EF.Property<string>(cis, "square_customer_id")!,
                                Row = cis,
                            }
                    )
                    .ToListAsync(cancellationToken)
            )
            .Where(x => !string.IsNullOrWhiteSpace(x.SquareCustomerId))
            .GroupBy(x => x.SquareCustomerId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Row, StringComparer.Ordinal);

        var integrationsByClientId = matchedClientIds.Length == 0
            ? new Dictionary<string, Dictionary<string, object>>(StringComparer.Ordinal)
            : (
                await (dryRun ? clientIntegrationsSquare.AsNoTracking() : clientIntegrationsSquare)
                    .Where(cis => matchedClientIds.Contains(EF.Property<string>(cis, "client_id")))
                    .Select(
                        cis =>
                            new
                            {
                                ClientId = EF.Property<string>(cis, "client_id"),
                                Row = cis,
                            }
                    )
                    .ToListAsync(cancellationToken)
            )
            .Where(x => !string.IsNullOrWhiteSpace(x.ClientId))
            .GroupBy(x => x.ClientId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Row, StringComparer.Ordinal);

        var clientsById = matchedClientIds.Length == 0
            ? new Dictionary<string, Client>(StringComparer.Ordinal)
            : await db.Clients.AsNoTracking()
                .Where(c => matchedClientIds.Contains(c.ClientId))
                .ToDictionaryAsync(c => c.ClientId, StringComparer.Ordinal, cancellationToken);

        var peopleById = matchedPersonIds.Length == 0
            ? new Dictionary<string, Person>(StringComparer.Ordinal)
            : await db.People.AsNoTracking()
                .Where(p => matchedPersonIds.Contains(p.PersonId))
                .ToDictionaryAsync(p => p.PersonId, StringComparer.Ordinal, cancellationToken);

        foreach (var row in batch.OrderBy(r => r.RowNumber).ThenBy(r => r.SquareCustomerId, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var action = (row.ProposedAction ?? string.Empty).Trim().ToLowerInvariant();
            if (action.Length == 0 || action == "skip")
            {
                stats.RowsSkipped++;
                continue;
            }

            if (string.IsNullOrWhiteSpace(row.SquareCustomerId))
            {
                stats.Errors++;
                continue;
            }

            var squareCustomerId = row.SquareCustomerId.Trim();
            var clientId = string.IsNullOrWhiteSpace(row.MatchedClientId) ? null : row.MatchedClientId.Trim();

            if (string.IsNullOrWhiteSpace(clientId))
            {
                stats.Errors++;
                continue;
            }

            var personId = string.IsNullOrWhiteSpace(row.MatchedPersonId) ? null : row.MatchedPersonId.Trim();

            if (integrationsBySquareCustomerId.TryGetValue(squareCustomerId, out var existingBySquare))
            {
                var mappedClientId = GetString(existingBySquare, "client_id");
                if (!string.Equals(mappedClientId, clientId, StringComparison.Ordinal))
                {
                    stats.Errors++;
                    continue;
                }
            }

            clientsById.TryGetValue(clientId, out var existingClient);
            peopleById.TryGetValue(personId ?? string.Empty, out var existingPerson);

            var beforeInserted = stats.TotalInserted;
            var beforeUpdated = stats.TotalUpdated;

            if (personId is not null)
            {
                await UpsertPersonFromAppliedAsync(existingPerson, personId, row, stats, cancellationToken, dryRun);
                await UpsertPersonContactsFromAppliedAsync(personContacts, personId, row, stats, cancellationToken, dryRun);
            }

            var primaryContactPersonId = existingClient?.PrimaryContactPersonId ?? personId;

            await UpsertClientAsync(
                existingClient,
                clientId,
                row.DisplayName,
                row.ProposedClientTypeKey,
                primaryContactPersonId,
                stats,
                cancellationToken,
                dryRun
            );

            if (
                personId is null
                && row.ProposedClientTypeKey.Equals("organization", StringComparison.OrdinalIgnoreCase)
                && (!string.IsNullOrWhiteSpace(row.NormalizedEmail) || !string.IsNullOrWhiteSpace(row.NormalizedPhone))
            )
            {
                await UpsertClientBillingProfileAsync(
                    billingProfiles,
                    clientId,
                    billingEmail: row.NormalizedEmail,
                    billingPhone: row.NormalizedPhone,
                    addressLine1: null,
                    addressLine2: null,
                    addressCity: null,
                    addressRegion: null,
                    addressPostalCode: null,
                    stats,
                    cancellationToken,
                    dryRun
                );
            }

            if (personId is not null)
            {
                await UpsertClientContactsAsync(
                    clientContacts,
                    clientId,
                    personId,
                    isPrimary: primaryContactPersonId is not null && primaryContactPersonId == personId,
                    stats,
                    cancellationToken,
                    dryRun
                );
            }

            integrationsByClientId.TryGetValue(clientId, out var existingIntegrationForClient);
            await UpsertClientIntegrationSquareFromAppliedAsync(
                clientIntegrationsSquare,
                existingIntegrationForClient,
                clientId,
                squareCustomerId,
                stats,
                cancellationToken,
                dryRun
            );

            var didChange = stats.TotalInserted != beforeInserted || stats.TotalUpdated != beforeUpdated;
            if (!didChange)
            {
                stats.RowsSkipped++;
                continue;
            }

            if (action == "insert")
            {
                stats.RowsInserted++;
            }
            else if (action == "link")
            {
                stats.RowsLinked++;
            }
            else
            {
                stats.RowsUpdated++;
            }
        }
    }

    private async Task UpsertClientIntegrationSquareFromAppliedAsync(
        DbSet<Dictionary<string, object>> clientIntegrationsSquare,
        Dictionary<string, object>? existingIntegration,
        string clientId,
        string squareCustomerId,
        CustomersImportStats stats,
        CancellationToken cancellationToken,
        bool dryRun
    )
    {
        if (existingIntegration is null)
        {
            stats.ClientIntegrationsInserted++;
            if (dryRun)
            {
                return;
            }

            await clientIntegrationsSquare.AddAsync(
                new Dictionary<string, object>
                {
                    ["client_id"] = clientId,
                    ["creation_source_key"] = null!,
                    ["currency_code"] = null!,
                    ["first_visit_at"] = null!,
                    ["last_visit_at"] = null!,
                    ["lifetime_spend_cents"] = null!,
                    ["square_customer_id"] = squareCustomerId,
                    ["transaction_count"] = null!,
                    ["updated_at"] = DateTimeOffset.UtcNow,
                },
                cancellationToken
            );

            return;
        }

        var existingSquareCustomerId = GetString(existingIntegration, "square_customer_id");
        if (!string.IsNullOrWhiteSpace(existingSquareCustomerId) && !string.Equals(existingSquareCustomerId, squareCustomerId, StringComparison.Ordinal))
        {
            stats.Errors++;
            return;
        }

        var changed = false;
        changed |= SetIfDifferent(existingIntegration, "square_customer_id", squareCustomerId);
        if (!changed)
        {
            stats.ClientIntegrationsSkipped++;
            return;
        }

        stats.ClientIntegrationsUpdated++;
        if (dryRun)
        {
            return;
        }

        existingIntegration["updated_at"] = DateTimeOffset.UtcNow;
    }

    private async Task UpsertPersonFromAppliedAsync(
        Person? existingPerson,
        string personId,
        CustomersImportReportRow row,
        CustomersImportStats stats,
        CancellationToken cancellationToken,
        bool dryRun
    )
    {
        if (existingPerson is null)
        {
            stats.PeopleInserted++;
            if (dryRun)
            {
                return;
            }

            var (firstName, lastName, displayName) = ComputePersonNamesFromApplied(row);

            await db.People.AddAsync(
                new Person(
                    personId: personId,
                    firstName: firstName,
                    lastName: lastName,
                    displayName: displayName,
                    initials: null,
                    statusKey: "active",
                    timezone: null,
                    defaultHostKey: null,
                    notes: null,
                    dataProfile: "real",
                    createdAt: DateTimeOffset.UtcNow,
                    updatedAt: null
                ),
                cancellationToken
            );

            return;
        }

        if (!string.IsNullOrWhiteSpace(existingPerson.DisplayName) || string.IsNullOrWhiteSpace(row.DisplayName))
        {
            stats.PeopleSkipped++;
            return;
        }

        stats.PeopleUpdated++;
        if (dryRun)
        {
            return;
        }

        db.People.Update(
            new Person(
                personId: existingPerson.PersonId,
                firstName: existingPerson.FirstName,
                lastName: existingPerson.LastName,
                displayName: row.DisplayName,
                initials: existingPerson.Initials,
                statusKey: existingPerson.StatusKey,
                timezone: existingPerson.Timezone,
                defaultHostKey: existingPerson.DefaultHostKey,
                notes: existingPerson.Notes,
                dataProfile: existingPerson.DataProfile,
                createdAt: existingPerson.CreatedAt,
                updatedAt: DateTimeOffset.UtcNow
            )
        );
    }

    private static (string FirstName, string LastName, string? DisplayName) ComputePersonNamesFromApplied(CustomersImportReportRow row)
    {
        var display = IdentityKeys.NormalizeName(row.DisplayName) ?? "Unknown";

        if (row.ProposedClientTypeKey.Equals("organization", StringComparison.OrdinalIgnoreCase))
        {
            return (display, "Contact", display);
        }

        var parts = display.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length >= 2)
        {
            return (parts[0], parts[^1], display);
        }

        return (display, "Unknown", display);
    }

    private static async Task UpsertPersonContactsFromAppliedAsync(
        DbSet<Dictionary<string, object>> personContacts,
        string personId,
        CustomersImportReportRow row,
        CustomersImportStats stats,
        CancellationToken cancellationToken,
        bool dryRun
    )
    {
        var email = IdentityKeys.NormalizeEmail(row.NormalizedEmail);
        var phone = IdentityKeys.NormalizePhone(row.NormalizedPhone);

        if (email is null && phone is null)
        {
            stats.PersonContactsSkipped++;
            return;
        }

        var existing = await (dryRun ? personContacts.AsNoTracking() : personContacts)
            .SingleOrDefaultAsync(x => EF.Property<string>(x, "person_id") == personId, cancellationToken);

        if (existing is null)
        {
            stats.PersonContactsInserted++;
            if (dryRun)
            {
                return;
            }

            await personContacts.AddAsync(
                new Dictionary<string, object>
                {
                    ["person_id"] = personId,
                    ["discord_handle"] = null!,
                    ["email"] = email!,
                    ["phone"] = phone!,
                    ["updated_at"] = DateTimeOffset.UtcNow,
                },
                cancellationToken
            );

            return;
        }

        var desiredEmail = email ?? GetString(existing, "email");
        var desiredPhone = phone ?? GetString(existing, "phone");

        var changed =
            !string.Equals(GetString(existing, "email"), desiredEmail, StringComparison.Ordinal)
            || !string.Equals(GetString(existing, "phone"), desiredPhone, StringComparison.Ordinal);

        if (!changed)
        {
            stats.PersonContactsSkipped++;
            return;
        }

        stats.PersonContactsUpdated++;
        if (dryRun)
        {
            return;
        }

        existing["email"] = desiredEmail!;
        existing["phone"] = desiredPhone!;
        existing["updated_at"] = DateTimeOffset.UtcNow;
    }
}
