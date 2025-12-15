namespace MGF.Tools.SquareImport.Parsing;

using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;

internal static class CsvReaderFactory
{
    public static CsvReader Create(TextReader reader)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            IgnoreBlankLines = true,
            TrimOptions = TrimOptions.Trim,
            DetectDelimiter = true,
            BadDataFound = null,
            MissingFieldFound = null,
            HeaderValidated = null,
            PrepareHeaderForMatch = args => args.Header?.Trim() ?? string.Empty,
        };

        return new CsvReader(reader, config);
    }
}

