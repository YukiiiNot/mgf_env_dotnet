namespace MGF.Tools.SquareImport.Commands;

using System.CommandLine;

internal static class RootCommandFactory
{
    public static RootCommand Create()
    {
        var root = new RootCommand("MGF: Square CSV import tool");

        root.AddCommand(CustomersCommand.Create());
        root.AddCommand(TransactionsCommand.Create());
        root.AddCommand(InvoicesCommand.Create());
        root.AddCommand(ReportCommand.Create());

        return root;
    }
}

