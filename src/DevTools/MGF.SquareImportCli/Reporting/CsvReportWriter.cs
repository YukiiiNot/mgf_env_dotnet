namespace MGF.SquareImportCli.Reporting;

using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;

internal sealed class CsvReportWriter<T> : IDisposable
{
    private readonly StreamWriter streamWriter;
    private readonly CsvWriter csvWriter;

    public CsvReportWriter(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path must be non-empty.", nameof(path));
        }

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        streamWriter = new StreamWriter(path, append: false, encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            NewLine = Environment.NewLine,
        };

        csvWriter = new CsvWriter(streamWriter, config);
        csvWriter.WriteHeader<T>();
        csvWriter.NextRecord();
    }

    public void Write(T row)
    {
        csvWriter.WriteRecord(row);
        csvWriter.NextRecord();
    }

    public void Dispose()
    {
        csvWriter.Dispose();
        streamWriter.Dispose();
    }
}


