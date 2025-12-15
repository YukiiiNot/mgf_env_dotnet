using Microsoft.VisualBasic.FileIO;

namespace MGF.Infrastructure.Data.SchemaDocs;

internal enum SchemaDocTableGroup
{
    Lookup,
    Core,
    Join,
}

internal sealed record SchemaDocColumn(
    string FieldKey,
    string DbType,
    bool Primary,
    bool Nullable,
    string? Default,
    bool Computed,
    string? DerivesFrom,
    string? FkTarget,
    string? EnumGroup,
    string? Constraints,
    bool Indexed,
    string? Description,
    string? Example,
    string? Notes
);

internal sealed class SchemaDocTable
{
    public required string TableName { get; init; }
    public required string CsvPath { get; init; }
    public required SchemaDocTableGroup Group { get; init; }
    public required IReadOnlyList<SchemaDocColumn> Columns { get; init; }
    public required IReadOnlyList<string> PrimaryKeyColumns { get; init; }
}

internal static class SchemaDocPack
{
    private const string SchemaRootRel = "docs\\db_design\\schema_csv\\_core";

    private static readonly Lazy<IReadOnlyList<SchemaDocTable>> CachedTables = new(LoadTables);

    public static IReadOnlyList<SchemaDocTable> GetTables() => CachedTables.Value;

    public static string GetSchemaRoot()
    {
        var repoRoot = FindRepoRoot();
        return Path.Combine(repoRoot, SchemaRootRel);
    }

    private static IReadOnlyList<SchemaDocTable> LoadTables()
    {
        var schemaRoot = GetSchemaRoot();
        if (!Directory.Exists(schemaRoot))
        {
            throw new InvalidOperationException($"Schema docs root not found: {schemaRoot}");
        }

        var csvFiles = Directory.GetFiles(schemaRoot, "*_schema_documentation.csv", System.IO.SearchOption.AllDirectories);
        Array.Sort(csvFiles, StringComparer.OrdinalIgnoreCase);

        var tables = new List<SchemaDocTable>(csvFiles.Length);

        foreach (var file in csvFiles)
        {
            var tableName = Path.GetFileName(file).Replace("_schema_documentation.csv", "", StringComparison.OrdinalIgnoreCase);
            var group = GetGroupForFile(file);

            var rows = ReadCsvRows(file);
            var columns = rows.Select(r => ToColumn(r, file)).ToList();
            var primaryKeyColumns = GetPrimaryKeyColumns(tableName, columns);

            tables.Add(
                new SchemaDocTable
                {
                    TableName = tableName,
                    CsvPath = Path.GetRelativePath(schemaRoot, file),
                    Group = group,
                    Columns = columns,
                    PrimaryKeyColumns = primaryKeyColumns,
                }
            );
        }

        return tables;
    }

    private static SchemaDocTableGroup GetGroupForFile(string file)
    {
        var normalized = file.Replace('/', '\\');
        if (normalized.Contains("\\_lookup\\", StringComparison.OrdinalIgnoreCase))
        {
            return SchemaDocTableGroup.Lookup;
        }

        if (normalized.Contains("\\_join\\", StringComparison.OrdinalIgnoreCase))
        {
            return SchemaDocTableGroup.Join;
        }

        return SchemaDocTableGroup.Core;
    }

    private static IReadOnlyList<Dictionary<string, string?>> ReadCsvRows(string filePath)
    {
        using var parser = new TextFieldParser(filePath)
        {
            TextFieldType = FieldType.Delimited,
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = false,
        };

        parser.SetDelimiters(",");

        var headers = parser.ReadFields();
        if (headers is null || headers.Length == 0)
        {
            throw new InvalidOperationException($"Schema CSV has no header row: {filePath}");
        }

        var rows = new List<Dictionary<string, string?>>();
        while (!parser.EndOfData)
        {
            var fields = parser.ReadFields();
            if (fields is null)
            {
                continue;
            }

            var row = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < headers.Length; i++)
            {
                var key = headers[i];
                var value = i < fields.Length ? fields[i] : null;
                row[key] = string.IsNullOrWhiteSpace(value) ? null : value;
            }

            if (row.TryGetValue("FieldKey", out var fieldKey) && string.IsNullOrWhiteSpace(fieldKey))
            {
                continue;
            }

            rows.Add(row);
        }

        return rows;
    }

    private static SchemaDocColumn ToColumn(Dictionary<string, string?> row, string filePath)
    {
        string Required(string name)
        {
            if (!row.TryGetValue(name, out var value) || string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException($"Missing required column '{name}' in {filePath}.");
            }

            return value!;
        }

        var fieldKey = Required("FieldKey");
        var type = Required("Type");

        return new SchemaDocColumn(
            FieldKey: fieldKey,
            DbType: type,
            Primary: ParseBool(row.GetValueOrDefault("Primary")),
            Nullable: ParseBool(row.GetValueOrDefault("Nullable")),
            Default: row.GetValueOrDefault("Default"),
            Computed: ParseBool(row.GetValueOrDefault("Computed")),
            DerivesFrom: row.GetValueOrDefault("DerivesFrom"),
            FkTarget: row.GetValueOrDefault("FKTarget"),
            EnumGroup: row.GetValueOrDefault("EnumGroup"),
            Constraints: row.GetValueOrDefault("Constraints"),
            Indexed: ParseBool(row.GetValueOrDefault("Indexed")),
            Description: row.GetValueOrDefault("Description"),
            Example: row.GetValueOrDefault("Example"),
            Notes: row.GetValueOrDefault("Notes")
        );
    }

    private static IReadOnlyList<string> GetPrimaryKeyColumns(string tableName, IReadOnlyList<SchemaDocColumn> columns)
    {
        var primary = columns.Where(c => c.Primary).Select(c => c.FieldKey).ToList();
        if (primary.Count > 0)
        {
            return primary;
        }

        // Some legacy CSVs capture the intended PK in Notes rather than in Primary=true.
        var notes = string.Join(" ", columns.Select(c => c.Notes).Where(n => !string.IsNullOrWhiteSpace(n)));
        var pkFromNotes = TryParseCompositePkFromNotes(notes);
        if (pkFromNotes is not null && pkFromNotes.Count > 0)
        {
            return pkFromNotes;
        }

        // Known doc gap: client_contacts is a join table intended to be keyed by (client_id, person_id).
        if (string.Equals(tableName, "client_contacts", StringComparison.OrdinalIgnoreCase))
        {
            return new[] { "client_id", "person_id" };
        }

        throw new InvalidOperationException(
            $"No primary key columns detected for table '{tableName}'. Mark Primary=true in the CSV docs."
        );
    }

    private static List<string>? TryParseCompositePkFromNotes(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            return null;
        }

        var marker = "Composite PK";
        var idx = notes.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
        {
            return null;
        }

        var open = notes.IndexOf('(', idx);
        var close = notes.IndexOf(')', open + 1);
        if (open < 0 || close < 0 || close <= open)
        {
            return null;
        }

        var inside = notes.Substring(open + 1, close - open - 1);
        return inside.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }

    private static bool ParseBool(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Trim().Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase)
            || value.Trim().Equals("1", StringComparison.OrdinalIgnoreCase);
    }

    private static string FindRepoRoot()
    {
        return TryFindRepoRootFrom(Directory.GetCurrentDirectory())
            ?? TryFindRepoRootFrom(AppContext.BaseDirectory)
            ?? throw new InvalidOperationException("Could not locate repo root (MGF.sln). Run from within the repo.");
    }

    private static string? TryFindRepoRootFrom(string startPath)
    {
        var dir = new DirectoryInfo(startPath);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "MGF.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return null;
    }
}
