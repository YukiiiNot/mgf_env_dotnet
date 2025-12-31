namespace MGF.Tools.SquareImport.Reporting;

public sealed record ImportSummary(int Inserted, int Updated, int Skipped, int Errors)
{
    public static ImportSummary Empty() => new(Inserted: 0, Updated: 0, Skipped: 0, Errors: 0);

    public void WriteToConsole(string commandName)
    {
        Console.WriteLine($"square-import {commandName}: summary inserted={Inserted} updated={Updated} skipped={Skipped} errors={Errors}");
    }
}

