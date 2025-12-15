namespace MGF.Tools.SquareImport.Importers;

using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using MGF.Domain.Entities;
using MGF.Infrastructure.Data;
using MGF.Tools.SquareImport.Parsing;
using MGF.Tools.SquareImport.Reporting;

internal sealed class TransactionsImporter
{
    private const int BatchSize = 1000;
    private const string DefaultCurrencyCode = "USD";
    private const string SquareProcessorKey = "square";
    private const string SquarePaymentMethodKey = "square";
    private const string CodePrefix = "MGF";
    private const string LedgerProjectName = "Square Transactions (Imported)";

    private const string UnmatchedClientDisplayName = "UNMATCHED: Square Transactions";
    private const string UnmatchedClientNotesMarker = "[system:unmatched_square]";
    private const string UnmatchedSquareCustomerIdSentinel = "__unmatched_square__";
    private const string UnmatchedClientIdPlaceholder = "cli_unmatched_placeholder";

    private readonly AppDbContext db;
    private string? unmatchedClientIdCache;
    private readonly Dictionary<string, string> ledgerProjectIdCache = new(StringComparer.Ordinal);

    public TransactionsImporter(AppDbContext db)
    {
        this.db = db;
    }

    public async Task<ImportSummary> ImportAsync(
        string[] filePaths,
        bool dryRun,
        string? unmatchedReportPath,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(filePaths);

        using var unmatchedReportWriter = CreateUnmatchedReportWriter(unmatchedReportPath);
        if (unmatchedReportWriter is not null)
        {
            WriteCsvRow(
                unmatchedReportWriter,
                "transaction_id",
                "raw_customer_id",
                "customer_name",
                "amount",
                "occurred_at",
                "source_file",
                "row_number"
            );
        }

        var transactions = CsvLoader.LoadTransactions(filePaths);
        Console.WriteLine($"square-import transactions: parsed rows={transactions.Count} (dry-run={dryRun}).");

        var stats = new TransactionsImportStats();

        var dedupedByTransactionId = new Dictionary<string, SquareTransactionRow>(StringComparer.Ordinal);
        foreach (var row in transactions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(row.TransactionId))
            {
                stats.Errors++;
                continue;
            }

            var transactionId = row.TransactionId.Trim();
            if (!dedupedByTransactionId.TryAdd(transactionId, row))
            {
                stats.DuplicateTransactionIdsSkipped++;
                continue;
            }

            if (row.HasMultipleCustomerIds)
            {
                stats.MultiCustomerIdRows++;

                if (stats.MultiCustomerIdRows <= 10)
                {
                    row.RawFields.TryGetValue("Customer ID", out var rawCustomerId);
                    Console.WriteLine(
                        $"square-import transactions: multi-customer id transaction_id={transactionId} row={row.RowNumber} raw_customer_id={rawCustomerId} chosen_customer_id={row.CustomerId}"
                    );
                }
            }
        }

        var distinctCustomerIdsEncountered = dedupedByTransactionId.Values
            .Select(t => t.CustomerId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!.Trim())
            .Distinct(StringComparer.Ordinal)
            .Count();

        Console.WriteLine(
            $"square-import transactions: distinct_customer_ids_encountered={distinctCustomerIdsEncountered} duplicate_transaction_ids_skipped={stats.DuplicateTransactionIdsSkipped}"
        );

        var rows = dedupedByTransactionId.Values
            .OrderBy(r => r.TransactionAt ?? DateTimeOffset.MinValue)
            .ThenBy(r => r.TransactionId, StringComparer.Ordinal)
            .ToList();

        var unmatchedCustomerIdCounts = new Dictionary<string, int>(StringComparer.Ordinal);

        for (var offset = 0; offset < rows.Count; offset += BatchSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var batch = rows.Skip(offset).Take(BatchSize).ToList();

            if (!dryRun)
            {
                await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
                await ProcessBatchAsync(
                    batch,
                    stats,
                    unmatchedCustomerIdCounts,
                    unmatchedReportWriter,
                    cancellationToken,
                    dryRun
                );
                await db.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);
            }
            else
            {
                await ProcessBatchAsync(
                    batch,
                    stats,
                    unmatchedCustomerIdCounts,
                    unmatchedReportWriter,
                    cancellationToken,
                    dryRun
                );
            }
        }

        WriteDetailedSummary(stats, unmatchedCustomerIdCounts);

        if (unmatchedReportWriter is not null && !string.IsNullOrWhiteSpace(unmatchedReportPath))
        {
            Console.WriteLine(
                $"square-import transactions: unmatched report path={unmatchedReportPath} rows={stats.UnmatchedReportRowsWritten}"
            );
        }

        return new ImportSummary(
            Inserted: stats.TotalInserted,
            Updated: stats.TotalUpdated,
            Skipped: stats.TotalSkipped + stats.DuplicateTransactionIdsSkipped,
            Errors: stats.Errors
        );
    }

    private async Task ProcessBatchAsync(
        IReadOnlyList<SquareTransactionRow> batch,
        TransactionsImportStats stats,
        Dictionary<string, int> unmatchedCustomerIdCounts,
        TextWriter? unmatchedReportWriter,
        CancellationToken cancellationToken,
        bool dryRun
    )
    {
        var now = DateTimeOffset.UtcNow;

        var clientIntegrationsSquare = db.Set<Dictionary<string, object>>("client_integrations_square");
        var invoices = db.Set<Dictionary<string, object>>("invoices");
        var invoiceIntegrationsSquare = db.Set<Dictionary<string, object>>("invoice_integrations_square");
        var payments = db.Set<Dictionary<string, object>>("payments");
        var projectCodeCounters = db.Set<Dictionary<string, object>>("project_code_counters");
        var invoiceNumberCounters = db.Set<Dictionary<string, object>>("invoice_number_counters");

        var transactionIds = batch
            .Select(r => r.TransactionId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var existingPayments = transactionIds.Length == 0
            ? new List<Dictionary<string, object>>()
            : await (dryRun ? payments.AsNoTracking() : payments)
                .Where(
                    p =>
                        EF.Property<string?>(p, "processor_key") == SquareProcessorKey
                        && EF.Property<string?>(p, "processor_payment_id") != null
                        && transactionIds.Contains(EF.Property<string?>(p, "processor_payment_id")!)
                )
                .ToListAsync(cancellationToken);

        var paymentsByExternalId = existingPayments
            .Where(p => !string.IsNullOrWhiteSpace(GetString(p, "processor_payment_id")))
            .GroupBy(p => GetString(p, "processor_payment_id")!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        var invoiceIdsToLoad = existingPayments
            .Select(p => GetString(p, "invoice_id"))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var existingInvoices = invoiceIdsToLoad.Length == 0
            ? new List<Dictionary<string, object>>()
            : await (dryRun ? invoices.AsNoTracking() : invoices)
                .Where(i => invoiceIdsToLoad.Contains(EF.Property<string>(i, "invoice_id")))
                .ToListAsync(cancellationToken);

        var invoicesById = existingInvoices
            .Where(i => !string.IsNullOrWhiteSpace(GetString(i, "invoice_id")))
            .ToDictionary(i => GetString(i, "invoice_id")!, StringComparer.Ordinal);

        var existingInvoiceIntegrations = invoiceIdsToLoad.Length == 0
            ? new List<Dictionary<string, object>>()
            : await (dryRun ? invoiceIntegrationsSquare.AsNoTracking() : invoiceIntegrationsSquare)
                .Where(i => invoiceIdsToLoad.Contains(EF.Property<string>(i, "invoice_id")))
                .ToListAsync(cancellationToken);

        var invoiceIntegrationsByInvoiceId = existingInvoiceIntegrations
            .Where(i => !string.IsNullOrWhiteSpace(GetString(i, "invoice_id")))
            .ToDictionary(i => GetString(i, "invoice_id")!, StringComparer.Ordinal);

        var customerIds = batch
            .Select(r => r.CustomerId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var clientIdBySquareCustomerId = customerIds.Length == 0
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : await clientIntegrationsSquare.AsNoTracking()
                .Where(cis => EF.Property<string?>(cis, "square_customer_id") != null)
                .Where(cis => customerIds.Contains(EF.Property<string?>(cis, "square_customer_id")!))
                .Select(
                    cis =>
                        new
                        {
                            SquareCustomerId = EF.Property<string?>(cis, "square_customer_id")!,
                            ClientId = EF.Property<string>(cis, "client_id"),
                        }
                )
                .ToDictionaryAsync(x => x.SquareCustomerId, x => x.ClientId, StringComparer.Ordinal, cancellationToken);

        var clientIdsInBatch = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in batch)
        {
            if (string.IsNullOrWhiteSpace(row.TransactionId) || row.TransactionAt is null)
            {
                continue;
            }

            var squareCustomerId = row.CustomerId?.Trim();
            if (squareCustomerId is not null && clientIdBySquareCustomerId.TryGetValue(squareCustomerId, out var matchedClientId))
            {
                clientIdsInBatch.Add(matchedClientId);
                continue;
            }

            clientIdsInBatch.Add(await EnsureUnmatchedClientIdAsync(dryRun, cancellationToken));
        }

        var ledgerProjectIdByClientId = await EnsureLedgerProjectsAsync(
            db.Projects,
            projectCodeCounters,
            clientIdsInBatch,
            now,
            stats,
            cancellationToken,
            dryRun
        );

        foreach (var row in batch)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await ImportRowAsync(
                row,
                clientIdBySquareCustomerId,
                ledgerProjectIdByClientId,
                paymentsByExternalId,
                invoicesById,
                invoiceIntegrationsByInvoiceId,
                invoices,
                invoiceIntegrationsSquare,
                payments,
                invoiceNumberCounters,
                now,
                stats,
                unmatchedCustomerIdCounts,
                unmatchedReportWriter,
                cancellationToken,
                dryRun
            );
        }
    }

    private async Task ImportRowAsync(
        SquareTransactionRow row,
        IReadOnlyDictionary<string, string> clientIdBySquareCustomerId,
        IReadOnlyDictionary<string, string> ledgerProjectIdByClientId,
        IReadOnlyDictionary<string, List<Dictionary<string, object>>> paymentsByExternalId,
        IReadOnlyDictionary<string, Dictionary<string, object>> invoicesById,
        IReadOnlyDictionary<string, Dictionary<string, object>> invoiceIntegrationsByInvoiceId,
        DbSet<Dictionary<string, object>> invoices,
        DbSet<Dictionary<string, object>> invoiceIntegrationsSquare,
        DbSet<Dictionary<string, object>> payments,
        DbSet<Dictionary<string, object>> invoiceNumberCounters,
        DateTimeOffset now,
        TransactionsImportStats stats,
        Dictionary<string, int> unmatchedCustomerIdCounts,
        TextWriter? unmatchedReportWriter,
        CancellationToken cancellationToken,
        bool dryRun
    )
    {
        if (string.IsNullOrWhiteSpace(row.TransactionId))
        {
            stats.Errors++;
            return;
        }

        if (row.TransactionAt is null)
        {
            stats.Errors++;
            return;
        }

        var transactionId = row.TransactionId.Trim();
        var issuedAt = row.TransactionAt.Value;

        var amountCents = row.TotalCollectedCents ?? row.GrossSalesCents;
        if (amountCents is null || amountCents.Value <= 0)
        {
            stats.Errors++;
            return;
        }

        var paymentStatusKey = MapPaymentStatusKey(row.TransactionStatus, row.PartialRefundsCents);
        var invoiceStatusKey = MapInvoiceStatusKey(paymentStatusKey);
        var amount = ToMoney(amountCents.Value);
        var currencyCode = NormalizeCurrencyCode(row.CurrencyCode) ?? DefaultCurrencyCode;
        var taxAmount = row.TaxCents is null ? 0m : ToMoney(Math.Abs(row.TaxCents.Value));

        var squareCustomerId = row.CustomerId?.Trim();
        string? matchedClientId = null;
        var hasMatch = squareCustomerId is not null && clientIdBySquareCustomerId.TryGetValue(squareCustomerId, out matchedClientId);
        var clientId = hasMatch ? matchedClientId! : await EnsureUnmatchedClientIdAsync(dryRun, cancellationToken);

        if (!hasMatch)
        {
            stats.UnmatchedRows++;

            if (unmatchedReportWriter is not null)
            {
                WriteUnmatchedReportRow(unmatchedReportWriter, row, transactionId, amount, issuedAt);
                stats.UnmatchedReportRowsWritten++;
            }

            if (!string.IsNullOrWhiteSpace(squareCustomerId))
            {
                unmatchedCustomerIdCounts[squareCustomerId] =
                    unmatchedCustomerIdCounts.TryGetValue(squareCustomerId, out var c) ? c + 1 : 1;
            }
        }

        if (!ledgerProjectIdByClientId.TryGetValue(clientId, out var ledgerProjectId))
        {
            stats.Errors++;
            return;
        }

        stats.TotalsByYear.Add(issuedAt.Year, amount);
        stats.TotalsByPaymentStatus.Add(paymentStatusKey, amount);

        if (paymentsByExternalId.TryGetValue(transactionId, out var existingPaymentRows))
        {
            if (existingPaymentRows.Count != 1)
            {
                stats.Errors++;
                return;
            }

            var paymentRow = existingPaymentRows[0];
            var invoiceId = GetString(paymentRow, "invoice_id");
            if (string.IsNullOrWhiteSpace(invoiceId) || !invoicesById.TryGetValue(invoiceId, out var invoiceRow))
            {
                stats.Errors++;
                return;
            }

            var changedInvoice = ApplyInvoiceFields(
                invoiceRow,
                statusKey: invoiceStatusKey,
                clientId: clientId,
                projectId: ledgerProjectId,
                issuedAt: issuedAt,
                paidAt: paymentStatusKey == "captured" ? issuedAt : null,
                currencyCode: currencyCode,
                amount: amount,
                taxAmount: taxAmount,
                now: now,
                squareCustomerId: squareCustomerId,
                transactionId: transactionId,
                dryRun: dryRun
            );

            if (changedInvoice)
            {
                stats.InvoicesUpdated++;
            }
            else
            {
                stats.InvoicesSkipped++;
            }

            var changedPayment = ApplyPaymentFields(
                paymentRow,
                statusKey: paymentStatusKey,
                capturedAt: paymentStatusKey == "captured" ? issuedAt : null,
                currencyCode: currencyCode,
                amount: amount,
                now: now,
                transactionId: transactionId,
                dryRun: dryRun
            );

            if (changedPayment)
            {
                stats.PaymentsUpdated++;
            }
            else
            {
                stats.PaymentsSkipped++;
            }

            var integrationRow = invoiceIntegrationsByInvoiceId.TryGetValue(invoiceId, out var existingIntegration)
                ? existingIntegration
                : null;

            var integrationResult = await UpsertInvoiceIntegrationSquareAsync(
                invoiceIntegrationsSquare,
                existing: integrationRow,
                invoiceId: invoiceId,
                squareCustomerId: squareCustomerId,
                cancellationToken: cancellationToken,
                dryRun: dryRun
            );

            switch (integrationResult)
            {
                case UpsertResult.Inserted:
                    stats.InvoiceIntegrationsInserted++;
                    break;
                case UpsertResult.Updated:
                    stats.InvoiceIntegrationsUpdated++;
                    break;
                default:
                    stats.InvoiceIntegrationsSkipped++;
                    break;
            }

            return;
        }

        stats.PaymentsInserted++;
        stats.InvoicesInserted++;
        stats.InvoiceIntegrationsInserted++;

        if (dryRun)
        {
            return;
        }

        var invoiceIdNew = EntityIds.NewWithPrefix("inv");
        var invoiceNumber = await AllocateInvoiceNumberAsync(invoiceNumberCounters, issuedAt, now, cancellationToken);

        await invoices.AddAsync(
            CreateInvoiceRow(
                invoiceId: invoiceIdNew,
                invoiceNumber: invoiceNumber,
                clientId: clientId,
                projectId: ledgerProjectId,
                statusKey: invoiceStatusKey,
                issuedAt: issuedAt,
                paidAt: paymentStatusKey == "captured" ? issuedAt : null,
                currencyCode: currencyCode,
                amount: amount,
                taxAmount: taxAmount,
                now: now,
                squareCustomerId: squareCustomerId,
                transactionId: transactionId
            ),
            cancellationToken
        );

        await invoiceIntegrationsSquare.AddAsync(CreateInvoiceIntegrationSquareRow(invoiceIdNew, squareCustomerId), cancellationToken);

        await payments.AddAsync(
            CreatePaymentRow(
                paymentId: EntityIds.NewWithPrefix("pay"),
                invoiceId: invoiceIdNew,
                statusKey: paymentStatusKey,
                capturedAt: paymentStatusKey == "captured" ? issuedAt : null,
                currencyCode: currencyCode,
                amount: amount,
                now: now,
                transactionId: transactionId,
                squareCustomerId: squareCustomerId
            ),
            cancellationToken
        );
    }

    private static void WriteDetailedSummary(TransactionsImportStats stats, Dictionary<string, int> unmatchedCustomerIdCounts)
    {
        Console.WriteLine(
            $"square-import transactions: totals inserted={stats.TotalInserted} updated={stats.TotalUpdated} skipped={stats.TotalSkipped} dupes={stats.DuplicateTransactionIdsSkipped} unmatched={stats.UnmatchedRows} errors={stats.Errors}"
        );

        if (stats.MultiCustomerIdRows > 0)
        {
            Console.WriteLine($"square-import transactions: multi-customer id rows={stats.MultiCustomerIdRows} (using first non-empty)");
        }

        Console.WriteLine($"square-import transactions: projects inserted={stats.ProjectsInserted}");
        Console.WriteLine($"square-import transactions: invoices inserted={stats.InvoicesInserted} updated={stats.InvoicesUpdated} skipped={stats.InvoicesSkipped}");
        Console.WriteLine(
            $"square-import transactions: invoice_integrations_square inserted={stats.InvoiceIntegrationsInserted} updated={stats.InvoiceIntegrationsUpdated} skipped={stats.InvoiceIntegrationsSkipped}"
        );
        Console.WriteLine($"square-import transactions: payments inserted={stats.PaymentsInserted} updated={stats.PaymentsUpdated} skipped={stats.PaymentsSkipped}");

        var topUnmatched = unmatchedCustomerIdCounts
            .OrderByDescending(kvp => kvp.Value)
            .ThenBy(kvp => kvp.Key, StringComparer.Ordinal)
            .Take(20)
            .ToList();

        Console.WriteLine(
            topUnmatched.Count == 0
                ? "square-import transactions: top_unmatched_customer_ids=none"
                : $"square-import transactions: top_unmatched_customer_ids={topUnmatched.Count}"
        );

        foreach (var kvp in topUnmatched)
        {
            Console.WriteLine($"square-import transactions: unmatched_customer_id {kvp.Key} count={kvp.Value}");
        }

        if (stats.TotalsByYear.Count > 0)
        {
            Console.WriteLine("square-import transactions: totals_by_year:");
            foreach (var kvp in stats.TotalsByYear.Items.OrderBy(k => k.Key))
            {
                Console.WriteLine($"square-import transactions: year={kvp.Key} count={kvp.Value.Count} amount={kvp.Value.Amount:0.00}");
            }
        }

        if (stats.TotalsByPaymentStatus.Count > 0)
        {
            Console.WriteLine("square-import transactions: totals_by_status:");
            foreach (var kvp in stats.TotalsByPaymentStatus.Items.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                Console.WriteLine($"square-import transactions: status={kvp.Key} count={kvp.Value.Count} amount={kvp.Value.Amount:0.00}");
            }
        }
    }

    private static string MapPaymentStatusKey(string? transactionStatus, long? partialRefundsCents)
    {
        if (partialRefundsCents is not null && partialRefundsCents.Value != 0)
        {
            return "refunded";
        }

        if (string.IsNullOrWhiteSpace(transactionStatus))
        {
            return "captured";
        }

        var value = transactionStatus.Trim();
        if (value.Equals("Complete", StringComparison.OrdinalIgnoreCase))
        {
            return "captured";
        }

        if (value.Contains("auth", StringComparison.OrdinalIgnoreCase))
        {
            return "authorized";
        }

        if (value.Contains("refund", StringComparison.OrdinalIgnoreCase))
        {
            return "refunded";
        }

        if (
            value.Contains("fail", StringComparison.OrdinalIgnoreCase)
            || value.Contains("cancel", StringComparison.OrdinalIgnoreCase)
            || value.Contains("void", StringComparison.OrdinalIgnoreCase)
        )
        {
            return "failed";
        }

        if (value.Contains("pending", StringComparison.OrdinalIgnoreCase))
        {
            return "pending";
        }

        return "captured";
    }

    private static string MapInvoiceStatusKey(string paymentStatusKey)
    {
        return paymentStatusKey switch
        {
            "captured" => "paid",
            "refunded" => "refunded",
            "failed" => "void",
            _ => "unpaid",
        };
    }

    private static decimal ToMoney(long cents) => cents / 100m;

    private static string? NormalizeCurrencyCode(string? currencyCode)
    {
        if (string.IsNullOrWhiteSpace(currencyCode))
        {
            return null;
        }

        var normalized = currencyCode.Trim().ToUpperInvariant();
        return normalized.Length == 3 ? normalized : null;
    }

    private static bool SetIfDifferent(Dictionary<string, object> row, string key, object value, bool dryRun)
    {
        if (row.TryGetValue(key, out var existing) && Equals(existing, value))
        {
            return false;
        }

        if (!dryRun)
        {
            row[key] = value;
        }

        return true;
    }

    private static string? GetString(Dictionary<string, object> row, string key)
    {
        if (!row.TryGetValue(key, out var value) || value is null || value is DBNull)
        {
            return null;
        }

        return value as string;
    }

    private static int GetInt(Dictionary<string, object> row, string key)
    {
        if (!row.TryGetValue(key, out var value) || value is null || value is DBNull)
        {
            return 0;
        }

        return value switch
        {
            int i => i,
            short s => s,
            long l => (int)l,
            _ => 0,
        };
    }

    private async Task<string> EnsureUnmatchedClientIdAsync(bool dryRun, CancellationToken cancellationToken)
    {
        if (dryRun)
        {
            return UnmatchedClientIdPlaceholder;
        }

        if (unmatchedClientIdCache is not null)
        {
            return unmatchedClientIdCache;
        }

        var existingClientId = await db.Clients.AsNoTracking()
            .Where(c => c.Notes == UnmatchedClientNotesMarker)
            .Select(c => c.ClientId)
            .FirstOrDefaultAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(existingClientId))
        {
            unmatchedClientIdCache = existingClientId;
            return existingClientId;
        }

        var clientId = EntityIds.NewClientId();
        await db.Clients.AddAsync(
            new Client(
                clientId: clientId,
                displayName: UnmatchedClientDisplayName,
                clientTypeKey: "organization",
                statusKey: "active",
                primaryContactPersonId: null,
                accountOwnerPersonId: null,
                notes: UnmatchedClientNotesMarker,
                dataProfile: "real",
                createdAt: DateTimeOffset.UtcNow,
                updatedAt: null
            ),
            cancellationToken
        );

        var clientIntegrationsSquare = db.Set<Dictionary<string, object>>("client_integrations_square");
        await clientIntegrationsSquare.AddAsync(
            new Dictionary<string, object>
            {
                ["client_id"] = clientId,
                ["square_customer_id"] = UnmatchedSquareCustomerIdSentinel,
                ["creation_source_key"] = "import",
                ["currency_code"] = null!,
                ["first_visit_at"] = null!,
                ["last_visit_at"] = null!,
                ["lifetime_spend_cents"] = null!,
                ["transaction_count"] = null!,
                ["updated_at"] = DateTimeOffset.UtcNow,
            },
            cancellationToken
        );

        unmatchedClientIdCache = clientId;
        return clientId;
    }

    private async Task<Dictionary<string, string>> EnsureLedgerProjectsAsync(
        DbSet<Project> projects,
        DbSet<Dictionary<string, object>> projectCodeCounters,
        IReadOnlyCollection<string> clientIds,
        DateTimeOffset now,
        TransactionsImportStats stats,
        CancellationToken cancellationToken,
        bool dryRun
    )
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        if (clientIds.Count == 0)
        {
            return result;
        }

        foreach (var clientId in clientIds)
        {
            if (ledgerProjectIdCache.TryGetValue(clientId, out var cachedProjectId))
            {
                result[clientId] = cachedProjectId;
            }
        }

        var remainingClientIds = clientIds.Where(id => !result.ContainsKey(id)).ToList();
        if (remainingClientIds.Count == 0)
        {
            return result;
        }

        var existingProjects = await projects.AsNoTracking()
            .Where(p => remainingClientIds.Contains(p.ClientId) && p.Name == LedgerProjectName)
            .ToListAsync(cancellationToken);

        foreach (var p in existingProjects)
        {
            if (result.ContainsKey(p.ClientId))
            {
                continue;
            }

            result[p.ClientId] = p.ProjectId;
            ledgerProjectIdCache[p.ClientId] = p.ProjectId;
        }

        foreach (var clientId in remainingClientIds)
        {
            if (result.ContainsKey(clientId))
            {
                continue;
            }

            stats.ProjectsInserted++;

            if (dryRun)
            {
                var placeholderProjectId = EntityIds.NewProjectId();
                result[clientId] = placeholderProjectId;
                ledgerProjectIdCache[clientId] = placeholderProjectId;
                continue;
            }

            var year2 = (short)(now.Year % 100);
            var projectCode = await AllocateProjectCodeAsync(projectCodeCounters, year2, now, cancellationToken);
            var projectId = EntityIds.NewProjectId();

            await projects.AddAsync(
                new Project(
                    projectId: projectId,
                    projectCode: projectCode,
                    clientId: clientId,
                    name: LedgerProjectName,
                    statusKey: "active",
                    phaseKey: "planning",
                    dataProfile: "real"
                ),
                cancellationToken
            );

            result[clientId] = projectId;
            ledgerProjectIdCache[clientId] = projectId;
        }

        return result;
    }

    private static async Task<string> AllocateProjectCodeAsync(
        DbSet<Dictionary<string, object>> projectCodeCounters,
        short year2,
        DateTimeOffset now,
        CancellationToken cancellationToken
    )
    {
        var counter = await projectCodeCounters
            .SingleOrDefaultAsync(
                c => EF.Property<string>(c, "prefix") == CodePrefix && EF.Property<short>(c, "year_2") == year2,
                cancellationToken
            );

        int seq;
        if (counter is null)
        {
            seq = 1;
            await projectCodeCounters.AddAsync(
                new Dictionary<string, object>
                {
                    ["prefix"] = CodePrefix,
                    ["year_2"] = year2,
                    ["next_seq"] = seq + 1,
                    ["updated_at"] = now,
                },
                cancellationToken
            );
        }
        else
        {
            seq = GetInt(counter, "next_seq");
            counter["next_seq"] = seq + 1;
            counter["updated_at"] = now;
        }

        return $"{CodePrefix}{year2:00}-{seq:0000}";
    }

    private static async Task<string> AllocateInvoiceNumberAsync(
        DbSet<Dictionary<string, object>> invoiceNumberCounters,
        DateTimeOffset issuedAt,
        DateTimeOffset now,
        CancellationToken cancellationToken
    )
    {
        var year2 = (short)(issuedAt.Year % 100);

        var counter = await invoiceNumberCounters
            .SingleOrDefaultAsync(
                c => EF.Property<string>(c, "prefix") == CodePrefix && EF.Property<short>(c, "year_2") == year2,
                cancellationToken
            );

        int seq;
        if (counter is null)
        {
            seq = 1;
            await invoiceNumberCounters.AddAsync(
                new Dictionary<string, object>
                {
                    ["prefix"] = CodePrefix,
                    ["year_2"] = year2,
                    ["next_seq"] = seq + 1,
                    ["updated_at"] = now,
                },
                cancellationToken
            );
        }
        else
        {
            seq = GetInt(counter, "next_seq");
            counter["next_seq"] = seq + 1;
            counter["updated_at"] = now;
        }

        return $"{CodePrefix}-INV-{year2:00}-{seq:000000}";
    }

    private static Dictionary<string, object> CreateInvoiceRow(
        string invoiceId,
        string invoiceNumber,
        string clientId,
        string projectId,
        string statusKey,
        DateTimeOffset issuedAt,
        DateTimeOffset? paidAt,
        string currencyCode,
        decimal amount,
        decimal taxAmount,
        DateTimeOffset now,
        string? squareCustomerId,
        string transactionId
    )
    {
        var noteSuffix = squareCustomerId is null ? string.Empty : $"; square_customer_id={squareCustomerId}";

        return new Dictionary<string, object>
        {
            ["invoice_id"] = invoiceId,
            ["client_id"] = clientId,
            ["project_id"] = projectId,
            ["invoice_number"] = invoiceNumber,
            ["currency_code"] = currencyCode,
            ["status_key"] = statusKey,
            ["issued_at"] = issuedAt,
            ["due_at"] = null!,
            ["paid_at"] = paidAt!,
            ["refunded_at"] = null!,
            ["payment_method_key"] = SquarePaymentMethodKey,
            ["subtotal_amount"] = amount,
            ["tax_rate"] = null!,
            ["tax_amount"] = taxAmount,
            ["total_amount"] = amount,
            ["notes"] = $"square-import transactions: transaction_id={transactionId}{noteSuffix}",
            ["data_profile"] = "real",
            ["created_at"] = now,
            ["updated_at"] = null!,
        };
    }

    private static Dictionary<string, object> CreateInvoiceIntegrationSquareRow(string invoiceId, string? squareCustomerId)
    {
        return new Dictionary<string, object>
        {
            ["invoice_id"] = invoiceId,
            ["square_customer_id"] = squareCustomerId!,
            ["square_invoice_id"] = null!,
            ["last_synced_at"] = null!,
            ["sync_status_key"] = null!,
        };
    }

    private static Dictionary<string, object> CreatePaymentRow(
        string paymentId,
        string invoiceId,
        string statusKey,
        DateTimeOffset? capturedAt,
        string currencyCode,
        decimal amount,
        DateTimeOffset now,
        string transactionId,
        string? squareCustomerId
    )
    {
        var noteSuffix = squareCustomerId is null ? string.Empty : $"; square_customer_id={squareCustomerId}";

        return new Dictionary<string, object>
        {
            ["payment_id"] = paymentId,
            ["invoice_id"] = invoiceId,
            ["amount"] = amount,
            ["currency_code"] = currencyCode,
            ["status_key"] = statusKey,
            ["captured_at"] = capturedAt!,
            ["refunded_amount"] = null!,
            ["refunded_at"] = null!,
            ["method_key"] = SquarePaymentMethodKey,
            ["processor_key"] = SquareProcessorKey,
            ["processor_payment_id"] = transactionId,
            ["processor_refund_id"] = null!,
            ["recorded_by_person_id"] = null!,
            ["source"] = "square_import",
            ["notes"] = $"square-import transactions: transaction_id={transactionId}{noteSuffix}",
            ["data_profile"] = "real",
            ["created_at"] = now,
            ["updated_at"] = null!,
        };
    }

    private static bool ApplyInvoiceFields(
        Dictionary<string, object> invoiceRow,
        string statusKey,
        string clientId,
        string projectId,
        DateTimeOffset issuedAt,
        DateTimeOffset? paidAt,
        string currencyCode,
        decimal amount,
        decimal taxAmount,
        DateTimeOffset now,
        string? squareCustomerId,
        string transactionId,
        bool dryRun
    )
    {
        var changed = false;

        changed |= SetIfDifferent(invoiceRow, "client_id", clientId, dryRun);
        changed |= SetIfDifferent(invoiceRow, "project_id", projectId, dryRun);
        changed |= SetIfDifferent(invoiceRow, "status_key", statusKey, dryRun);
        changed |= SetIfDifferent(invoiceRow, "issued_at", issuedAt, dryRun);
        changed |= SetIfDifferent(invoiceRow, "currency_code", currencyCode, dryRun);
        changed |= SetIfDifferent(invoiceRow, "payment_method_key", SquarePaymentMethodKey, dryRun);
        changed |= SetIfDifferent(invoiceRow, "subtotal_amount", amount, dryRun);
        changed |= SetIfDifferent(invoiceRow, "tax_amount", taxAmount, dryRun);
        changed |= SetIfDifferent(invoiceRow, "total_amount", amount, dryRun);
        changed |= SetIfDifferent(invoiceRow, "paid_at", paidAt!, dryRun);

        var noteSuffix = squareCustomerId is null ? string.Empty : $"; square_customer_id={squareCustomerId}";
        var desiredNotes = $"square-import transactions: transaction_id={transactionId}{noteSuffix}";
        changed |= SetIfDifferent(invoiceRow, "notes", desiredNotes, dryRun);

        if (changed)
        {
            _ = SetIfDifferent(invoiceRow, "updated_at", now, dryRun);
        }

        return changed;
    }

    private static bool ApplyPaymentFields(
        Dictionary<string, object> paymentRow,
        string statusKey,
        DateTimeOffset? capturedAt,
        string currencyCode,
        decimal amount,
        DateTimeOffset now,
        string transactionId,
        bool dryRun
    )
    {
        var changed = false;

        changed |= SetIfDifferent(paymentRow, "status_key", statusKey, dryRun);
        changed |= SetIfDifferent(paymentRow, "captured_at", capturedAt!, dryRun);
        changed |= SetIfDifferent(paymentRow, "currency_code", currencyCode, dryRun);
        changed |= SetIfDifferent(paymentRow, "amount", amount, dryRun);
        changed |= SetIfDifferent(paymentRow, "method_key", SquarePaymentMethodKey, dryRun);
        changed |= SetIfDifferent(paymentRow, "processor_key", SquareProcessorKey, dryRun);
        changed |= SetIfDifferent(paymentRow, "processor_payment_id", transactionId, dryRun);

        if (changed)
        {
            _ = SetIfDifferent(paymentRow, "updated_at", now, dryRun);
        }

        return changed;
    }

    private static async Task<UpsertResult> UpsertInvoiceIntegrationSquareAsync(
        DbSet<Dictionary<string, object>> invoiceIntegrationsSquare,
        Dictionary<string, object>? existing,
        string invoiceId,
        string? squareCustomerId,
        CancellationToken cancellationToken,
        bool dryRun
    )
    {
        if (existing is null)
        {
            if (dryRun)
            {
                return UpsertResult.Inserted;
            }

            await invoiceIntegrationsSquare.AddAsync(CreateInvoiceIntegrationSquareRow(invoiceId, squareCustomerId), cancellationToken);
            return UpsertResult.Inserted;
        }

        if (string.IsNullOrWhiteSpace(squareCustomerId))
        {
            return UpsertResult.Skipped;
        }

        var changed = SetIfDifferent(existing, "square_customer_id", squareCustomerId, dryRun);
        return changed ? UpsertResult.Updated : UpsertResult.Skipped;
    }

    private enum UpsertResult
    {
        Skipped = 0,
        Inserted = 1,
        Updated = 2,
    }

    private sealed class MoneyTotal
    {
        public int Count { get; set; }
        public decimal Amount { get; set; }
    }

    private sealed class Totals<TKey>
        where TKey : notnull
    {
        private readonly Dictionary<TKey, MoneyTotal> totals;

        public Totals()
        {
            totals = new Dictionary<TKey, MoneyTotal>();
        }

        public Totals(IEqualityComparer<TKey> comparer)
        {
            totals = new Dictionary<TKey, MoneyTotal>(comparer);
        }

        public int Count => totals.Count;

        public IReadOnlyDictionary<TKey, MoneyTotal> Items => totals;

        public void Add(TKey key, decimal amount)
        {
            if (!totals.TryGetValue(key, out var total))
            {
                total = new MoneyTotal();
                totals[key] = total;
            }

            total.Count++;
            total.Amount += amount;
        }
    }

    private sealed class TransactionsImportStats
    {
        public int ProjectsInserted { get; set; }

        public int InvoicesInserted { get; set; }
        public int InvoicesUpdated { get; set; }
        public int InvoicesSkipped { get; set; }

        public int InvoiceIntegrationsInserted { get; set; }
        public int InvoiceIntegrationsUpdated { get; set; }
        public int InvoiceIntegrationsSkipped { get; set; }

        public int PaymentsInserted { get; set; }
        public int PaymentsUpdated { get; set; }
        public int PaymentsSkipped { get; set; }

        public int DuplicateTransactionIdsSkipped { get; set; }
        public int MultiCustomerIdRows { get; set; }
        public int UnmatchedRows { get; set; }
        public int UnmatchedReportRowsWritten { get; set; }
        public int Errors { get; set; }

        public Totals<int> TotalsByYear { get; } = new();
        public Totals<string> TotalsByPaymentStatus { get; } = new(StringComparer.Ordinal);

        public int TotalInserted => ProjectsInserted + InvoicesInserted + InvoiceIntegrationsInserted + PaymentsInserted;
        public int TotalUpdated => InvoicesUpdated + InvoiceIntegrationsUpdated + PaymentsUpdated;
        public int TotalSkipped => InvoicesSkipped + InvoiceIntegrationsSkipped + PaymentsSkipped;
    }

    private static TextWriter? CreateUnmatchedReportWriter(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        return new StreamWriter(path, append: false, encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static void WriteUnmatchedReportRow(
        TextWriter writer,
        SquareTransactionRow row,
        string transactionId,
        decimal amount,
        DateTimeOffset occurredAt
    )
    {
        row.RawFields.TryGetValue("Customer ID", out var rawCustomerId);
        row.RawFields.TryGetValue("Customer Name", out var rawCustomerName);

        WriteCsvRow(
            writer,
            transactionId,
            rawCustomerId,
            rawCustomerName ?? row.CustomerName,
            amount.ToString("0.00", CultureInfo.InvariantCulture),
            occurredAt.ToString("o", CultureInfo.InvariantCulture),
            Path.GetFileName(row.SourceFile),
            row.RowNumber.ToString(CultureInfo.InvariantCulture)
        );
    }

    private static void WriteCsvRow(TextWriter writer, params string?[] fields)
    {
        for (var i = 0; i < fields.Length; i++)
        {
            if (i > 0)
            {
                writer.Write(',');
            }

            writer.Write(CsvEscape(fields[i]));
        }

        writer.WriteLine();
    }

    private static string CsvEscape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var needsQuotes = value.Contains(',', StringComparison.Ordinal)
            || value.Contains('"', StringComparison.Ordinal)
            || value.Contains('\n', StringComparison.Ordinal)
            || value.Contains('\r', StringComparison.Ordinal);

        if (!needsQuotes)
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}
