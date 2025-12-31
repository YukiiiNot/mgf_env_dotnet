namespace MGF.SquareImportCli.Commands;

using System.CommandLine;
using System.CommandLine.Invocation;
using MGF.SquareImportCli.Importers;

internal static class ReportCommand
{
    public static Command Create()
    {
        var outOption = new Option<FileInfo?>("--out")
        {
            Description = "Optional path to write the report output.",
        };

        var command = new Command("report", "Generate a summary report for recent Square imports.");
        command.AddOption(outOption);

        command.SetHandler(
            async (InvocationContext context) =>
            {
                var outFile = context.ParseResult.GetValueForOption(outOption);

                context.ExitCode = await SquareImportCommandRunner.RunAsync(
                    commandName: "report",
                    dryRun: false,
                    action: (db, cancellationToken) => new ReportGenerator(db).GenerateAsync(outFile?.FullName, cancellationToken),
                    cancellationToken: CancellationToken.None
                );
            }
        );

        return command;
    }
}


