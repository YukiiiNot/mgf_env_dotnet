namespace MGF.SquareImportCli.Parsing;

public sealed record SquareCustomerRow(
    string SourceFile,
    int RowNumber,
    string? SquareCustomerId,
    string? ReferenceId,
    string? FirstName,
    string? LastName,
    string? EmailAddress,
    string? PhoneNumber,
    string? Nickname,
    string? CompanyName,
    string? StreetAddress1,
    string? StreetAddress2,
    string? City,
    string? State,
    string? PostalCode,
    DateTimeOffset? Birthday,
    string? Memo,
    string? CreationSource,
    DateTimeOffset? FirstVisit,
    DateTimeOffset? LastVisit,
    int? TransactionCount,
    string? LifetimeSpendRaw,
    long? LifetimeSpendCents,
    string? EmailSubscriptionStatus,
    string? InstantProfile,
    IReadOnlyDictionary<string, string?> RawFields
);


