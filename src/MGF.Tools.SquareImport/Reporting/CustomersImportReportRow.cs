namespace MGF.Tools.SquareImport.Reporting;

internal sealed record CustomersImportReportRow(
    string SquareCustomerId,
    string DisplayName,
    string? NormalizedEmail,
    string? NormalizedPhone,
    string ProposedClientTypeKey,
    string ClassificationReason,
    string ProposedAction,
    string? MatchedClientId,
    string? MatchedPersonId,
    string Resolution,
    string Notes,
    string SourceFile,
    int RowNumber
);

