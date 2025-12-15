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

        var resetOption = new Option<bool>("--reset")
        {
            Description = "DEV-only: delete Square customer import data so it can be re-imported clean.",
        };

        var writeReportsOption = new Option<bool>("--write-reports", getDefaultValue: () => true)
        {
            Description = "Write review CSVs (default true).",
            Arity = ArgumentArity.ZeroOrOne,
        };

        var reportDirOption = new Option<DirectoryInfo?>("--report-dir", getDefaultValue: () => new DirectoryInfo(".\\runtime\\square-import\\"))
        {
            Description = "Directory to write review CSVs (default .\\runtime\\square-import\\).",
        };

        var strictOption = new Option<bool>("--strict")
        {
            Description = "Fail (exit nonzero) if a hard-duplicate match is ambiguous.",
        };

        var minConfidenceOption = new Option<string?>("--min-confidence-to-auto-link", getDefaultValue: () => "email_or_phone")
        {
            Description = "Auto-link confidence threshold (default email_or_phone). Supported: email_or_phone, email_only, phone_only, none.",
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
        command.AddOption(resetOption);
        command.AddOption(writeReportsOption);
        command.AddOption(reportDirOption);
        command.AddOption(strictOption);
        command.AddOption(minConfidenceOption);
        command.AddOption(dryRunOption);
        command.AddOption(verifyOption);

        command.SetHandler(
            async (InvocationContext context) =>
            {
                var file = context.ParseResult.GetValueForOption(fileOption);
                var reset = context.ParseResult.GetValueForOption(resetOption);
                var writeReports = context.ParseResult.GetValueForOption(writeReportsOption);
                var reportDir = context.ParseResult.GetValueForOption(reportDirOption);
                var strict = context.ParseResult.GetValueForOption(strictOption);
                var minConfidence = context.ParseResult.GetValueForOption(minConfidenceOption);
                var dryRun = context.ParseResult.GetValueForOption(dryRunOption);
                var verify = context.ParseResult.GetValueForOption(verifyOption);

                if (reset)
                {
                    if (verify)
                    {
                        Console.Error.WriteLine("square-import customers: --reset cannot be used with --verify.");
                        new ImportSummary(Inserted: 0, Updated: 0, Skipped: 0, Errors: 1).WriteToConsole("customers");
                        context.ExitCode = 1;
                        return;
                    }

                    if (file is not null)
                    {
                        Console.Error.WriteLine("square-import customers: --reset cannot be used with --file.");
                        new ImportSummary(Inserted: 0, Updated: 0, Skipped: 0, Errors: 1).WriteToConsole("customers");
                        context.ExitCode = 1;
                        return;
                    }

                    context.ExitCode = await SquareImportCommandRunner.RunAsync(
                        commandName: "customers",
                        dryRun: dryRun,
                        action: (db, cancellationToken) => new CustomersImporter(db).ResetAsync(dryRun, cancellationToken),
                        cancellationToken: CancellationToken.None
                    );
                    return;
                }

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

                if (!CustomersImportOptions.TryParseMinConfidence(minConfidence, out var minConfidenceToAutoLink, out var minConfidenceError))
                {
                    Console.Error.WriteLine($"square-import customers: invalid --min-confidence-to-auto-link: {minConfidenceError}");
                    new ImportSummary(Inserted: 0, Updated: 0, Skipped: 0, Errors: 1).WriteToConsole("customers");
                    context.ExitCode = 1;
                    return;
                }

                context.ExitCode = await SquareImportCommandRunner.RunAsync(
                    commandName: "customers",
                    dryRun: dryRun,
                    action: (db, cancellationToken) =>
                        new CustomersImporter(db).ImportAsync(
                            filePath: file.FullName,
                            options: new CustomersImportOptions(
                                WriteReports: writeReports,
                                ReportDir: reportDir,
                                Strict: strict,
                                MinConfidenceToAutoLink: minConfidenceToAutoLink
                            ),
                            dryRun: dryRun,
                            cancellationToken: cancellationToken
                        ),
                    cancellationToken: CancellationToken.None
                );
            }
        );

        return command;
    }
}
