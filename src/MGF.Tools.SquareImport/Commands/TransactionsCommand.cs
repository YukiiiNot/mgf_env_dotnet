namespace MGF.Tools.SquareImport.Commands;

using System.CommandLine;
using System.CommandLine.Invocation;
using MGF.Tools.SquareImport.Importers;
using MGF.Tools.SquareImport.Reporting;

internal static class TransactionsCommand
{
    public static Command Create()
    {
        var filesOption = new Option<FileInfo[]>("--files")
        {
            Description = "Paths to one or more Square transactions export CSV files.",
            IsRequired = true,
            AllowMultipleArgumentsPerToken = true,
        };

        var dryRunOption = new Option<bool>("--dry-run")
        {
            Description = "Parse and validate only; do not write to the database.",
        };

        var command = new Command("transactions", "Import transactions from Square CSV exports.");
        command.AddOption(filesOption);
        command.AddOption(dryRunOption);

        command.SetHandler(
            async (InvocationContext context) =>
            {
                var files = context.ParseResult.GetValueForOption(filesOption) ?? Array.Empty<FileInfo>();
                var dryRun = context.ParseResult.GetValueForOption(dryRunOption);

                if (files.Length < 1 || files.Any(f => f is null || !f.Exists))
                {
                    Console.Error.WriteLine("square-import transactions: one or more --files paths are missing or invalid.");
                    new ImportSummary(Inserted: 0, Updated: 0, Skipped: 0, Errors: 1).WriteToConsole("transactions");
                    context.ExitCode = 1;
                    return;
                }

                context.ExitCode = await SquareImportCommandRunner.RunAsync(
                    commandName: "transactions",
                    dryRun: dryRun,
                    action: (db, cancellationToken) =>
                        new TransactionsImporter(db).ImportAsync(
                            files.Select(f => f.FullName).ToArray(),
                            dryRun,
                            cancellationToken
                        ),
                    cancellationToken: CancellationToken.None
                );
            }
        );

        return command;
    }
}

