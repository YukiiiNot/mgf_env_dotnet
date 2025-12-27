namespace MGF.Tools.LegacyAudit.Commands;

using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;
using MGF.Tools.LegacyAudit.Models;
using MGF.Tools.LegacyAudit.Reporting;

internal static class ExportCommand
{
    public static Command Create()
    {
        var command = new Command("export", "Re-export CSVs from an existing scan_report.json.");
        var inputOption = new Option<string>("--in") { IsRequired = true, Description = "Path to scan_report.json." };
        var outputOption = new Option<string>("--out") { Description = "Output folder for CSVs (defaults under ./runtime)." };
        var applyOption = new Option<bool>("--apply", "Write CSVs to disk (required).");

        command.AddOption(inputOption);
        command.AddOption(outputOption);
        command.AddOption(applyOption);

        command.SetHandler(
            (InvocationContext context) =>
            {
                var inputPath = context.ParseResult.GetValueForOption(inputOption) ?? string.Empty;
                var outputPath = context.ParseResult.GetValueForOption(outputOption) ?? string.Empty;
                var apply = context.ParseResult.GetValueForOption(applyOption);

                if (!Guardrails.EnsureApply(apply, out var applyError))
                {
                    Console.Error.WriteLine(applyError);
                    context.ExitCode = 1;
                    return;
                }

                if (!File.Exists(inputPath))
                {
                    Console.Error.WriteLine($"report not found: {inputPath}");
                    context.ExitCode = 1;
                    return;
                }

                if (!Guardrails.TryFindRepoRoot(out var repoRoot, out var repoError))
                {
                    Console.Error.WriteLine(repoError);
                    context.ExitCode = 1;
                    return;
                }

                var defaultOutput = Guardrails.GetDefaultExportOutputPath(repoRoot, inputPath);
                if (!Guardrails.TryResolveOutputPath(repoRoot, outputPath, defaultOutput, out var resolvedOutput, out var outputError))
                {
                    Console.Error.WriteLine(outputError);
                    context.ExitCode = 1;
                    return;
                }

                var json = File.ReadAllText(inputPath);
                var report = JsonSerializer.Deserialize<ScanReport>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (report is null)
                {
                    Console.Error.WriteLine("failed to parse report");
                    context.ExitCode = 1;
                    return;
                }

                ReportWriter.WriteCsvs(report, resolvedOutput);
                Console.WriteLine($"csvs written to {resolvedOutput}");
                context.ExitCode = 0;
            });

        return command;
    }
}
