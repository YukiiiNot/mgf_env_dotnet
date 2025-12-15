namespace MGF.Tools.SquareImport.Commands;

using System.CommandLine;
using System.CommandLine.Invocation;
using MGF.Tools.SquareImport.Importers;
using MGF.Tools.SquareImport.Reporting;

internal static class CustomersCommand
{
    public static Command Create()
    {
        var fileOption = new Option<FileInfo?>("--file")
        {
            Description = "Path to the Square customers export CSV file.",
        };

        var dryRunOption = new Option<bool>("--dry-run")
        {
            Description = "Parse and validate only; do not write to the database.",
        };

        var verifyOption = new Option<bool>("--verify")
        {
            Description = "Run DB integrity checks after importing customers (no CSV needed).",
        };

        var command = new Command("customers", "Import customers from a Square CSV export.");
        command.AddOption(fileOption);
        command.AddOption(dryRunOption);
        command.AddOption(verifyOption);

        command.SetHandler(
            async (InvocationContext context) =>
            {
                var file = context.ParseResult.GetValueForOption(fileOption);
                var dryRun = context.ParseResult.GetValueForOption(dryRunOption);
                var verify = context.ParseResult.GetValueForOption(verifyOption);

                if (verify)
                {
                    if (file is not null)
                    {
                        Console.Error.WriteLine("square-import customers: --verify cannot be used with --file.");
                        new ImportSummary(Inserted: 0, Updated: 0, Skipped: 0, Errors: 1).WriteToConsole("customers");
                        context.ExitCode = 1;
                        return;
                    }

                    context.ExitCode = await SquareImportCommandRunner.RunAsync(
                        commandName: "customers",
                        dryRun: true,
                        action: (db, cancellationToken) => new CustomersImporter(db).VerifyAsync(cancellationToken),
                        cancellationToken: CancellationToken.None
                    );
                    return;
                }

                if (file is null || !file.Exists)
                {
                    Console.Error.WriteLine("square-import customers: missing or invalid --file path (or use --verify).");
                    new ImportSummary(Inserted: 0, Updated: 0, Skipped: 0, Errors: 1).WriteToConsole("customers");
                    context.ExitCode = 1;
                    return;
                }

                context.ExitCode = await SquareImportCommandRunner.RunAsync(
                    commandName: "customers",
                    dryRun: dryRun,
                    action: (db, cancellationToken) =>
                        new CustomersImporter(db).ImportAsync(file.FullName, dryRun, cancellationToken),
                    cancellationToken: CancellationToken.None
                );
            }
        );

        return command;
    }
}
