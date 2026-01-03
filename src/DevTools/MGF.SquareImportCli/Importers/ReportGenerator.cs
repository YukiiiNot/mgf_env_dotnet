namespace MGF.SquareImportCli.Importers;

using MGF.Data.Data;
using MGF.SquareImportCli.Reporting;

internal sealed class ReportGenerator
{
    private readonly AppDbContext db;

    public ReportGenerator(AppDbContext db)
    {
        this.db = db;
    }

    public Task<ImportSummary> GenerateAsync(string? outPath, CancellationToken cancellationToken)
    {
        _ = db;
        _ = cancellationToken;

        Console.WriteLine(
            outPath is null ? "square-import report: not implemented yet." : $"square-import report: not implemented yet (out={outPath})."
        );

        return Task.FromResult(ImportSummary.Empty());
    }
}



