namespace MGF.SquareImportCli.Parsing;

public sealed record SquareInvoiceRow(
    string SourceFile,
    int RowNumber,
    string? InvoiceToken,
    string? InvoiceId,
    DateTimeOffset? InvoiceDate,
    string? TimeZone,
    string? CustomerName,
    string? CustomerEmail,
    string? CustomerPhone,
    string? InvoiceTitle,
    string? Status,
    string? RequestedAmountRaw,
    long? RequestedAmountCents,
    DateTimeOffset? DueDate,
    DateTimeOffset? LastPaymentDate,
    string? AmountPaidRaw,
    long? AmountPaidCents,
    string? RecurringSeriesId,
    string? InvoiceDeliveryMethod,
    int? NumberOfInstallments,
    string? TipAmountRaw,
    long? TipAmountCents,
    string? AutomaticPaymentSource,
    DateTimeOffset? ServiceDate,
    IReadOnlyDictionary<string, string?> RawFields
);


