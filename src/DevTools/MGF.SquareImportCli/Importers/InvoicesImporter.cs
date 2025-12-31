namespace MGF.Tools.SquareImport.Importers;

using MGF.Data.Data;
using MGF.Tools.SquareImport.Parsing;
using MGF.Tools.SquareImport.Reporting;

internal sealed class InvoicesImporter
{
    private readonly AppDbContext db;

    public InvoicesImporter(AppDbContext db)
    {
        this.db = db;
    }

    public Task<ImportSummary> ImportAsync(string filePath, bool dryRun, CancellationToken cancellationToken)
    {
        _ = db;
        _ = cancellationToken;

        var invoices = CsvLoader.LoadInvoices(filePath);
        Console.WriteLine($"square-import invoices: invoices count={invoices.Count} (dry-run={dryRun}).");

        return Task.FromResult(ImportSummary.Empty());
    }
}

