using System.Text;

namespace MGF.Tools.LegacyAudit.Reporting;

internal static class CsvWriter
{
    public static void Write(string path, IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string?>> rows)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");

        using var writer = new StreamWriter(path, false, new UTF8Encoding(false));
        WriteRow(writer, headers);
        foreach (var row in rows)
        {
            WriteRow(writer, row);
        }
    }

    private static void WriteRow(TextWriter writer, IReadOnlyList<string?> values)
    {
        for (var i = 0; i < values.Count; i++)
        {
            if (i > 0)
            {
                writer.Write(',');
            }

            writer.Write(Escape(values[i] ?? string.Empty));
        }

        writer.WriteLine();
    }

    private static string Escape(string value)
    {
        var needsQuotes = value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r');
        if (!needsQuotes)
        {
            return value;
        }

        var escaped = value.Replace("\"", "\"\"");
        return $"\"{escaped}\"";
    }
}
