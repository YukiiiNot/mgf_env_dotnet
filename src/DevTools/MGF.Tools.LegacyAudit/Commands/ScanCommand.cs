namespace MGF.Tools.LegacyAudit.Commands;

using System.CommandLine;
using System.CommandLine.Invocation;
using MGF.Tools.LegacyAudit.Reporting;
using MGF.Tools.LegacyAudit.Scanning;

internal static class ScanCommand
{
    public static Command Create()
    {
        var command = new Command("scan", @"Scan a NAS root (read-only) and produce audit reports.

Examples:
  dotnet run --project src/DevTools/MGF.Tools.LegacyAudit -- scan --root ""\\Truenas\zan4k pool\OFFLOAD 2"" --out ""C:\mgf_audit_out\OFFLOAD_2""
  dotnet run --project src/DevTools/MGF.Tools.LegacyAudit -- scan --root ""\\Truenas\zana 10tb - 01\Sector 2"" --out ""C:\mgf_audit_out\ZANA10_Sector2""");

        var rootOption = new Option<string>("--root") { IsRequired = true, Description = "UNC path to scan (required)." };
        var outOption = new Option<string>("--out") { Description = "Local output folder for reports (defaults under ./runtime)." };
        var applyOption = new Option<bool>("--apply", "Write reports to disk (required).");
        var profileOption = new Option<string>("--profile", () => "editorial", "Scan profile: editorial|everything.");
        var maxDepthOption = new Option<int>("--max-depth", () => -1, "Max depth to scan (-1 = unlimited)." );

        command.AddOption(rootOption);
        command.AddOption(outOption);
        command.AddOption(applyOption);
        command.AddOption(profileOption);
        command.AddOption(maxDepthOption);

        command.SetHandler(
            (InvocationContext context) =>
            {
                var root = context.ParseResult.GetValueForOption(rootOption) ?? string.Empty;
                var output = context.ParseResult.GetValueForOption(outOption) ?? string.Empty;
                var apply = context.ParseResult.GetValueForOption(applyOption);
                var profileText = context.ParseResult.GetValueForOption(profileOption) ?? string.Empty;
                var maxDepth = context.ParseResult.GetValueForOption(maxDepthOption);

                if (!Guardrails.EnsureApply(apply, out var applyError))
                {
                    Console.Error.WriteLine(applyError);
                    context.ExitCode = 1;
                    return;
                }

                if (!ScanProfileRules.TryParse(profileText, out var profile))
                {
                    Console.Error.WriteLine($"Unknown profile '{profileText}'. Use 'editorial' or 'everything'.");
                    context.ExitCode = 1;
                    return;
                }

                if (!Guardrails.TryFindRepoRoot(out var repoRoot, out var repoError))
                {
                    Console.Error.WriteLine(repoError);
                    context.ExitCode = 1;
                    return;
                }

                var defaultOutput = Guardrails.GetDefaultScanOutputPath(repoRoot, root);
                if (!Guardrails.TryResolveOutputPath(repoRoot, output, defaultOutput, out var resolvedOutput, out var outputError))
                {
                    Console.Error.WriteLine(outputError);
                    context.ExitCode = 1;
                    return;
                }

                var scanner = new LegacyScanner();
                var options = new ScanOptions
                {
                    RootPath = root,
                    OutputPath = resolvedOutput,
                    Profile = profile,
                    MaxDepth = maxDepth
                };

                try
                {
                    var report = scanner.Scan(options, context.GetCancellationToken());
                    ReportWriter.WriteAll(report, resolvedOutput);
                    WriteSummaryToConsole(report);
                    context.ExitCode = 0;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"scan failed: {ex.Message}");
                    context.ExitCode = 1;
                }
            });

        return command;
    }

    private static void WriteSummaryToConsole(Models.ScanReport report)
    {
        var confirmed = report.Classifications.Count(item => item.Classification == "project_confirmed");
        var containerConfirmed = report.Classifications.Count(item => item.Classification == "container_confirmed");
        var roots = report.Classifications.Count(item => item.Classification == "project_root");
        var containers = report.Classifications.Count(item => item.Classification == "project_container");
        var templates = report.Classifications.Count(item => item.Classification == "template_pack");
        var cameraDumps = report.Classifications.Count(item => item.Classification == "camera_dump_subtree");
        var cacheOnly = report.Classifications.Count(item => item.Classification == "cache_only");
        var emptyFolders = report.Classifications.Count(item => item.Classification == "empty_folder");

        Console.WriteLine($"scan complete: files={report.Inventory.TotalFiles} dirs={report.Inventory.TotalDirectories} bytes={report.Inventory.TotalBytes}");
        Console.WriteLine($"projects_confirmed={confirmed} container_confirmed={containerConfirmed} project_roots={roots} project_containers={containers}");
        Console.WriteLine($"templates={templates} camera_dumps={cameraDumps} cache_only={cacheOnly} empty_folders={emptyFolders}");
        Console.WriteLine($"errors={report.Errors.Count}");
    }
}
