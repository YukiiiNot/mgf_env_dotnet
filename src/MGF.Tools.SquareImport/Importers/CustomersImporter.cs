namespace MGF.Tools.SquareImport.Importers;

using Microsoft.EntityFrameworkCore;
using MGF.Domain.Entities;
using MGF.Infrastructure.Configuration;
using MGF.Infrastructure.Data;
using MGF.Tools.SquareImport.Normalization;
using MGF.Tools.SquareImport.Parsing;
using MGF.Tools.SquareImport.Reporting;

internal sealed partial class CustomersImporter
{
    private const int DefaultBatchSize = 1000;
    private const string UnmatchedClientNotesMarker = "[system:unmatched_square]";
    private const string UnmatchedSquareCustomerIdSentinel = "__unmatched_square__";

    private readonly AppDbContext db;

    public CustomersImporter(AppDbContext db)
    {
        this.db = db;
    }

    public async Task<ImportSummary> ImportAsync(
        string filePath,
        CustomersImportOptions options,
        bool dryRun,
        CancellationToken cancellationToken
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(options);

        var customers = CsvLoader.LoadCustomers(filePath);
        Console.WriteLine($"square-import customers: parsed rows={customers.Count} (dry-run={dryRun}).");

        var stats = new CustomersImportStats();

        var deduped = new Dictionary<string, SquareCustomerRow>(StringComparer.Ordinal);
        foreach (var row in customers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(row.SquareCustomerId))
            {
                stats.Errors++;
                Console.Error.WriteLine(
                    $"square-import customers: error missing Square Customer ID (row={row.RowNumber} file={Path.GetFileName(row.SourceFile)})"
                );
                continue;
            }

            var squareCustomerId = row.SquareCustomerId.Trim();
            if (!deduped.TryAdd(squareCustomerId, row))
            {
                stats.DuplicateSquareCustomerIdsSkipped++;
            }
        }

        var inputs = deduped.Values
            .OrderBy(r => r.RowNumber)
            .ThenBy(r => r.SquareCustomerId, StringComparer.Ordinal)
            .Select(BuildCustomerIdentity)
            .ToList();

        stats.TotalRows = inputs.Count;
        stats.ClassifiedOrganizations = inputs.Count(x => x.ProposedClientTypeKey == "organization");
        stats.ClassifiedIndividuals = inputs.Count(x => x.ProposedClientTypeKey == "individual");

        var hardDuplicates = DetectHardDuplicates(inputs);
        var softDuplicates = DetectSoftDuplicates(inputs);

        stats.HardDuplicateRows = hardDuplicates.RowCount;
        stats.SoftDuplicateRows = softDuplicates.RowCount;

        CsvReportWriter<CustomersImportReportRow>? hardDuplicatesWriter = null;
        CsvReportWriter<CustomersImportReportRow>? softDuplicatesWriter = null;
        CsvReportWriter<CustomersImportReportRow>? needsReviewWriter = null;
        CsvReportWriter<CustomersImportReportRow>? appliedWriter = null;

        var reportDirPath = options.ReportDir?.FullName ?? ".\\runtime\\square-import\\";
        if (options.WriteReports)
        {
            hardDuplicatesWriter = new CsvReportWriter<CustomersImportReportRow>(
                Path.Combine(reportDirPath, "customers-duplicates-hard.csv")
            );
            softDuplicatesWriter = new CsvReportWriter<CustomersImportReportRow>(
                Path.Combine(reportDirPath, "customers-duplicates-soft.csv")
            );
            needsReviewWriter = new CsvReportWriter<CustomersImportReportRow>(
                Path.Combine(reportDirPath, "customers-needs-review.csv")
            );
            appliedWriter = new CsvReportWriter<CustomersImportReportRow>(
                Path.Combine(reportDirPath, "customers-applied.csv")
            );
        }

        try
        {
            for (var offset = 0; offset < inputs.Count; offset += DefaultBatchSize)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var batch = inputs.Skip(offset).Take(DefaultBatchSize).ToList();

                if (!dryRun)
                {
                    await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
                    await ProcessBatchAsync(
                        batch,
                        options,
                        hardDuplicates,
                        softDuplicates,
                        stats,
                        hardDuplicatesWriter,
                        softDuplicatesWriter,
                        needsReviewWriter,
                        appliedWriter,
                        cancellationToken,
                        dryRun
                    );
                    await db.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                }
                else
                {
                    await ProcessBatchAsync(
                        batch,
                        options,
                        hardDuplicates,
                        softDuplicates,
                        stats,
                        hardDuplicatesWriter,
                        softDuplicatesWriter,
                        needsReviewWriter,
                        appliedWriter,
                        cancellationToken,
                        dryRun
                    );
                }
            }
        }
        finally
        {
            appliedWriter?.Dispose();
            needsReviewWriter?.Dispose();
            softDuplicatesWriter?.Dispose();
            hardDuplicatesWriter?.Dispose();
        }

        WriteDetailedSummary(stats, hardDuplicates, softDuplicates, reportDirPath, options.WriteReports);

        return new ImportSummary(
            Inserted: stats.TotalInserted,
            Updated: stats.TotalUpdated,
            Skipped: stats.TotalSkipped + stats.DuplicateSquareCustomerIdsSkipped,
            Errors: stats.Errors
        );
    }

    public async Task<ImportSummary> VerifyAsync(CancellationToken cancellationToken)
    {
        var clients = db.Clients.AsNoTracking();
        var people = db.People.AsNoTracking();

        var clientIntegrationsSquare = db.Set<Dictionary<string, object>>("client_integrations_square").AsNoTracking();
        var clientContacts = db.Set<Dictionary<string, object>>("client_contacts").AsNoTracking();
        var personContacts = db.Set<Dictionary<string, object>>("person_contacts").AsNoTracking();

        var totalClients = await clients.CountAsync(cancellationToken);
        var totalPeople = await people.CountAsync(cancellationToken);
        var totalClientIntegrationsSquare = await clientIntegrationsSquare.CountAsync(cancellationToken);

        var clientsWithSquareIntegration = await (
            from c in clients
            join cis in clientIntegrationsSquare on c.ClientId equals EF.Property<string>(cis, "client_id")
            select c.ClientId
        ).Distinct().CountAsync(cancellationToken);

        var clientsMissingSquareIntegration = Math.Max(0, totalClients - clientsWithSquareIntegration);

        var squareIntegrationsMissingSquareCustomerId = await clientIntegrationsSquare
            .Where(
                cis =>
                    EF.Property<string?>(cis, "square_customer_id") == null
                    || EF.Property<string?>(cis, "square_customer_id") == string.Empty
            )
            .CountAsync(cancellationToken);

        var emailSquarePairs = await (
            from cis in clientIntegrationsSquare
            join cc in clientContacts on EF.Property<string>(cis, "client_id") equals EF.Property<string>(cc, "client_id")
            where EF.Property<bool>(cc, "is_primary")
            join pc in personContacts on EF.Property<string>(cc, "person_id") equals EF.Property<string>(pc, "person_id")
            let email = EF.Property<string?>(pc, "email")
            let squareCustomerId = EF.Property<string?>(cis, "square_customer_id")
            where email != null && email != string.Empty && squareCustomerId != null && squareCustomerId != string.Empty
            select new
            {
                Email = email!,
                SquareCustomerId = squareCustomerId!,
            }
        ).ToListAsync(cancellationToken);

        var duplicateEmailsTop20 = emailSquarePairs
            .GroupBy(x => x.Email, StringComparer.Ordinal)
            .Select(
                g =>
                    new
                    {
                        Email = g.Key,
                        SquareCustomerIds = g.Select(x => x.SquareCustomerId)
                            .Distinct(StringComparer.Ordinal)
                            .OrderBy(x => x, StringComparer.Ordinal)
                            .ToList(),
                    }
            )
            .Where(x => x.SquareCustomerIds.Count > 1)
            .OrderByDescending(x => x.SquareCustomerIds.Count)
            .ThenBy(x => x.Email, StringComparer.Ordinal)
            .Take(20)
            .ToList();

        Console.WriteLine($"square-import customers: verify total_clients={totalClients}");
        Console.WriteLine($"square-import customers: verify total_people={totalPeople}");
        Console.WriteLine($"square-import customers: verify total_client_integrations_square={totalClientIntegrationsSquare}");
        Console.WriteLine($"square-import customers: verify clients_missing_square_integration={clientsMissingSquareIntegration}");
        Console.WriteLine(
            $"square-import customers: verify square_integrations_missing_square_customer_id={squareIntegrationsMissingSquareCustomerId}"
        );

        if (duplicateEmailsTop20.Count == 0)
        {
            Console.WriteLine("square-import customers: verify duplicate_emails_top20=none");
        }
        else
        {
            Console.WriteLine($"square-import customers: verify duplicate_emails_top20={duplicateEmailsTop20.Count}");
            foreach (var dup in duplicateEmailsTop20)
            {
                Console.WriteLine($"square-import customers: verify dup-email {dup.Email} => {string.Join(", ", dup.SquareCustomerIds)}");
            }
        }

        return ImportSummary.Empty();
    }

    public async Task<ImportSummary> ResetAsync(bool dryRun, CancellationToken cancellationToken)
    {
        var env = DatabaseConnection.GetEnvironment();
        if (env != MgfEnvironment.Dev)
        {
            Console.Error.WriteLine($"square-import customers: --reset blocked (DEV only). Current MGF_ENV={env}.");
            return new ImportSummary(Inserted: 0, Updated: 0, Skipped: 0, Errors: 1);
        }

        Console.WriteLine($"square-import customers: reset starting (dry-run={dryRun})");

        var clientIntegrationsSquare = db.Set<Dictionary<string, object>>("client_integrations_square");
        var clientContacts = db.Set<Dictionary<string, object>>("client_contacts");
        var personContacts = db.Set<Dictionary<string, object>>("person_contacts");

        var candidateClientIds = await clientIntegrationsSquare.AsNoTracking()
            .Where(
                cis =>
                    EF.Property<string?>(cis, "square_customer_id") != null
                    && EF.Property<string?>(cis, "square_customer_id") != string.Empty
                    && EF.Property<string?>(cis, "square_customer_id") != UnmatchedSquareCustomerIdSentinel
            )
            .Select(cis => EF.Property<string>(cis, "client_id"))
            .Distinct()
            .ToListAsync(cancellationToken);

        if (candidateClientIds.Count == 0)
        {
            Console.WriteLine("square-import customers: reset found 0 Square-integrated clients.");
            WriteResetCounts(
                dryRun: dryRun,
                clientsRemoved: 0,
                clientIntegrationsSquareRemoved: 0,
                clientContactsRemoved: 0,
                peopleRemoved: 0,
                personContactsRemoved: 0
            );
            return ImportSummary.Empty();
        }

        var excludedClientIds = await db.Clients.AsNoTracking()
            .Where(c => candidateClientIds.Contains(c.ClientId) && c.Notes == UnmatchedClientNotesMarker)
            .Select(c => c.ClientId)
            .ToListAsync(cancellationToken);

        if (excludedClientIds.Count > 0)
        {
            candidateClientIds = candidateClientIds.Except(excludedClientIds, StringComparer.Ordinal).ToList();
        }

        if (candidateClientIds.Count == 0)
        {
            Console.WriteLine("square-import customers: reset found 0 deletable clients after excluding system rows.");
            WriteResetCounts(
                dryRun: dryRun,
                clientsRemoved: 0,
                clientIntegrationsSquareRemoved: 0,
                clientContactsRemoved: 0,
                peopleRemoved: 0,
                personContactsRemoved: 0
            );
            return ImportSummary.Empty();
        }

        var clientsRemoved = 0;
        var clientIntegrationsSquareRemoved = 0;
        var clientContactsRemoved = 0;

        var personIdsFromDeletedClients = new HashSet<string>(StringComparer.Ordinal);

        var clientsToProcess = candidateClientIds
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        foreach (var clientId in clientsToProcess)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (dryRun)
            {
                clientContactsRemoved += await clientContacts.AsNoTracking()
                    .Where(cc => EF.Property<string>(cc, "client_id") == clientId)
                    .CountAsync(cancellationToken);

                clientIntegrationsSquareRemoved += await clientIntegrationsSquare.AsNoTracking()
                    .Where(cis => EF.Property<string>(cis, "client_id") == clientId)
                    .CountAsync(cancellationToken);

                clientsRemoved += await db.Clients.AsNoTracking()
                    .Where(c => c.ClientId == clientId)
                    .CountAsync(cancellationToken);

                var personIds = await GetPersonIdsForClientAsync(clientId, clientContacts, cancellationToken);
                foreach (var personId in personIds)
                {
                    personIdsFromDeletedClients.Add(personId);
                }

                continue;
            }

            var personIdsForClient = await GetPersonIdsForClientAsync(clientId, clientContacts, cancellationToken);

            await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                clientContactsRemoved += await clientContacts
                    .Where(cc => EF.Property<string>(cc, "client_id") == clientId)
                    .ExecuteDeleteAsync(cancellationToken);

                clientIntegrationsSquareRemoved += await clientIntegrationsSquare
                    .Where(cis => EF.Property<string>(cis, "client_id") == clientId)
                    .ExecuteDeleteAsync(cancellationToken);

                clientsRemoved += await db.Clients
                    .Where(c => c.ClientId == clientId)
                    .ExecuteDeleteAsync(cancellationToken);

                await tx.CommitAsync(cancellationToken);

                foreach (var personId in personIdsForClient)
                {
                    personIdsFromDeletedClients.Add(personId);
                }
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(cancellationToken);
                Console.Error.WriteLine($"square-import customers: reset skipped client_id={clientId}: {ex.Message}");
            }
        }

        var peopleRemoved = 0;
        var personContactsRemoved = 0;

        var candidatePersonIds = personIdsFromDeletedClients
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        if (candidatePersonIds.Count > 0)
        {
            var referencedByClientContacts = await clientContacts.AsNoTracking()
                .Where(cc => candidatePersonIds.Contains(EF.Property<string>(cc, "person_id")))
                .Select(cc => EF.Property<string>(cc, "person_id"))
                .Distinct()
                .ToListAsync(cancellationToken);

            var referencedAsPrimaryContact = await db.Clients.AsNoTracking()
                .Where(c => c.PrimaryContactPersonId != null && candidatePersonIds.Contains(c.PrimaryContactPersonId))
                .Select(c => c.PrimaryContactPersonId!)
                .Distinct()
                .ToListAsync(cancellationToken);

            var referencedAsAccountOwner = await db.Clients.AsNoTracking()
                .Where(c => c.AccountOwnerPersonId != null && candidatePersonIds.Contains(c.AccountOwnerPersonId))
                .Select(c => c.AccountOwnerPersonId!)
                .Distinct()
                .ToListAsync(cancellationToken);

            var protectedPersonIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in referencedByClientContacts)
            {
                protectedPersonIds.Add(id);
            }
            foreach (var id in referencedAsPrimaryContact)
            {
                protectedPersonIds.Add(id);
            }
            foreach (var id in referencedAsAccountOwner)
            {
                protectedPersonIds.Add(id);
            }

            var deletablePersonIds = candidatePersonIds.Where(id => !protectedPersonIds.Contains(id)).ToList();

            foreach (var personId in deletablePersonIds)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (dryRun)
                {
                    personContactsRemoved += await personContacts.AsNoTracking()
                        .Where(pc => EF.Property<string>(pc, "person_id") == personId)
                        .CountAsync(cancellationToken);

                    peopleRemoved += await db.People.AsNoTracking()
                        .Where(p => p.PersonId == personId)
                        .CountAsync(cancellationToken);
                    continue;
                }

                await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
                try
                {
                    personContactsRemoved += await personContacts
                        .Where(pc => EF.Property<string>(pc, "person_id") == personId)
                        .ExecuteDeleteAsync(cancellationToken);

                    peopleRemoved += await db.People
                        .Where(p => p.PersonId == personId)
                        .ExecuteDeleteAsync(cancellationToken);

                    await tx.CommitAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    await tx.RollbackAsync(cancellationToken);
                    Console.Error.WriteLine($"square-import customers: reset skipped person_id={personId}: {ex.Message}");
                }
            }
        }

        WriteResetCounts(
            dryRun: dryRun,
            clientsRemoved: clientsRemoved,
            clientIntegrationsSquareRemoved: clientIntegrationsSquareRemoved,
            clientContactsRemoved: clientContactsRemoved,
            peopleRemoved: peopleRemoved,
            personContactsRemoved: personContactsRemoved
        );

        return ImportSummary.Empty();
    }

    private async Task<IReadOnlyList<string>> GetPersonIdsForClientAsync(
        string clientId,
        DbSet<Dictionary<string, object>> clientContacts,
        CancellationToken cancellationToken
    )
    {
        var result = new HashSet<string>(StringComparer.Ordinal);

        var client = await db.Clients.AsNoTracking()
            .Where(c => c.ClientId == clientId)
            .Select(
                c =>
                    new
                    {
                        c.PrimaryContactPersonId,
                        c.AccountOwnerPersonId,
                    }
            )
            .SingleOrDefaultAsync(cancellationToken);

        if (client?.PrimaryContactPersonId is not null)
        {
            result.Add(client.PrimaryContactPersonId);
        }

        if (client?.AccountOwnerPersonId is not null)
        {
            result.Add(client.AccountOwnerPersonId);
        }

        var contactPersonIds = await clientContacts.AsNoTracking()
            .Where(cc => EF.Property<string>(cc, "client_id") == clientId)
            .Select(cc => EF.Property<string>(cc, "person_id"))
            .Distinct()
            .ToListAsync(cancellationToken);

        foreach (var personId in contactPersonIds)
        {
            if (!string.IsNullOrWhiteSpace(personId))
            {
                result.Add(personId);
            }
        }

        return result.OrderBy(x => x, StringComparer.Ordinal).ToList();
    }

    private static void WriteResetCounts(
        bool dryRun,
        int clientsRemoved,
        int clientIntegrationsSquareRemoved,
        int clientContactsRemoved,
        int peopleRemoved,
        int personContactsRemoved
    )
    {
        var prefix = dryRun ? "would_remove" : "removed";
        Console.WriteLine($"square-import customers: reset {prefix} clients={clientsRemoved}");
        Console.WriteLine($"square-import customers: reset {prefix} client_integrations_square={clientIntegrationsSquareRemoved}");
        Console.WriteLine($"square-import customers: reset {prefix} client_contacts={clientContactsRemoved}");
        Console.WriteLine($"square-import customers: reset {prefix} person_contacts={personContactsRemoved}");
        Console.WriteLine($"square-import customers: reset {prefix} people={peopleRemoved}");
    }

    private async Task ImportRowsAsync(
        IEnumerable<SquareCustomerRow> rows,
        CustomersImportStats stats,
        CancellationToken cancellationToken,
        bool dryRun
    )
    {
        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await ImportRowAsync(row, stats, cancellationToken, dryRun);
            }
            catch (Exception ex)
            {
                stats.Errors++;
                Console.Error.WriteLine(
                    $"square-import customers: error importing SquareCustomerId={row.SquareCustomerId} (row={row.RowNumber}): {ex.Message}"
                );
                throw;
            }
        }
    }

    private async Task ImportRowAsync(SquareCustomerRow row, CustomersImportStats stats, CancellationToken cancellationToken, bool dryRun)
    {
        var squareCustomerId = row.SquareCustomerId!.Trim();

        var displayName = ComputeClientDisplayName(row, squareCustomerId);
        var clientTypeKey = string.IsNullOrWhiteSpace(row.CompanyName) ? "individual" : "organization";

        var shouldCreatePerson =
            !string.IsNullOrWhiteSpace(row.FirstName)
            || !string.IsNullOrWhiteSpace(row.LastName)
            || !string.IsNullOrWhiteSpace(row.EmailAddress)
            || !string.IsNullOrWhiteSpace(row.PhoneNumber);

        var clientIntegrationsSquare = db.Set<Dictionary<string, object>>("client_integrations_square");
        var personContacts = db.Set<Dictionary<string, object>>("person_contacts");
        var clientContacts = db.Set<Dictionary<string, object>>("client_contacts");

        var existingIntegration = await (dryRun
                ? clientIntegrationsSquare.AsNoTracking()
                : clientIntegrationsSquare
            )
            .SingleOrDefaultAsync(
                x => EF.Property<string?>(x, "square_customer_id") == squareCustomerId,
                cancellationToken
            );

        var existingClientId = existingIntegration is null ? null : GetString(existingIntegration, "client_id");
        var existingClient = existingClientId is null
            ? null
            : await db.Clients.AsNoTracking().SingleOrDefaultAsync(x => x.ClientId == existingClientId, cancellationToken);

        if (existingIntegration is not null && existingClient is null)
        {
            stats.Errors++;
            Console.Error.WriteLine(
                $"square-import customers: error SquareCustomerId={squareCustomerId} has integration mapping but missing client_id={existingClientId}"
            );
            return;
        }

        var clientId = existingClient?.ClientId ?? EntityIds.NewClientId();

        string? personId = null;
        Person? existingPerson = null;

        if (shouldCreatePerson)
        {
            personId = existingClient?.PrimaryContactPersonId;

            if (personId is null && existingClient is not null)
            {
                personId = await FindExistingPrimaryContactPersonIdAsync(clientContacts, clientId, cancellationToken, dryRun);
            }

            if (personId is not null)
            {
                existingPerson = await db.People.AsNoTracking()
                    .SingleOrDefaultAsync(x => x.PersonId == personId, cancellationToken);

                if (existingPerson is null)
                {
                    stats.Errors++;
                    Console.Error.WriteLine(
                        $"square-import customers: error client_id={clientId} references missing person_id={personId}"
                    );
                    personId = null;
                }
            }

            personId ??= EntityIds.NewPersonId();
        }

        var desiredPrimaryContactPersonId = existingClient?.PrimaryContactPersonId;
        if (desiredPrimaryContactPersonId is null && personId is not null)
        {
            desiredPrimaryContactPersonId = personId;
        }

        await UpsertPersonAsync(
            existingPerson,
            personId,
            row,
            stats,
            cancellationToken,
            dryRun
        );

        await UpsertClientAsync(
            existingClient,
            clientId,
            displayName,
            clientTypeKey,
            desiredPrimaryContactPersonId,
            stats,
            cancellationToken,
            dryRun
        );

        await UpsertPersonContactsAsync(personContacts, personId, row, stats, cancellationToken, dryRun);
        await UpsertClientContactsAsync(
            clientContacts,
            clientId,
            personId,
            isPrimary: desiredPrimaryContactPersonId is not null && desiredPrimaryContactPersonId == personId,
            stats,
            cancellationToken,
            dryRun
        );

        await UpsertClientIntegrationSquareAsync(
            clientIntegrationsSquare,
            existingIntegration,
            clientId,
            squareCustomerId,
            row,
            stats,
            cancellationToken,
            dryRun
        );
    }

    private async Task UpsertClientAsync(
        Client? existingClient,
        string clientId,
        string displayName,
        string clientTypeKey,
        string? primaryContactPersonId,
        CustomersImportStats stats,
        CancellationToken cancellationToken,
        bool dryRun
    )
    {
        if (existingClient is null)
        {
            stats.ClientsInserted++;
            if (dryRun)
            {
                return;
            }

            await db.Clients.AddAsync(
                new Client(
                    clientId: clientId,
                    displayName: displayName,
                    clientTypeKey: clientTypeKey,
                    statusKey: "active",
                    primaryContactPersonId: primaryContactPersonId,
                    accountOwnerPersonId: null,
                    notes: null,
                    dataProfile: "real",
                    createdAt: DateTimeOffset.UtcNow,
                    updatedAt: null
                ),
                cancellationToken
            );

            return;
        }

        var desiredDisplayName = existingClient.DisplayName;
        if (ShouldOverwriteDisplayName(existingClient.DisplayName, displayName))
        {
            desiredDisplayName = displayName;
        }

        var desiredClientTypeKey = existingClient.ClientTypeKey;
        if (
            string.Equals(clientTypeKey, "organization", StringComparison.Ordinal)
            && !string.Equals(existingClient.ClientTypeKey, "organization", StringComparison.Ordinal)
        )
        {
            desiredClientTypeKey = "organization";
        }

        var desiredPrimaryContactPersonId = existingClient.PrimaryContactPersonId ?? primaryContactPersonId;

        var needsUpdate =
            !string.Equals(existingClient.DisplayName, desiredDisplayName, StringComparison.Ordinal)
            || !string.Equals(existingClient.ClientTypeKey, desiredClientTypeKey, StringComparison.Ordinal)
            || !string.Equals(existingClient.PrimaryContactPersonId, desiredPrimaryContactPersonId, StringComparison.Ordinal);

        if (!needsUpdate)
        {
            stats.ClientsSkipped++;
            return;
        }

        stats.ClientsUpdated++;
        if (dryRun)
        {
            return;
        }

        db.Clients.Update(
            new Client(
                clientId: existingClient.ClientId,
                displayName: desiredDisplayName,
                clientTypeKey: desiredClientTypeKey,
                statusKey: existingClient.StatusKey,
                primaryContactPersonId: desiredPrimaryContactPersonId,
                accountOwnerPersonId: existingClient.AccountOwnerPersonId,
                notes: existingClient.Notes,
                dataProfile: existingClient.DataProfile,
                createdAt: existingClient.CreatedAt,
                updatedAt: DateTimeOffset.UtcNow
            )
        );
    }

    private async Task UpsertPersonAsync(
        Person? existingPerson,
        string? personId,
        SquareCustomerRow row,
        CustomersImportStats stats,
        CancellationToken cancellationToken,
        bool dryRun
    )
    {
        if (personId is null)
        {
            stats.PeopleSkipped++;
            return;
        }

        if (existingPerson is null)
        {
            stats.PeopleInserted++;
            if (dryRun)
            {
                return;
            }

            var (firstName, lastName, displayName) = ComputePersonNamesForInsert(row);

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

        var desiredFirstName = row.FirstName ?? existingPerson.FirstName;
        var desiredLastName = row.LastName ?? existingPerson.LastName;
        var desiredDisplayName = ComputePersonDisplayName(row) ?? existingPerson.DisplayName;

        var needsUpdate =
            !string.Equals(existingPerson.FirstName, desiredFirstName, StringComparison.Ordinal)
            || !string.Equals(existingPerson.LastName, desiredLastName, StringComparison.Ordinal)
            || !string.Equals(existingPerson.DisplayName, desiredDisplayName, StringComparison.Ordinal);

        if (!needsUpdate)
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
                firstName: desiredFirstName,
                lastName: desiredLastName,
                displayName: desiredDisplayName,
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

    private async Task UpsertPersonContactsAsync(
        DbSet<Dictionary<string, object>> personContacts,
        string? personId,
        SquareCustomerRow row,
        CustomersImportStats stats,
        CancellationToken cancellationToken,
        bool dryRun
    )
    {
        if (personId is null)
        {
            stats.PersonContactsSkipped++;
            return;
        }

        var incomingEmails = NormalizeMultiValue(row.EmailAddress, IdentityKeys.NormalizeEmail);
        var incomingPhones = NormalizeMultiValue(row.PhoneNumber, IdentityKeys.NormalizePhone);

        var incomingEmail = incomingEmails.Count == 0 ? null : incomingEmails[0];
        var incomingPhone = incomingPhones.Count == 0 ? null : incomingPhones[0];

        if (incomingEmail is null && incomingPhone is null)
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
                    ["email"] = incomingEmail!,
                    ["phone"] = incomingPhone!,
                    ["updated_at"] = DateTimeOffset.UtcNow,
                },
                cancellationToken
            );

            return;
        }

        var desiredEmail = incomingEmail ?? GetString(existing, "email");
        var desiredPhone = incomingPhone ?? GetString(existing, "phone");

        var needsUpdate =
            !string.Equals(GetString(existing, "email"), desiredEmail, StringComparison.Ordinal)
            || !string.Equals(GetString(existing, "phone"), desiredPhone, StringComparison.Ordinal);

        if (!needsUpdate)
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

    private async Task UpsertClientContactsAsync(
        DbSet<Dictionary<string, object>> clientContacts,
        string clientId,
        string? personId,
        bool isPrimary,
        CustomersImportStats stats,
        CancellationToken cancellationToken,
        bool dryRun
    )
    {
        if (personId is null)
        {
            stats.ClientContactsSkipped++;
            return;
        }

        var existing = await (dryRun ? clientContacts.AsNoTracking() : clientContacts)
            .SingleOrDefaultAsync(
                x => EF.Property<string>(x, "client_id") == clientId && EF.Property<string>(x, "person_id") == personId,
                cancellationToken
            );

        if (existing is null)
        {
            stats.ClientContactsInserted++;
            if (dryRun)
            {
                return;
            }

            await clientContacts.AddAsync(
                new Dictionary<string, object>
                {
                    ["client_id"] = clientId,
                    ["person_id"] = personId,
                    ["created_at"] = DateTimeOffset.UtcNow,
                    ["is_primary"] = isPrimary,
                    ["notes"] = null!,
                    // Square exports don't reliably tell us whether a contact is a producer/assistant, so keep this null.
                    ["role_key"] = null!,
                },
                cancellationToken
            );

            return;
        }

        var existingIsPrimary = GetBool(existing, "is_primary");
        if (existingIsPrimary == isPrimary)
        {
            stats.ClientContactsSkipped++;
            return;
        }

        stats.ClientContactsUpdated++;
        if (dryRun)
        {
            return;
        }

        existing["is_primary"] = isPrimary;
    }

    private async Task UpsertClientIntegrationSquareAsync(
        DbSet<Dictionary<string, object>> clientIntegrationsSquare,
        Dictionary<string, object>? existingIntegration,
        string clientId,
        string squareCustomerId,
        SquareCustomerRow row,
        CustomersImportStats stats,
        CancellationToken cancellationToken,
        bool dryRun
    )
    {
        var desiredCreationSourceKey = MapCreationSourceKey(row.CreationSource);
        var desiredTransactionCount = row.TransactionCount;
        var desiredFirstVisitAt = row.FirstVisit;
        var desiredLastVisitAt = row.LastVisit;
        var desiredLifetimeSpendCents = ToDbIntCents(row.LifetimeSpendCents, stats);

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
                    ["creation_source_key"] = desiredCreationSourceKey!,
                    ["currency_code"] = null!,
                    ["first_visit_at"] = desiredFirstVisitAt!,
                    ["last_visit_at"] = desiredLastVisitAt!,
                    ["lifetime_spend_cents"] = desiredLifetimeSpendCents!,
                    ["square_customer_id"] = squareCustomerId,
                    ["transaction_count"] = desiredTransactionCount!,
                    ["updated_at"] = DateTimeOffset.UtcNow,
                },
                cancellationToken
            );

            return;
        }

        var changed = false;
        changed |= SetIfDifferent(existingIntegration, "square_customer_id", squareCustomerId);
        changed |= SetIfDifferentWhenNotNull(existingIntegration, "creation_source_key", desiredCreationSourceKey);
        changed |= SetIfDifferentWhenNotNull(existingIntegration, "first_visit_at", desiredFirstVisitAt);
        changed |= SetIfDifferentWhenNotNull(existingIntegration, "last_visit_at", desiredLastVisitAt);
        changed |= SetIfDifferentWhenNotNull(existingIntegration, "transaction_count", desiredTransactionCount);
        changed |= SetIfDifferentWhenNotNull(existingIntegration, "lifetime_spend_cents", desiredLifetimeSpendCents);

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

    private static string ComputeClientDisplayName(SquareCustomerRow row, string squareCustomerId)
    {
        if (!string.IsNullOrWhiteSpace(row.CompanyName))
        {
            return row.CompanyName.Trim();
        }

        var fullName = ComputeFullName(row.FirstName, row.LastName);
        if (!string.IsNullOrWhiteSpace(fullName))
        {
            return fullName;
        }

        if (!string.IsNullOrWhiteSpace(row.Nickname))
        {
            return row.Nickname.Trim();
        }

        if (!string.IsNullOrWhiteSpace(row.EmailAddress))
        {
            return row.EmailAddress.Trim();
        }

        if (!string.IsNullOrWhiteSpace(row.PhoneNumber))
        {
            return row.PhoneNumber.Trim();
        }

        return $"square_{squareCustomerId}";
    }

    private static (string FirstName, string LastName, string? DisplayName) ComputePersonNamesForInsert(SquareCustomerRow row)
    {
        if (row.FirstName is not null || row.LastName is not null)
        {
            var firstName = row.FirstName ?? "Unknown";
            var lastName = row.LastName ?? "Unknown";
            return (firstName, lastName, ComputePersonDisplayName(row));
        }

        if (!string.IsNullOrWhiteSpace(row.CompanyName))
        {
            return (row.CompanyName.Trim(), "Contact", row.CompanyName.Trim());
        }

        return ("Unknown", "Unknown", null);
    }

    private static string? ComputePersonDisplayName(SquareCustomerRow row)
    {
        if (!string.IsNullOrWhiteSpace(row.CompanyName))
        {
            return row.CompanyName.Trim();
        }

        var fullName = ComputeFullName(row.FirstName, row.LastName);
        if (!string.IsNullOrWhiteSpace(fullName))
        {
            return fullName;
        }

        if (!string.IsNullOrWhiteSpace(row.Nickname))
        {
            return row.Nickname.Trim();
        }

        return null;
    }

    private static string? ComputeFullName(string? firstName, string? lastName)
    {
        if (string.IsNullOrWhiteSpace(firstName) && string.IsNullOrWhiteSpace(lastName))
        {
            return null;
        }

        return $"{firstName} {lastName}".Trim();
    }

    private static string? MapCreationSourceKey(string? rawCreationSource)
    {
        if (string.IsNullOrWhiteSpace(rawCreationSource))
        {
            return null;
        }

        var value = rawCreationSource.Trim();
        return value switch
        {
            "Import" => "import",
            "Directory" => "directory",
            "Third Party" => "third_party",
            "Merge" => "merge",
            "Manual" => "manual",
            "Instant Profile" => "instant_profile",
            _ => value.Trim().ToLowerInvariant() switch
            {
                "import" => "import",
                "directory" => "directory",
                "third party" => "third_party",
                "third_party" => "third_party",
                "merge" => "merge",
                "manual" => "manual",
                "instant profile" => "instant_profile",
                "instant_profile" => "instant_profile",
                _ => null,
            },
        };
    }

    private static int? ToDbIntCents(long? cents, CustomersImportStats stats)
    {
        if (cents is null)
        {
            return null;
        }

        if (cents.Value < 0)
        {
            stats.Errors++;
            return null;
        }

        if (cents.Value > int.MaxValue)
        {
            stats.Errors++;
            return null;
        }

        return (int)cents.Value;
    }

    private static bool SetIfDifferent(Dictionary<string, object> row, string key, object value)
    {
        if (row.TryGetValue(key, out var existing) && Equals(existing, value))
        {
            return false;
        }

        row[key] = value;
        return true;
    }

    private static bool SetIfDifferentWhenNotNull(Dictionary<string, object> row, string key, object? value)
    {
        if (value is null)
        {
            return false;
        }

        return SetIfDifferent(row, key, value);
    }

    private static async Task<string?> FindExistingPrimaryContactPersonIdAsync(
        DbSet<Dictionary<string, object>> clientContacts,
        string clientId,
        CancellationToken cancellationToken,
        bool dryRun
    )
    {
        return await (dryRun ? clientContacts.AsNoTracking() : clientContacts)
            .Where(x => EF.Property<string>(x, "client_id") == clientId)
            .OrderByDescending(x => EF.Property<bool>(x, "is_primary"))
            .Select(x => EF.Property<string>(x, "person_id"))
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static IReadOnlyList<(string Email, IReadOnlyList<string> SquareCustomerIds)> FindDuplicateEmails(
        IEnumerable<SquareCustomerRow> rows
    )
    {
        return rows
            .Where(r => !string.IsNullOrWhiteSpace(r.EmailAddress) && !string.IsNullOrWhiteSpace(r.SquareCustomerId))
            .GroupBy(r => r.EmailAddress!, StringComparer.Ordinal)
            .Select(
                g =>
                    (
                        Email: g.Key,
                        SquareCustomerIds: (IReadOnlyList<string>)g
                            .Select(r => r.SquareCustomerId!.Trim())
                            .Distinct(StringComparer.Ordinal)
                            .OrderBy(x => x, StringComparer.Ordinal)
                            .ToList()
                    )
            )
            .Where(x => x.SquareCustomerIds.Count > 1)
            .OrderBy(x => x.Email, StringComparer.Ordinal)
            .ToList();
    }

    private static void WriteDetailedSummary(
        CustomersImportStats stats,
        IReadOnlyList<(string Email, IReadOnlyList<string> SquareCustomerIds)> duplicateEmails
    )
    {
        Console.WriteLine(
            $"square-import customers: clients inserted={stats.ClientsInserted} updated={stats.ClientsUpdated} skipped={stats.ClientsSkipped}"
        );
        Console.WriteLine(
            $"square-import customers: people inserted={stats.PeopleInserted} updated={stats.PeopleUpdated} skipped={stats.PeopleSkipped}"
        );
        Console.WriteLine(
            $"square-import customers: person_contacts inserted={stats.PersonContactsInserted} updated={stats.PersonContactsUpdated} skipped={stats.PersonContactsSkipped}"
        );
        Console.WriteLine(
            $"square-import customers: client_contacts inserted={stats.ClientContactsInserted} updated={stats.ClientContactsUpdated} skipped={stats.ClientContactsSkipped}"
        );
        Console.WriteLine(
            $"square-import customers: client_integrations_square inserted={stats.ClientIntegrationsInserted} updated={stats.ClientIntegrationsUpdated} skipped={stats.ClientIntegrationsSkipped}"
        );
        Console.WriteLine(
            $"square-import customers: duplicate_square_customer_ids_skipped={stats.DuplicateSquareCustomerIdsSkipped}"
        );

        if (duplicateEmails.Count == 0)
        {
            Console.WriteLine("square-import customers: duplicates detected: none.");
            return;
        }

        Console.WriteLine($"square-import customers: duplicates detected (email across square customers)={duplicateEmails.Count}");
        foreach (var dup in duplicateEmails.Take(25))
        {
            Console.WriteLine($"square-import customers: dup-email {dup.Email} => {string.Join(", ", dup.SquareCustomerIds)}");
        }

        if (duplicateEmails.Count > 25)
        {
            Console.WriteLine($"square-import customers: duplicates output truncated (showing 25/{duplicateEmails.Count}).");
        }
    }

    private static string? GetString(Dictionary<string, object> row, string key)
    {
        if (!row.TryGetValue(key, out var value) || value is null || value is DBNull)
        {
            return null;
        }

        return value as string;
    }

    private static bool GetBool(Dictionary<string, object> row, string key)
    {
        if (!row.TryGetValue(key, out var value) || value is null || value is DBNull)
        {
            return false;
        }

        return value is bool b && b;
    }

    private sealed class CustomersImportStats
    {
        public int TotalRows { get; set; }
        public int ClassifiedOrganizations { get; set; }
        public int ClassifiedIndividuals { get; set; }

        public int RowsInserted { get; set; }
        public int RowsUpdated { get; set; }
        public int RowsLinked { get; set; }
        public int RowsSkipped { get; set; }
        public int RowsNeedsReview { get; set; }

        public int AutoLinkedByEmail { get; set; }
        public int AutoLinkedByPhone { get; set; }

        public int HardDuplicateRows { get; set; }
        public int SoftDuplicateRows { get; set; }

        public int ClientsInserted { get; set; }
        public int ClientsUpdated { get; set; }
        public int ClientsSkipped { get; set; }

        public int PeopleInserted { get; set; }
        public int PeopleUpdated { get; set; }
        public int PeopleSkipped { get; set; }

        public int PersonContactsInserted { get; set; }
        public int PersonContactsUpdated { get; set; }
        public int PersonContactsSkipped { get; set; }

        public int ClientContactsInserted { get; set; }
        public int ClientContactsUpdated { get; set; }
        public int ClientContactsSkipped { get; set; }

        public int ClientIntegrationsInserted { get; set; }
        public int ClientIntegrationsUpdated { get; set; }
        public int ClientIntegrationsSkipped { get; set; }

        public int DuplicateSquareCustomerIdsSkipped { get; set; }
        public int Errors { get; set; }

        public int TotalInserted =>
            ClientsInserted + PeopleInserted + PersonContactsInserted + ClientContactsInserted + ClientIntegrationsInserted;

        public int TotalUpdated =>
            ClientsUpdated + PeopleUpdated + PersonContactsUpdated + ClientContactsUpdated + ClientIntegrationsUpdated;

        public int TotalSkipped =>
            ClientsSkipped + PeopleSkipped + PersonContactsSkipped + ClientContactsSkipped + ClientIntegrationsSkipped;
    }
}
