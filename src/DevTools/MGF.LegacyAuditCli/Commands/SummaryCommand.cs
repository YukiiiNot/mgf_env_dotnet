namespace MGF.Tools.LegacyAudit.Commands;

using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;
using MGF.Tools.LegacyAudit.Models;

internal static class SummaryCommand
{
    public static Command Create()
    {
        var command = new Command("summary", "Print a summary from an existing scan_report.json.");
        var inputOption = new Option<string>("--in") { IsRequired = true, Description = "Path to scan_report.json." };
        command.AddOption(inputOption);

        command.SetHandler(
            (InvocationContext context) =>
            {
                var inputPath = context.ParseResult.GetValueForOption(inputOption) ?? string.Empty;
                if (!File.Exists(inputPath))
                {
                    Console.Error.WriteLine($"report not found: {inputPath}");
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

                var confirmed = report.Classifications.Count(item => item.Classification == "project_confirmed");
                var containerConfirmed = report.Classifications.Count(item => item.Classification == "container_confirmed");
                var roots = report.Classifications.Count(item => item.Classification == "project_root");
                var containers = report.Classifications.Count(item => item.Classification == "project_container");
                var templates = report.Classifications.Count(item => item.Classification == "template_pack");
                var cameraDumps = report.Classifications.Count(item => item.Classification == "camera_dump_subtree");
                var cacheOnly = report.Classifications.Count(item => item.Classification == "cache_only");
                var emptyFolders = report.Classifications.Count(item => item.Classification == "empty_folder");

                Console.WriteLine($"root={report.ScanInfo.RootPath}");
                Console.WriteLine($"profile={report.ScanInfo.Profile}");
                Console.WriteLine($"files={report.Inventory.TotalFiles} dirs={report.Inventory.TotalDirectories} bytes={report.Inventory.TotalBytes}");
                Console.WriteLine($"projects_confirmed={confirmed}");
                Console.WriteLine($"container_confirmed={containerConfirmed}");
                Console.WriteLine($"project_roots={roots}");
                Console.WriteLine($"project_containers={containers}");
                Console.WriteLine($"templates={templates}");
                Console.WriteLine($"camera_dumps={cameraDumps}");
                Console.WriteLine($"cache_only={cacheOnly}");
                Console.WriteLine($"empty_folders={emptyFolders}");
                Console.WriteLine($"errors={report.Errors.Count}");
                context.ExitCode = 0;
            });

        return command;
    }
}
