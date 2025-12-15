namespace MGF.Tools.SquareImport.Importers;

using Microsoft.EntityFrameworkCore;
using MGF.Domain.Entities;
using MGF.Tools.SquareImport.Normalization;
using MGF.Tools.SquareImport.Reporting;

internal sealed partial class CustomersImporter
{
    private async Task ProcessBatchAsync(
        IReadOnlyList<CustomerIdentity> batch,
        CustomersImportOptions options,
        HardDuplicates hardDuplicates,
        SoftDuplicates softDuplicates,
        CustomersImportStats stats,
        CsvReportWriter<CustomersImportReportRow>? hardDuplicatesWriter,
        CsvReportWriter<CustomersImportReportRow>? softDuplicatesWriter,
        CsvReportWriter<CustomersImportReportRow>? needsReviewWriter,
        CsvReportWriter<CustomersImportReportRow>? appliedWriter,
        CancellationToken cancellationToken,
        bool dryRun
    )
    {
        var clientIntegrationsSquare = db.Set<Dictionary<string, object>>("client_integrations_square");
        var personContacts = db.Set<Dictionary<string, object>>("person_contacts");
        var clientContacts = db.Set<Dictionary<string, object>>("client_contacts");

        var squareCustomerIds = batch
            .Select(x => x.SquareCustomerId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var existingIntegrationsBySquareCustomerId = squareCustomerIds.Length == 0
            ? new Dictionary<string, Dictionary<string, object>>(StringComparer.Ordinal)
            : (
                await (dryRun ? clientIntegrationsSquare.AsNoTracking() : clientIntegrationsSquare)
                    .Where(
                        cis =>
                            EF.Property<string?>(cis, "square_customer_id") != null
                            && squareCustomerIds.Contains(EF.Property<string?>(cis, "square_customer_id")!)
                    )
                    .ToListAsync(cancellationToken)
            )
            .Where(cis => !string.IsNullOrWhiteSpace(GetString(cis, "square_customer_id")))
            .GroupBy(cis => GetString(cis, "square_customer_id")!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var autoLinkIndex = await BuildAutoLinkIndexAsync(
            batch,
            options,
            personContacts,
            clientContacts,
            clientIntegrationsSquare,
            cancellationToken
        );

        foreach (var identity in batch.OrderBy(x => x.Row.RowNumber).ThenBy(x => x.SquareCustomerId, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await ProcessIdentityAsync(
                    identity,
                    options,
                    hardDuplicates,
                    softDuplicates,
                    existingIntegrationsBySquareCustomerId,
                    autoLinkIndex,
                    stats,
                    clientIntegrationsSquare,
                    personContacts,
                    clientContacts,
                    hardDuplicatesWriter,
                    softDuplicatesWriter,
                    needsReviewWriter,
                    appliedWriter,
                    cancellationToken,
                    dryRun
                );
            }
            catch (Exception ex)
            {
                stats.Errors++;
                Console.Error.WriteLine(
                    $"square-import customers: error importing square_customer_id={identity.SquareCustomerId} (row={identity.Row.RowNumber} file={identity.SourceFileName}): {ex.Message}"
                );
                throw;
            }
        }
    }

    private sealed record AutoLinkIndex(
        IReadOnlyDictionary<string, IReadOnlyList<string>> ClientIdsByEmail,
        IReadOnlyDictionary<string, IReadOnlyList<string>> ClientIdsByPhone,
        IReadOnlyDictionary<string, Client> ClientsById,
        IReadOnlyDictionary<string, string?> ExistingSquareCustomerIdByClientId
    );

    private async Task<AutoLinkIndex> BuildAutoLinkIndexAsync(
        IReadOnlyList<CustomerIdentity> batch,
        CustomersImportOptions options,
        DbSet<Dictionary<string, object>> personContacts,
        DbSet<Dictionary<string, object>> clientContacts,
        DbSet<Dictionary<string, object>> clientIntegrationsSquare,
        CancellationToken cancellationToken
    )
    {
        if (options.MinConfidenceToAutoLink == CustomersMinConfidenceToAutoLink.None)
        {
            return new AutoLinkIndex(
                ClientIdsByEmail: new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal),
                ClientIdsByPhone: new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal),
                ClientsById: new Dictionary<string, Client>(StringComparer.Ordinal),
                ExistingSquareCustomerIdByClientId: new Dictionary<string, string?>(StringComparer.Ordinal)
            );
        }

        var emails = options.MinConfidenceToAutoLink is CustomersMinConfidenceToAutoLink.EmailOnly or CustomersMinConfidenceToAutoLink.EmailOrPhone
            ? batch.SelectMany(x => x.NormalizedEmails).Distinct(StringComparer.Ordinal).ToArray()
            : Array.Empty<string>();

        var phones = options.MinConfidenceToAutoLink is CustomersMinConfidenceToAutoLink.PhoneOnly or CustomersMinConfidenceToAutoLink.EmailOrPhone
            ? batch.SelectMany(x => x.NormalizedPhones).Distinct(StringComparer.Ordinal).ToArray()
            : Array.Empty<string>();

        if (emails.Length == 0 && phones.Length == 0)
        {
            return new AutoLinkIndex(
                ClientIdsByEmail: new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal),
                ClientIdsByPhone: new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal),
                ClientsById: new Dictionary<string, Client>(StringComparer.Ordinal),
                ExistingSquareCustomerIdByClientId: new Dictionary<string, string?>(StringComparer.Ordinal)
            );
        }

        var matchedPersonIds = new HashSet<string>(StringComparer.Ordinal);
        var matchedEmailsByPersonId = new Dictionary<string, string>(StringComparer.Ordinal);
        var matchedPhonesByPersonId = new Dictionary<string, string>(StringComparer.Ordinal);

        if (emails.Length > 0)
        {
            var emailMatches = await personContacts.AsNoTracking()
                .Where(pc => EF.Property<string?>(pc, "email") != null)
                .Where(pc => emails.Contains(EF.Property<string?>(pc, "email")!.ToLower()))
                .Select(
                    pc =>
                        new
                        {
                            PersonId = EF.Property<string>(pc, "person_id"),
                            Email = EF.Property<string?>(pc, "email"),
                        }
                )
                .ToListAsync(cancellationToken);

            foreach (var match in emailMatches)
            {
                if (string.IsNullOrWhiteSpace(match.PersonId))
                {
                    continue;
                }

                matchedPersonIds.Add(match.PersonId);
                if (!string.IsNullOrWhiteSpace(match.Email))
                {
                    matchedEmailsByPersonId[match.PersonId] = match.Email!;
                }
            }
        }

        if (phones.Length > 0)
        {
            var phoneMatches = await personContacts.AsNoTracking()
                .Where(pc => EF.Property<string?>(pc, "phone") != null)
                .Where(pc => phones.Contains(EF.Property<string?>(pc, "phone")!))
                .Select(
                    pc =>
                        new
                        {
                            PersonId = EF.Property<string>(pc, "person_id"),
                            Phone = EF.Property<string?>(pc, "phone"),
                        }
                )
                .ToListAsync(cancellationToken);

            foreach (var match in phoneMatches)
            {
                if (string.IsNullOrWhiteSpace(match.PersonId))
                {
                    continue;
                }

                matchedPersonIds.Add(match.PersonId);
                if (!string.IsNullOrWhiteSpace(match.Phone))
                {
                    matchedPhonesByPersonId[match.PersonId] = match.Phone!;
                }
            }
        }

        if (matchedPersonIds.Count == 0)
        {
            return new AutoLinkIndex(
                ClientIdsByEmail: new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal),
                ClientIdsByPhone: new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal),
                ClientsById: new Dictionary<string, Client>(StringComparer.Ordinal),
                ExistingSquareCustomerIdByClientId: new Dictionary<string, string?>(StringComparer.Ordinal)
            );
        }

        var personIdsArray = matchedPersonIds.OrderBy(x => x, StringComparer.Ordinal).ToArray();

        var clientIdsByPersonId = await clientContacts.AsNoTracking()
            .Where(cc => personIdsArray.Contains(EF.Property<string>(cc, "person_id")))
            .Select(
                cc =>
                    new
                    {
                        ClientId = EF.Property<string>(cc, "client_id"),
                        PersonId = EF.Property<string>(cc, "person_id"),
                    }
            )
            .ToListAsync(cancellationToken);

        var primaryClientIdsByPersonId = await db.Clients.AsNoTracking()
            .Where(c => c.PrimaryContactPersonId != null && personIdsArray.Contains(c.PrimaryContactPersonId))
            .Select(
                c =>
                    new
                    {
                        c.ClientId,
                        PersonId = c.PrimaryContactPersonId!,
                    }
            )
            .ToListAsync(cancellationToken);

        var allCandidateClientIds = clientIdsByPersonId
            .Select(x => x.ClientId)
            .Concat(primaryClientIdsByPersonId.Select(x => x.ClientId))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

        if (allCandidateClientIds.Length == 0)
        {
            return new AutoLinkIndex(
                ClientIdsByEmail: new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal),
                ClientIdsByPhone: new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal),
                ClientsById: new Dictionary<string, Client>(StringComparer.Ordinal),
                ExistingSquareCustomerIdByClientId: new Dictionary<string, string?>(StringComparer.Ordinal)
            );
        }

        var clients = await db.Clients.AsNoTracking()
            .Where(c => allCandidateClientIds.Contains(c.ClientId))
            .ToListAsync(cancellationToken);

        var clientsById = clients.ToDictionary(c => c.ClientId, StringComparer.Ordinal);

        var integrations = await clientIntegrationsSquare.AsNoTracking()
            .Where(cis => allCandidateClientIds.Contains(EF.Property<string>(cis, "client_id")))
            .Select(
                cis =>
                    new
                    {
                        ClientId = EF.Property<string>(cis, "client_id"),
                        SquareCustomerId = EF.Property<string?>(cis, "square_customer_id"),
                    }
            )
            .ToListAsync(cancellationToken);

        var existingSquareCustomerIdByClientId = integrations
            .GroupBy(x => x.ClientId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(x => x.SquareCustomerId).FirstOrDefault(), StringComparer.Ordinal);

        var clientIdsByEmail = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var kvp in matchedEmailsByPersonId)
        {
            var normalizedEmail = IdentityKeys.NormalizeEmail(kvp.Value);
            if (normalizedEmail is null)
            {
                continue;
            }

            var personId = kvp.Key;
            var ids = clientIdsByPersonId.Where(x => x.PersonId == personId).Select(x => x.ClientId)
                .Concat(primaryClientIdsByPersonId.Where(x => x.PersonId == personId).Select(x => x.ClientId))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            if (ids.Length == 0)
            {
                continue;
            }

            if (!clientIdsByEmail.TryGetValue(normalizedEmail, out var set))
            {
                set = new HashSet<string>(StringComparer.Ordinal);
                clientIdsByEmail[normalizedEmail] = set;
            }

            foreach (var id in ids)
            {
                set.Add(id);
            }
        }

        var clientIdsByPhone = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var kvp in matchedPhonesByPersonId)
        {
            var normalizedPhone = IdentityKeys.NormalizePhone(kvp.Value);
            if (normalizedPhone is null)
            {
                continue;
            }

            var personId = kvp.Key;
            var ids = clientIdsByPersonId.Where(x => x.PersonId == personId).Select(x => x.ClientId)
                .Concat(primaryClientIdsByPersonId.Where(x => x.PersonId == personId).Select(x => x.ClientId))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            if (ids.Length == 0)
            {
                continue;
            }

            if (!clientIdsByPhone.TryGetValue(normalizedPhone, out var set))
            {
                set = new HashSet<string>(StringComparer.Ordinal);
                clientIdsByPhone[normalizedPhone] = set;
            }

            foreach (var id in ids)
            {
                set.Add(id);
            }
        }

        return new AutoLinkIndex(
            ClientIdsByEmail: clientIdsByEmail.ToDictionary(
                kvp => kvp.Key,
                kvp => (IReadOnlyList<string>)kvp.Value.OrderBy(x => x, StringComparer.Ordinal).ToList(),
                StringComparer.Ordinal
            ),
            ClientIdsByPhone: clientIdsByPhone.ToDictionary(
                kvp => kvp.Key,
                kvp => (IReadOnlyList<string>)kvp.Value.OrderBy(x => x, StringComparer.Ordinal).ToList(),
                StringComparer.Ordinal
            ),
            ClientsById: clientsById,
            ExistingSquareCustomerIdByClientId: existingSquareCustomerIdByClientId
        );
    }

    private async Task ProcessIdentityAsync(
        CustomerIdentity identity,
        CustomersImportOptions options,
        HardDuplicates hardDuplicates,
        SoftDuplicates softDuplicates,
        IReadOnlyDictionary<string, Dictionary<string, object>> existingIntegrationsBySquareCustomerId,
        AutoLinkIndex autoLinkIndex,
        CustomersImportStats stats,
        DbSet<Dictionary<string, object>> clientIntegrationsSquare,
        DbSet<Dictionary<string, object>> personContacts,
        DbSet<Dictionary<string, object>> clientContacts,
        CsvReportWriter<CustomersImportReportRow>? hardDuplicatesWriter,
        CsvReportWriter<CustomersImportReportRow>? softDuplicatesWriter,
        CsvReportWriter<CustomersImportReportRow>? needsReviewWriter,
        CsvReportWriter<CustomersImportReportRow>? appliedWriter,
        CancellationToken cancellationToken,
        bool dryRun
    )
    {
        var squareCustomerId = identity.SquareCustomerId;

        existingIntegrationsBySquareCustomerId.TryGetValue(squareCustomerId, out var existingIntegration);

        hardDuplicates.BySquareCustomerId.TryGetValue(squareCustomerId, out var hardDupeInfo);
        softDuplicates.BySquareCustomerId.TryGetValue(squareCustomerId, out var softDupeInfo);

        // Skip non-primary hard-duplicate rows unless they're already imported.
        if (existingIntegration is null && hardDupeInfo is not null && !hardDupeInfo.IsPrimary)
        {
            stats.RowsSkipped++;
            stats.RowsNeedsReview++;

            WriteReportRows(
                identity,
                proposedAction: "skip",
                matchedClientId: null,
                matchedPersonId: null,
                resolution: "needs_review",
                notes: $"hard_duplicate_input primary_square_customer_id={hardDupeInfo.PrimarySquareCustomerId}",
                hardDupeInfo,
                softDupeInfo,
                hardDuplicatesWriter,
                softDuplicatesWriter,
                needsReviewWriter,
                appliedWriter
            );

            return;
        }

        if (existingIntegration is not null)
        {
            await UpdateExistingSquareMappingAsync(
                identity,
                existingIntegration,
                clientIntegrationsSquare,
                personContacts,
                clientContacts,
                stats,
                hardDupeInfo,
                softDupeInfo,
                hardDuplicatesWriter,
                softDuplicatesWriter,
                needsReviewWriter,
                appliedWriter,
                cancellationToken,
                dryRun
            );
            return;
        }

        await InsertOrLinkAsync(
            identity,
            options,
            autoLinkIndex,
            clientIntegrationsSquare,
            personContacts,
            clientContacts,
            stats,
            hardDupeInfo,
            softDupeInfo,
            hardDuplicatesWriter,
            softDuplicatesWriter,
            needsReviewWriter,
            appliedWriter,
            cancellationToken,
            dryRun
        );
    }

    private async Task UpdateExistingSquareMappingAsync(
        CustomerIdentity identity,
        Dictionary<string, object> existingIntegration,
        DbSet<Dictionary<string, object>> clientIntegrationsSquare,
        DbSet<Dictionary<string, object>> personContacts,
        DbSet<Dictionary<string, object>> clientContacts,
        CustomersImportStats stats,
        HardDuplicateRowInfo? hardDupeInfo,
        SoftDuplicateRowInfo? softDupeInfo,
        CsvReportWriter<CustomersImportReportRow>? hardDuplicatesWriter,
        CsvReportWriter<CustomersImportReportRow>? softDuplicatesWriter,
        CsvReportWriter<CustomersImportReportRow>? needsReviewWriter,
        CsvReportWriter<CustomersImportReportRow>? appliedWriter,
        CancellationToken cancellationToken,
        bool dryRun
    )
    {
        var existingClientId = GetString(existingIntegration, "client_id");
        if (string.IsNullOrWhiteSpace(existingClientId))
        {
            stats.Errors++;
            WriteReportRows(
                identity,
                proposedAction: "skip",
                matchedClientId: null,
                matchedPersonId: null,
                resolution: "error",
                notes: "integration row missing client_id",
                hardDupeInfo,
                softDupeInfo,
                hardDuplicatesWriter,
                softDuplicatesWriter,
                needsReviewWriter,
                appliedWriter
            );
            return;
        }

        var existingClient = await db.Clients.AsNoTracking()
            .SingleOrDefaultAsync(c => c.ClientId == existingClientId, cancellationToken);

        if (existingClient is null)
        {
            stats.Errors++;
            WriteReportRows(
                identity,
                proposedAction: "skip",
                matchedClientId: existingClientId,
                matchedPersonId: null,
                resolution: "error",
                notes: "integration maps to missing client",
                hardDupeInfo,
                softDupeInfo,
                hardDuplicatesWriter,
                softDuplicatesWriter,
                needsReviewWriter,
                appliedWriter
            );
            return;
        }

        var personId = await ResolvePersonIdAsync(identity, existingClient, clientContacts, cancellationToken, dryRun);
        var existingPerson = personId is null
            ? null
            : await db.People.AsNoTracking().SingleOrDefaultAsync(p => p.PersonId == personId, cancellationToken);

        var beforeInserted = stats.TotalInserted;
        var beforeUpdated = stats.TotalUpdated;

        await ApplyUpsertsAsync(
            identity,
            existingClient,
            clientId: existingClient.ClientId,
            personId,
            existingPerson,
            existingIntegration,
            clientIntegrationsSquare,
            personContacts,
            clientContacts,
            stats,
            cancellationToken,
            dryRun
        );

        var didChange = stats.TotalInserted != beforeInserted || stats.TotalUpdated != beforeUpdated;
        if (didChange)
        {
            stats.RowsUpdated++;
        }
        else
        {
            stats.RowsSkipped++;
        }

        WriteReportRows(
            identity,
            proposedAction: didChange ? "update" : "skip",
            matchedClientId: existingClient.ClientId,
            matchedPersonId: personId,
            resolution: "existing_square_customer_id",
            notes: didChange ? "updated existing mapping" : "no changes (already up to date)",
            hardDupeInfo,
            softDupeInfo,
            hardDuplicatesWriter,
            softDuplicatesWriter,
            needsReviewWriter,
            appliedWriter
        );
    }

    private async Task InsertOrLinkAsync(
        CustomerIdentity identity,
        CustomersImportOptions options,
        AutoLinkIndex autoLinkIndex,
        DbSet<Dictionary<string, object>> clientIntegrationsSquare,
        DbSet<Dictionary<string, object>> personContacts,
        DbSet<Dictionary<string, object>> clientContacts,
        CustomersImportStats stats,
        HardDuplicateRowInfo? hardDupeInfo,
        SoftDuplicateRowInfo? softDupeInfo,
        CsvReportWriter<CustomersImportReportRow>? hardDuplicatesWriter,
        CsvReportWriter<CustomersImportReportRow>? softDuplicatesWriter,
        CsvReportWriter<CustomersImportReportRow>? needsReviewWriter,
        CsvReportWriter<CustomersImportReportRow>? appliedWriter,
        CancellationToken cancellationToken,
        bool dryRun
    )
    {
        var autoLink = TrySelectAutoLinkClient(identity, options, autoLinkIndex);

        if (autoLink.Decision == AutoLinkDecision.Ambiguous)
        {
            stats.RowsSkipped++;
            stats.RowsNeedsReview++;
            if (options.Strict)
            {
                stats.Errors++;
            }

            WriteReportRows(
                identity,
                proposedAction: "skip",
                matchedClientId: null,
                matchedPersonId: null,
                resolution: "needs_review",
                notes: autoLink.Notes,
                hardDupeInfo,
                softDupeInfo,
                hardDuplicatesWriter,
                softDuplicatesWriter,
                needsReviewWriter,
                appliedWriter
            );
            return;
        }

        if (autoLink.Decision == AutoLinkDecision.IneligibleExistingIntegration)
        {
            stats.RowsSkipped++;
            stats.RowsNeedsReview++;

            WriteReportRows(
                identity,
                proposedAction: "skip",
                matchedClientId: autoLink.ClientId,
                matchedPersonId: null,
                resolution: "needs_review",
                notes: autoLink.Notes,
                hardDupeInfo,
                softDupeInfo,
                hardDuplicatesWriter,
                softDuplicatesWriter,
                needsReviewWriter,
                appliedWriter
            );
            return;
        }

        if (autoLink.Decision == AutoLinkDecision.Linked && autoLink.ClientId is not null)
        {
            var clientId = autoLink.ClientId;
            if (!autoLinkIndex.ClientsById.TryGetValue(clientId, out var existingClient))
            {
                stats.Errors++;
                WriteReportRows(
                    identity,
                    proposedAction: "skip",
                    matchedClientId: clientId,
                    matchedPersonId: null,
                    resolution: "error",
                    notes: "auto-link candidate missing from cache",
                    hardDupeInfo,
                    softDupeInfo,
                    hardDuplicatesWriter,
                    softDuplicatesWriter,
                    needsReviewWriter,
                    appliedWriter
                );
                return;
            }

            var personId = await ResolvePersonIdAsync(identity, existingClient, clientContacts, cancellationToken, dryRun);
            var existingPerson = personId is null
                ? null
                : await db.People.AsNoTracking().SingleOrDefaultAsync(p => p.PersonId == personId, cancellationToken);

            var existingIntegrationForClient = await (dryRun ? clientIntegrationsSquare.AsNoTracking() : clientIntegrationsSquare)
                .SingleOrDefaultAsync(cis => EF.Property<string>(cis, "client_id") == clientId, cancellationToken);

            await ApplyUpsertsAsync(
                identity,
                existingClient,
                clientId,
                personId,
                existingPerson,
                existingIntegrationForClient,
                clientIntegrationsSquare,
                personContacts,
                clientContacts,
                stats,
                cancellationToken,
                dryRun
            );

            stats.RowsLinked++;
            if (autoLink.Resolution == "auto_linked_by_email")
            {
                stats.AutoLinkedByEmail++;
            }
            else if (autoLink.Resolution == "auto_linked_by_phone")
            {
                stats.AutoLinkedByPhone++;
            }

            WriteReportRows(
                identity,
                proposedAction: "link",
                matchedClientId: clientId,
                matchedPersonId: personId,
                resolution: autoLink.Resolution,
                notes: autoLink.Notes,
                hardDupeInfo,
                softDupeInfo,
                hardDuplicatesWriter,
                softDuplicatesWriter,
                needsReviewWriter,
                appliedWriter
            );
            return;
        }

        var newClientId = EntityIds.NewClientId();
        var newPersonId = identity.ShouldCreatePerson ? EntityIds.NewPersonId() : null;

        await ApplyUpsertsAsync(
            identity,
            existingClient: null,
            clientId: newClientId,
            personId: newPersonId,
            existingPerson: null,
            existingIntegration: null,
            clientIntegrationsSquare,
            personContacts,
            clientContacts,
            stats,
            cancellationToken,
            dryRun
        );

        stats.RowsInserted++;

        WriteReportRows(
            identity,
            proposedAction: "insert",
            matchedClientId: newClientId,
            matchedPersonId: newPersonId,
            resolution: "inserted_new",
            notes: "created new client/person",
            hardDupeInfo,
            softDupeInfo,
            hardDuplicatesWriter,
            softDuplicatesWriter,
            needsReviewWriter,
            appliedWriter
        );
    }

    private enum AutoLinkDecision
    {
        None = 0,
        Linked = 1,
        Ambiguous = 2,
        IneligibleExistingIntegration = 3,
    }

    private sealed record AutoLinkSelection(AutoLinkDecision Decision, string? ClientId, string Resolution, string Notes);

    private static AutoLinkSelection TrySelectAutoLinkClient(
        CustomerIdentity identity,
        CustomersImportOptions options,
        AutoLinkIndex autoLinkIndex
    )
    {
        if (options.MinConfidenceToAutoLink == CustomersMinConfidenceToAutoLink.None)
        {
            return new AutoLinkSelection(AutoLinkDecision.None, null, "no_auto_link", "auto-link disabled");
        }

        var emailCandidates = new HashSet<string>(StringComparer.Ordinal);
        if (options.MinConfidenceToAutoLink is CustomersMinConfidenceToAutoLink.EmailOnly or CustomersMinConfidenceToAutoLink.EmailOrPhone)
        {
            foreach (var email in identity.NormalizedEmails)
            {
                if (autoLinkIndex.ClientIdsByEmail.TryGetValue(email, out var ids))
                {
                    foreach (var id in ids)
                    {
                        emailCandidates.Add(id);
                    }
                }
            }
        }

        if (emailCandidates.Count > 0)
        {
            return SelectFromCandidates(identity, emailCandidates, autoLinkIndex, resolution: "auto_linked_by_email");
        }

        var phoneCandidates = new HashSet<string>(StringComparer.Ordinal);
        if (options.MinConfidenceToAutoLink is CustomersMinConfidenceToAutoLink.PhoneOnly or CustomersMinConfidenceToAutoLink.EmailOrPhone)
        {
            foreach (var phone in identity.NormalizedPhones)
            {
                if (autoLinkIndex.ClientIdsByPhone.TryGetValue(phone, out var ids))
                {
                    foreach (var id in ids)
                    {
                        phoneCandidates.Add(id);
                    }
                }
            }
        }

        if (phoneCandidates.Count > 0)
        {
            return SelectFromCandidates(identity, phoneCandidates, autoLinkIndex, resolution: "auto_linked_by_phone");
        }

        return new AutoLinkSelection(AutoLinkDecision.None, null, "no_match", "no matching email/phone found");
    }

    private static AutoLinkSelection SelectFromCandidates(
        CustomerIdentity identity,
        HashSet<string> candidates,
        AutoLinkIndex autoLinkIndex,
        string resolution
    )
    {
        var eligible = candidates
            .Where(id => autoLinkIndex.ClientsById.ContainsKey(id))
            .Where(id => IsEligibleForAutoLink(id, autoLinkIndex.ExistingSquareCustomerIdByClientId))
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        if (eligible.Count == 0)
        {
            var ineligible = candidates
                .Where(id => autoLinkIndex.ExistingSquareCustomerIdByClientId.TryGetValue(id, out var sq) && !string.IsNullOrWhiteSpace(sq))
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToList();

            if (ineligible.Count > 0)
            {
                return new AutoLinkSelection(
                    AutoLinkDecision.IneligibleExistingIntegration,
                    ineligible[0],
                    resolution,
                    $"match found but client already has square_customer_id={autoLinkIndex.ExistingSquareCustomerIdByClientId[ineligible[0]]}"
                );
            }

            return new AutoLinkSelection(AutoLinkDecision.None, null, resolution, "no eligible clients found");
        }

        if (eligible.Count == 1)
        {
            return new AutoLinkSelection(AutoLinkDecision.Linked, eligible[0], resolution, $"linked by {resolution}");
        }

        var ranked = eligible
            .Select(
                id =>
                    new
                    {
                        ClientId = id,
                        Client = autoLinkIndex.ClientsById[id],
                        Score = ComputeClientCompletenessScore(autoLinkIndex.ClientsById[id]),
                        Recency = autoLinkIndex.ClientsById[id].UpdatedAt ?? autoLinkIndex.ClientsById[id].CreatedAt,
                    }
            )
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Recency)
            .ThenBy(x => x.ClientId, StringComparer.Ordinal)
            .ToList();

        var best = ranked[0];
        if (ranked.Count > 1)
        {
            var second = ranked[1];
            if (second.Score == best.Score && second.Recency == best.Recency)
            {
                var topIds = ranked.Take(5).Select(x => x.ClientId).ToArray();
                return new AutoLinkSelection(
                    AutoLinkDecision.Ambiguous,
                    null,
                    resolution,
                    $"ambiguous match for {identity.SquareCustomerId} candidates={string.Join(",", topIds)}"
                );
            }
        }

        return new AutoLinkSelection(AutoLinkDecision.Linked, best.ClientId, resolution, $"linked to client_id={best.ClientId}");
    }

    private static bool IsEligibleForAutoLink(string clientId, IReadOnlyDictionary<string, string?> existingSquareCustomerIdByClientId)
    {
        if (!existingSquareCustomerIdByClientId.TryGetValue(clientId, out var existingSq))
        {
            return true;
        }

        return string.IsNullOrWhiteSpace(existingSq);
    }

    private static int ComputeClientCompletenessScore(Client client)
    {
        var score = 0;

        if (!LooksLikePlaceholderDisplayName(client.DisplayName))
        {
            score += 1;
        }

        if (client.PrimaryContactPersonId is not null)
        {
            score += 2;
        }

        if (client.AccountOwnerPersonId is not null)
        {
            score += 1;
        }

        return score;
    }

    private static bool LooksLikePlaceholderDisplayName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var normalized = value.Trim();
        if (normalized.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (normalized.Equals("Customer", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (normalized.StartsWith("square_", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static bool ShouldOverwriteDisplayName(string? existing, string incoming)
    {
        if (string.IsNullOrWhiteSpace(incoming))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(existing))
        {
            return true;
        }

        return LooksLikePlaceholderDisplayName(existing);
    }

    private async Task<string?> ResolvePersonIdAsync(
        CustomerIdentity identity,
        Client existingClient,
        DbSet<Dictionary<string, object>> clientContacts,
        CancellationToken cancellationToken,
        bool dryRun
    )
    {
        if (!identity.ShouldCreatePerson)
        {
            return null;
        }

        if (existingClient.PrimaryContactPersonId is not null)
        {
            return existingClient.PrimaryContactPersonId;
        }

        var personId = await FindExistingPrimaryContactPersonIdAsync(clientContacts, existingClient.ClientId, cancellationToken, dryRun);
        if (!string.IsNullOrWhiteSpace(personId))
        {
            return personId;
        }

        return EntityIds.NewPersonId();
    }

    private async Task ApplyUpsertsAsync(
        CustomerIdentity identity,
        Client? existingClient,
        string clientId,
        string? personId,
        Person? existingPerson,
        Dictionary<string, object>? existingIntegration,
        DbSet<Dictionary<string, object>> clientIntegrationsSquare,
        DbSet<Dictionary<string, object>> personContacts,
        DbSet<Dictionary<string, object>> clientContacts,
        CustomersImportStats stats,
        CancellationToken cancellationToken,
        bool dryRun
    )
    {
        var primaryContactPersonId = existingClient?.PrimaryContactPersonId ?? personId;

        await UpsertPersonAsync(existingPerson, personId, identity.Row, stats, cancellationToken, dryRun);

        await UpsertClientAsync(
            existingClient,
            clientId,
            identity.DisplayName,
            identity.ProposedClientTypeKey,
            primaryContactPersonId,
            stats,
            cancellationToken,
            dryRun
        );

        await UpsertPersonContactsAsync(personContacts, personId, identity.Row, stats, cancellationToken, dryRun);
        await UpsertClientContactsAsync(
            clientContacts,
            clientId,
            personId,
            isPrimary: primaryContactPersonId is not null && primaryContactPersonId == personId,
            stats,
            cancellationToken,
            dryRun
        );

        await UpsertClientIntegrationSquareAsync(
            clientIntegrationsSquare,
            existingIntegration,
            clientId,
            identity.SquareCustomerId,
            identity.Row,
            stats,
            cancellationToken,
            dryRun
        );
    }

    private static void WriteReportRows(
        CustomerIdentity identity,
        string proposedAction,
        string? matchedClientId,
        string? matchedPersonId,
        string resolution,
        string notes,
        HardDuplicateRowInfo? hardDupeInfo,
        SoftDuplicateRowInfo? softDupeInfo,
        CsvReportWriter<CustomersImportReportRow>? hardDuplicatesWriter,
        CsvReportWriter<CustomersImportReportRow>? softDuplicatesWriter,
        CsvReportWriter<CustomersImportReportRow>? needsReviewWriter,
        CsvReportWriter<CustomersImportReportRow>? appliedWriter
    )
    {
        var reportRow = new CustomersImportReportRow(
            SquareCustomerId: identity.SquareCustomerId,
            DisplayName: identity.DisplayName,
            NormalizedEmail: identity.PrimaryNormalizedEmail,
            NormalizedPhone: identity.PrimaryNormalizedPhone,
            ProposedClientTypeKey: identity.ProposedClientTypeKey,
            ClassificationReason: identity.ClassificationReason,
            ProposedAction: proposedAction,
            MatchedClientId: matchedClientId,
            MatchedPersonId: matchedPersonId,
            Resolution: resolution,
            Notes: notes,
            SourceFile: identity.SourceFileName,
            RowNumber: identity.Row.RowNumber
        );

        if (hardDupeInfo is not null)
        {
            hardDuplicatesWriter?.Write(
                reportRow with
                {
                    Notes =
                        $"{reportRow.Notes}; hard_duplicate_primary={hardDupeInfo.PrimarySquareCustomerId}; dup_emails={string.Join("|", hardDupeInfo.DuplicateEmails)}; dup_phones={string.Join("|", hardDupeInfo.DuplicatePhones)}",
                }
            );
        }

        if (softDupeInfo is not null)
        {
            var softResolution = reportRow.Resolution == "needs_review" ? "needs_review" : "needs_review_soft_duplicate";
            softDuplicatesWriter?.Write(
                reportRow with
                {
                    Resolution = softResolution,
                    Notes = $"{reportRow.Notes}; soft_duplicate_reasons={string.Join("|", softDupeInfo.Reasons)}",
                }
            );
        }

        var needsReview =
            reportRow.Resolution.StartsWith("needs_review", StringComparison.Ordinal)
            || reportRow.Resolution.Equals("error", StringComparison.Ordinal);

        if (needsReview)
        {
            needsReviewWriter?.Write(reportRow);
        }
        else
        {
            appliedWriter?.Write(reportRow);
        }
    }
}
