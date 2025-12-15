namespace MGF.Tools.SquareImport.Parsing;

using System.Globalization;
using CsvHelper;
using MGF.Tools.SquareImport.Normalization;

public static class CsvLoader
{
    public static IReadOnlyList<SquareCustomerRow> LoadCustomers(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var results = new List<SquareCustomerRow>();
        foreach (var row in LoadRawRows(path))
        {
            var lifetimeSpendRaw = row.GetString("Lifetime Spend");
            var lifetimeSpendCents = TryParseMoneyCents(lifetimeSpendRaw);

            results.Add(
                new SquareCustomerRow(
                    SourceFile: row.SourceFile,
                    RowNumber: row.RowNumber,
                    SquareCustomerId: row.GetString("Square Customer ID"),
                    ReferenceId: row.GetString("Reference ID"),
                    FirstName: IdentityKeys.NormalizeName(row.GetString("First Name")),
                    LastName: IdentityKeys.NormalizeName(row.GetString("Last Name")),
                    EmailAddress: IdentityKeys.NormalizeEmail(row.GetString("Email Address")),
                    PhoneNumber: IdentityKeys.NormalizePhone(row.GetString("Phone Number")),
                    Nickname: IdentityKeys.NormalizeName(row.GetString("Nickname")),
                    CompanyName: IdentityKeys.NormalizeName(row.GetString("Company Name")),
                    StreetAddress1: IdentityKeys.NormalizeName(row.GetString("Street Address 1")),
                    StreetAddress2: IdentityKeys.NormalizeName(row.GetString("Street Address 2")),
                    City: IdentityKeys.NormalizeName(row.GetString("City")),
                    State: IdentityKeys.NormalizeName(row.GetString("State")),
                    PostalCode: IdentityKeys.NormalizeName(row.GetString("Postal Code")),
                    Birthday: TryParseDateOnly(row.GetString("Birthday"), timeZone: null),
                    Memo: row.GetString("Memo"),
                    CreationSource: row.GetString("Creation Source"),
                    FirstVisit: TryParseDateOnly(row.GetString("First Visit"), timeZone: null),
                    LastVisit: TryParseDateOnly(row.GetString("Last Visit"), timeZone: null),
                    TransactionCount: TryParseInt(row.GetString("Transaction Count")),
                    LifetimeSpendRaw: lifetimeSpendRaw,
                    LifetimeSpendCents: lifetimeSpendCents,
                    EmailSubscriptionStatus: row.GetString("Email Subscription Status"),
                    InstantProfile: row.GetString("Instant Profile"),
                    RawFields: row.RawFields
                )
            );
        }

        return results;
    }

    public static IReadOnlyList<SquareTransactionRow> LoadTransactions(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        var results = new List<SquareTransactionRow>();

        foreach (var path in paths)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);

            foreach (var row in LoadRawRows(path))
            {
                var timeZone = row.GetString("Time Zone");
                var transactionAt = TryParseDateTime(
                    date: row.GetString("Date"),
                    time: row.GetString("Time"),
                    timeZone: timeZone
                );

                var depositDate = TryParseDateOnly(row.GetString("Deposit Date"), timeZone);

                var grossSalesRaw = row.GetString("Gross Sales");
                var discountsRaw = row.GetString("Discounts");
                var serviceChargesRaw = row.GetString("Service Charges");
                var netSalesRaw = row.GetString("Net Sales");
                var giftCardSalesRaw = row.GetString("Gift Card Sales");
                var taxRaw = row.GetString("Tax");
                var tipRaw = row.GetString("Tip");
                var partialRefundsRaw = row.GetString("Partial Refunds");
                var totalCollectedRaw = row.GetString("Total Collected");
                var feesRaw = row.GetString("Fees");
                var netTotalRaw = row.GetString("Net Total");
                var thirdPartyFeesRaw = row.GetString("Third Party Fees");
                var unattributedTipsRaw = row.GetString("Unattributed Tips");
                var (customerId, hasMultipleCustomerIds) = ParseCustomerId(row.GetString("Customer ID"));

                results.Add(
                    new SquareTransactionRow(
                        SourceFile: row.SourceFile,
                        RowNumber: row.RowNumber,
                        TransactionAt: transactionAt,
                        TimeZone: timeZone,
                        TransactionId: row.GetString("Transaction ID"),
                        PaymentId: row.GetString("Payment ID"),
                        Source: row.GetString("Source"),
                        CardEntryMethods: row.GetString("Card Entry Methods"),
                        StaffName: IdentityKeys.NormalizeName(row.GetString("Staff Name")),
                        StaffId: row.GetString("Staff ID"),
                        TransactionStatus: row.GetString("Transaction Status"),
                        CustomerId: customerId,
                        HasMultipleCustomerIds: hasMultipleCustomerIds,
                        CustomerName: IdentityKeys.NormalizeName(row.GetString("Customer Name")),
                        CustomerReferenceId: row.GetString("Customer Reference ID"),
                        DepositId: row.GetString("Deposit ID"),
                        DepositDate: depositDate,
                        GrossSalesRaw: grossSalesRaw,
                        GrossSalesCents: TryParseMoneyCents(grossSalesRaw),
                        DiscountsRaw: discountsRaw,
                        DiscountsCents: TryParseMoneyCents(discountsRaw),
                        ServiceChargesRaw: serviceChargesRaw,
                        ServiceChargesCents: TryParseMoneyCents(serviceChargesRaw),
                        NetSalesRaw: netSalesRaw,
                        NetSalesCents: TryParseMoneyCents(netSalesRaw),
                        GiftCardSalesRaw: giftCardSalesRaw,
                        GiftCardSalesCents: TryParseMoneyCents(giftCardSalesRaw),
                        TaxRaw: taxRaw,
                        TaxCents: TryParseMoneyCents(taxRaw),
                        TipRaw: tipRaw,
                        TipCents: TryParseMoneyCents(tipRaw),
                        PartialRefundsRaw: partialRefundsRaw,
                        PartialRefundsCents: TryParseMoneyCents(partialRefundsRaw),
                        TotalCollectedRaw: totalCollectedRaw,
                        TotalCollectedCents: TryParseMoneyCents(totalCollectedRaw),
                        FeesRaw: feesRaw,
                        FeesCents: TryParseMoneyCents(feesRaw),
                        NetTotalRaw: netTotalRaw,
                        NetTotalCents: TryParseMoneyCents(netTotalRaw),
                        ThirdPartyFeesRaw: thirdPartyFeesRaw,
                        ThirdPartyFeesCents: TryParseMoneyCents(thirdPartyFeesRaw),
                        UnattributedTipsRaw: unattributedTipsRaw,
                        UnattributedTipsCents: TryParseMoneyCents(unattributedTipsRaw),
                        CurrencyCode: row.GetString("Currency") ?? row.GetString("Currency Code"),
                        RawFields: row.RawFields
                    )
                );
            }
        }

        return results;
    }

    public static IReadOnlyList<SquareInvoiceRow> LoadInvoices(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var results = new List<SquareInvoiceRow>();
        foreach (var row in LoadRawRows(path))
        {
            var timeZone = row.GetString("Time Zone");

            var requestedAmountRaw = row.GetString("Requested Amount");
            var amountPaidRaw = row.GetString("Amount Paid");
            var tipAmountRaw = row.GetString("Tip Amount");

            results.Add(
                new SquareInvoiceRow(
                    SourceFile: row.SourceFile,
                    RowNumber: row.RowNumber,
                    InvoiceToken: row.GetString("Invoice Token"),
                    InvoiceDate: TryParseDateOnly(row.GetString("Invoice Date"), timeZone),
                    TimeZone: timeZone,
                    InvoiceId: row.GetString("Invoice ID"),
                    CustomerName: IdentityKeys.NormalizeName(row.GetString("Customer Name")),
                    CustomerEmail: IdentityKeys.NormalizeEmail(row.GetString("Customer Email")),
                    CustomerPhone: IdentityKeys.NormalizePhone(row.GetString("Customer Phone")),
                    InvoiceTitle: IdentityKeys.NormalizeName(row.GetString("Invoice Title")),
                    Status: row.GetString("Status"),
                    RequestedAmountRaw: requestedAmountRaw,
                    RequestedAmountCents: TryParseMoneyCents(requestedAmountRaw),
                    DueDate: TryParseDateOnly(row.GetString("Due Date"), timeZone),
                    LastPaymentDate: TryParseDateOnly(row.GetString("Last Payment Date"), timeZone),
                    AmountPaidRaw: amountPaidRaw,
                    AmountPaidCents: TryParseMoneyCents(amountPaidRaw),
                    RecurringSeriesId: row.GetString("Recurring Series ID"),
                    InvoiceDeliveryMethod: row.GetString("Invoice Delivery Method"),
                    NumberOfInstallments: TryParseInt(row.GetString("Number of Installments")),
                    TipAmountRaw: tipAmountRaw,
                    TipAmountCents: TryParseMoneyCents(tipAmountRaw),
                    AutomaticPaymentSource: row.GetString("Automatic Payment Source"),
                    ServiceDate: TryParseDateOnly(row.GetString("Service date"), timeZone) ?? TryParseDateOnly(row.GetString("Service Date"), timeZone),
                    RawFields: row.RawFields
                )
            );
        }

        return results;
    }

    private static IEnumerable<SquareRawRow> LoadRawRows(string path)
    {
        using var reader = new StreamReader(path);
        using var csv = CsvReaderFactory.Create(reader);

        if (!csv.Read())
        {
            yield break;
        }

        csv.ReadHeader();
        var headers = csv.HeaderRecord ?? Array.Empty<string>();
        var normalizedHeaderKeys = headers.Select(NormalizeHeaderKey).ToArray();
        var fileRowNumber = 1; // header row

        while (csv.Read())
        {
            fileRowNumber++;

            var rawFields = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            var fields = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < headers.Length; i++)
            {
                var header = (headers[i] ?? string.Empty).Trim();
                var value = NormalizeCell(csv.GetField(i));

                rawFields[header] = value;
                fields[normalizedHeaderKeys[i]] = value;
            }

            yield return new SquareRawRow(
                SourceFile: path,
                RowNumber: fileRowNumber,
                Fields: fields,
                RawFields: rawFields
            );
        }
    }

    private readonly record struct SquareRawRow(
        string SourceFile,
        int RowNumber,
        IReadOnlyDictionary<string, string?> Fields,
        IReadOnlyDictionary<string, string?> RawFields
    )
    {
        public string? GetString(string headerName)
        {
            var key = NormalizeHeaderKey(headerName);
            return Fields.TryGetValue(key, out var value) ? value : null;
        }
    }

    private static string? NormalizeCell(string? value)
    {
        if (value is null)
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    private static string NormalizeHeaderKey(string header)
    {
        var normalizedName = IdentityKeys.NormalizeName(header);
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return string.Empty;
        }

        Span<char> buffer = stackalloc char[normalizedName.Length];
        var idx = 0;
        var lastWasUnderscore = false;

        foreach (var c in normalizedName)
        {
            if (char.IsLetterOrDigit(c))
            {
                buffer[idx++] = char.ToLowerInvariant(c);
                lastWasUnderscore = false;
                continue;
            }

            if (idx == 0 || lastWasUnderscore)
            {
                continue;
            }

            buffer[idx++] = '_';
            lastWasUnderscore = true;
        }

        if (idx == 0)
        {
            return string.Empty;
        }

        if (buffer[idx - 1] == '_')
        {
            idx--;
        }

        return idx == 0 ? string.Empty : new string(buffer[..idx]);
    }

    private static int? TryParseInt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static (string? CustomerId, bool HasMultipleCustomerIds) ParseCustomerId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return (null, false);
        }

        var parts = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return (null, false);
        }

        if (parts.Length == 1)
        {
            return (parts[0], false);
        }

        var distinctIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var part in parts)
        {
            distinctIds.Add(part);
            if (distinctIds.Count > 1)
            {
                break;
            }
        }

        return (parts[0], distinctIds.Count > 1);
    }

    private static long? TryParseMoneyCents(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var text = value.Trim();
        if (text.Length == 0)
        {
            return null;
        }

        var negative = false;
        if (text.StartsWith('(') && text.EndsWith(')'))
        {
            negative = true;
            text = text[1..^1].Trim();
        }

        if (text.StartsWith('-'))
        {
            negative = true;
            text = text[1..].Trim();
        }

        // Strip common currency formatting.
        text = text.Replace("$", string.Empty, StringComparison.Ordinal)
            .Replace(",", string.Empty, StringComparison.Ordinal)
            .Trim();

        if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
        {
            return null;
        }

        var cents = (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero);
        return negative ? -cents : cents;
    }

    private static DateTimeOffset? TryParseDateOnly(string? date, string? timeZone)
    {
        if (string.IsNullOrWhiteSpace(date))
        {
            return null;
        }

        if (!TryParseDateOnlyValue(date, out var parsedDate))
        {
            return null;
        }

        var localDateTime = parsedDate.ToDateTime(TimeOnly.MinValue);
        return ToDateTimeOffset(localDateTime, timeZone);
    }

    private static DateTimeOffset? TryParseDateTime(string? date, string? time, string? timeZone)
    {
        if (string.IsNullOrWhiteSpace(date) || string.IsNullOrWhiteSpace(time))
        {
            return null;
        }

        if (!TryParseDateOnlyValue(date, out var parsedDate))
        {
            return null;
        }

        if (!TryParseTimeOnlyValue(time, out var parsedTime))
        {
            return null;
        }

        var localDateTime = parsedDate.ToDateTime(parsedTime);
        return ToDateTimeOffset(localDateTime, timeZone);
    }

    private static bool TryParseDateOnlyValue(string date, out DateOnly parsed)
    {
        var trimmed = date.Trim();

        return DateOnly.TryParseExact(
                trimmed,
                ["yyyy-MM-dd", "M/d/yyyy", "MM/dd/yyyy", "M/d/yy", "MM/dd/yy"],
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out parsed
            )
            || DateOnly.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed);
    }

    private static bool TryParseTimeOnlyValue(string time, out TimeOnly parsed)
    {
        var trimmed = time.Trim();

        return TimeOnly.TryParseExact(
                trimmed,
                ["H:mm:ss", "HH:mm:ss", "H:mm", "HH:mm"],
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out parsed
            )
            || TimeOnly.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed);
    }

    private static DateTimeOffset ToDateTimeOffset(DateTime localDateTime, string? timeZone)
    {
        var tz = TryResolveTimeZone(timeZone);
        var offset = tz?.GetUtcOffset(localDateTime) ?? TimeSpan.Zero;
        return new DateTimeOffset(DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified), offset);
    }

    private static TimeZoneInfo? TryResolveTimeZone(string? squareTimeZone)
    {
        if (string.IsNullOrWhiteSpace(squareTimeZone))
        {
            return null;
        }

        var value = squareTimeZone.Trim();

        // Common Square export values (DisplayName fragments on Windows).
        if (value.Equals("Eastern Time (US & Canada)", StringComparison.OrdinalIgnoreCase))
        {
            return TryFindTimeZone(["Eastern Standard Time", "America/New_York"], value);
        }

        if (value.Equals("Central Time (US & Canada)", StringComparison.OrdinalIgnoreCase))
        {
            return TryFindTimeZone(["Central Standard Time", "America/Chicago"], value);
        }

        if (value.Equals("Mountain Time (US & Canada)", StringComparison.OrdinalIgnoreCase))
        {
            return TryFindTimeZone(["Mountain Standard Time", "America/Denver"], value);
        }

        if (value.Equals("Pacific Time (US & Canada)", StringComparison.OrdinalIgnoreCase))
        {
            return TryFindTimeZone(["Pacific Standard Time", "America/Los_Angeles"], value);
        }

        return TryFindTimeZone([value], value);
    }

    private static TimeZoneInfo? TryFindTimeZone(string[] ids, string displayFragment)
    {
        foreach (var id in ids)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch
            {
                // ignore
            }
        }

        var matches = TimeZoneInfo
            .GetSystemTimeZones()
            .Where(
                tz =>
                    tz.Id.Contains(displayFragment, StringComparison.OrdinalIgnoreCase)
                    || tz.DisplayName.Contains(displayFragment, StringComparison.OrdinalIgnoreCase)
                    || tz.StandardName.Contains(displayFragment, StringComparison.OrdinalIgnoreCase)
            )
            .ToList();

        return matches.Count == 1 ? matches[0] : null;
    }
}
