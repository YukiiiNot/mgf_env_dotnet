namespace MGF.Tools.SquareImport.Commands;

using System.CommandLine;
using System.CommandLine.Invocation;
using MGF.Tools.SquareImport.Importers;
using MGF.Tools.SquareImport.Reporting;

internal static class InvoicesCommand
{
    public static Command Create()
    {
        var fileOption = new Option<FileInfo>("--file")
        {
            Description = "Path to the Square invoices export CSV file.",
            IsRequired = true,
        };

        var dryRunOption = new Option<bool>("--dry-run")
        {
            Description = "Parse and validate only; do not write to the database.",
        };

        var command = new Command("invoices", "Import invoices from a Square CSV export.");
        command.AddOption(fileOption);
        command.AddOption(dryRunOption);

        command.SetHandler(
            async (InvocationContext context) =>
            {
                var file = context.ParseResult.GetValueForOption(fileOption);
                var dryRun = context.ParseResult.GetValueForOption(dryRunOption);

                if (file is null || !file.Exists)
                {
                    Console.Error.WriteLine("square-import invoices: missing or invalid --file path.");
                    new ImportSummary(Inserted: 0, Updated: 0, Skipped: 0, Errors: 1).WriteToConsole("invoices");
                    context.ExitCode = 1;
                    return;
                }

                context.ExitCode = await SquareImportCommandRunner.RunAsync(
                    commandName: "invoices",
                    dryRun: dryRun,
                    action: (db, cancellationToken) => new InvoicesImporter(db).ImportAsync(file.FullName, dryRun, cancellationToken),
                    cancellationToken: CancellationToken.None
                );
            }
        );

        return command;
    }
}

