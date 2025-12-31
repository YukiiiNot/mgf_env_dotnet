namespace MGF.LegacyAuditCli.Commands;

using System.CommandLine;

internal static class RootCommandFactory
{
    public static RootCommand Create()
    {
        var root = new RootCommand("MGF: Legacy NAS audit tool (read-only)");

        root.AddCommand(ScanCommand.Create());
        root.AddCommand(SummaryCommand.Create());
        root.AddCommand(ExportCommand.Create());

        return root;
    }
}

