using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MGF.Domain.Entities;

namespace MGF.Infrastructure.Data.SchemaDocs;

internal static class SchemaDocModelBuilder
{
    private static readonly IReadOnlyDictionary<string, Type> CoreClrTables =
        new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
        {
            ["clients"] = typeof(Client),
            ["people"] = typeof(Person),
            ["projects"] = typeof(Project),
        };

    public static void Apply(ModelBuilder modelBuilder)
    {
        var tables = SchemaDocPack.GetTables();

        foreach (var table in tables)
        {
            if (CoreClrTables.TryGetValue(table.TableName, out var clrType))
            {
                ApplyClrEntity(modelBuilder, table, clrType);
                continue;
            }

            ApplyPropertyBagEntity(modelBuilder, table);
        }
    }

    private static void ApplyClrEntity(ModelBuilder modelBuilder, SchemaDocTable table, Type clrType)
    {
        var entity = modelBuilder.Entity(clrType);
        entity.ToTable(table.TableName);

        foreach (var column in table.Columns)
        {
            var propertyName = SnakeToPascal(column.FieldKey);
            var property = entity.Property(propertyName);

            property.HasColumnName(column.FieldKey);
            property.HasColumnType(NormalizeDbType(column.DbType));
            property.IsRequired(!column.Nullable);

            ApplyDefault(property, column);
        }

        entity.HasKey(table.PrimaryKeyColumns.Select(SnakeToPascal).ToArray());

        ApplyEntityConstraints(entity, table);
        ApplyEntityIndexes(entity, table);
        ApplyEntityForeignKeys(entity, table, dependentIsClr: true);
    }

    private static void ApplyPropertyBagEntity(ModelBuilder modelBuilder, SchemaDocTable table)
    {
        var entity = modelBuilder.SharedTypeEntity<Dictionary<string, object>>(table.TableName);
        entity.ToTable(table.TableName);

        foreach (var column in table.Columns)
        {
            var clrType = GetClrTypeForColumn(column);
            var property = entity.IndexerProperty(clrType, column.FieldKey);

            property.HasColumnName(column.FieldKey);
            property.HasColumnType(NormalizeDbType(column.DbType));
            property.IsRequired(!column.Nullable);

            ApplyDefault(property, column);
        }

        entity.HasKey(table.PrimaryKeyColumns.ToArray());

        ApplyEntityConstraints(entity, table);
        ApplyEntityIndexes(entity, table);
        ApplyEntityForeignKeys(entity, table, dependentIsClr: false);
    }

    private static void ApplyEntityConstraints(EntityTypeBuilder entity, SchemaDocTable table)
    {
        var checkConstraints = new List<string>();
        var uniqueConstraints = new List<IReadOnlyList<string>>();

        foreach (var column in table.Columns)
        {
            if (string.IsNullOrWhiteSpace(column.Constraints))
            {
                continue;
            }

            checkConstraints.AddRange(ExtractCheckExpressions(column.Constraints));

            foreach (var unique in ExtractUniqueColumns(column.Constraints))
            {
                uniqueConstraints.Add(unique.Count == 0 ? new[] { column.FieldKey } : unique);
            }
        }

        var checkIndex = 0;
        foreach (var check in checkConstraints.Distinct(StringComparer.Ordinal))
        {
            var name = $"CK_{table.TableName}_{checkIndex}";
            entity.ToTable(tb => tb.HasCheckConstraint(name, check));
            checkIndex++;
        }

        foreach (var unique in uniqueConstraints)
        {
            // CLR entities use property names, property bags use column names.
            var cols = unique.Select(c => CoreClrTables.ContainsKey(table.TableName) ? SnakeToPascal(c) : c).ToArray();
            entity.HasIndex(cols).IsUnique();
        }
    }

    private static void ApplyEntityIndexes(EntityTypeBuilder entity, SchemaDocTable table)
    {
        var indexes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var column in table.Columns)
        {
            if (!column.Indexed)
            {
                continue;
            }

            var key = CoreClrTables.ContainsKey(table.TableName) ? SnakeToPascal(column.FieldKey) : column.FieldKey;
            if (indexes.Add(key))
            {
                entity.HasIndex(key);
            }
        }
    }

    private static void ApplyEntityForeignKeys(EntityTypeBuilder entity, SchemaDocTable table, bool dependentIsClr)
    {
        foreach (var column in table.Columns)
        {
            if (string.IsNullOrWhiteSpace(column.FkTarget))
            {
                continue;
            }

            var (principalTable, _) = ParseFkTarget(column.FkTarget);

            var fkProperty = dependentIsClr ? SnakeToPascal(column.FieldKey) : column.FieldKey;

            if (CoreClrTables.TryGetValue(principalTable, out var principalClr))
            {
                entity.HasOne(principalClr)
                    .WithMany()
                    .HasForeignKey(fkProperty)
                    .OnDelete(DeleteBehavior.Restrict);
                continue;
            }

            entity.HasOne(principalTable, null)
                .WithMany()
                .HasForeignKey(fkProperty)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }

    private static (string Table, string Column) ParseFkTarget(string fkTarget)
    {
        var parts = fkTarget.Split('.', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
        {
            throw new InvalidOperationException($"Invalid FKTarget '{fkTarget}'. Expected 'table.column'.");
        }

        return (parts[0], parts[1]);
    }

    private static void ApplyDefault(PropertyBuilder property, SchemaDocColumn column)
    {
        if (string.IsNullOrWhiteSpace(column.Default))
        {
            return;
        }

        var value = column.Default.Trim();
        var normalizedType = NormalizeDbType(column.DbType);

        if (normalizedType.Equals("text", StringComparison.OrdinalIgnoreCase) && !LooksLikeSqlExpression(value))
        {
            property.HasDefaultValue(value);
            return;
        }

        if (normalizedType.Equals("jsonb", StringComparison.OrdinalIgnoreCase) && value == "{}")
        {
            property.HasDefaultValueSql("'{}'::jsonb");
            return;
        }

        if (value.Equals("true", StringComparison.OrdinalIgnoreCase) || value.Equals("false", StringComparison.OrdinalIgnoreCase))
        {
            property.HasDefaultValue(value.Equals("true", StringComparison.OrdinalIgnoreCase));
            return;
        }

        if (int.TryParse(value, out var intValue) && normalizedType is "integer" or "int" or "smallint")
        {
            property.HasDefaultValue(intValue);
            return;
        }

        if (decimal.TryParse(value, out var decValue) && normalizedType.StartsWith("numeric", StringComparison.OrdinalIgnoreCase))
        {
            property.HasDefaultValue(decValue);
            return;
        }

        property.HasDefaultValueSql(value);
    }

    private static bool LooksLikeSqlExpression(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        // e.g. now(), gen_random_uuid(), '{}'::jsonb, etc.
        if (value.Contains('(') || value.Contains(')') || value.Contains("::", StringComparison.Ordinal))
        {
            return true;
        }

        // If it already looks like a quoted SQL literal, treat it as SQL.
        if (value.StartsWith('\'') && value.EndsWith('\'') && value.Length >= 2)
        {
            return true;
        }

        return false;
    }

    private static string NormalizeDbType(string dbType)
    {
        if (dbType.Equals("int", StringComparison.OrdinalIgnoreCase))
        {
            return "integer";
        }

        return dbType;
    }

    private static Type GetClrTypeForColumn(SchemaDocColumn column)
    {
        var dbType = NormalizeDbType(column.DbType);

        Type baseType;

        if (dbType.Equals("text", StringComparison.OrdinalIgnoreCase) || dbType.Equals("uuid", StringComparison.OrdinalIgnoreCase))
        {
            baseType = typeof(string);
        }
        else if (dbType.Equals("integer", StringComparison.OrdinalIgnoreCase))
        {
            baseType = typeof(int);
        }
        else if (dbType.Equals("smallint", StringComparison.OrdinalIgnoreCase))
        {
            baseType = typeof(short);
        }
        else if (dbType.Equals("boolean", StringComparison.OrdinalIgnoreCase))
        {
            baseType = typeof(bool);
        }
        else if (dbType.Equals("timestamptz", StringComparison.OrdinalIgnoreCase))
        {
            baseType = typeof(DateTimeOffset);
        }
        else if (dbType.Equals("date", StringComparison.OrdinalIgnoreCase))
        {
            baseType = typeof(DateOnly);
        }
        else if (dbType.Equals("jsonb", StringComparison.OrdinalIgnoreCase))
        {
            baseType = typeof(JsonElement);
        }
        else if (dbType.StartsWith("numeric", StringComparison.OrdinalIgnoreCase))
        {
            baseType = typeof(decimal);
        }
        else
        {
            baseType = typeof(string);
        }

        if (!column.Nullable || !baseType.IsValueType || Nullable.GetUnderlyingType(baseType) is not null)
        {
            return baseType;
        }

        return typeof(Nullable<>).MakeGenericType(baseType);
    }

    private static IEnumerable<IReadOnlyList<string>> ExtractUniqueColumns(string constraints)
    {
        var text = constraints;
        var idx = 0;

        while (true)
        {
            idx = text.IndexOf("UNIQUE", idx, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
            {
                yield break;
            }

            idx += "UNIQUE".Length;

            // UNIQUE (a, b) form
            var open = NextNonWhitespaceIndex(text, idx);
            if (open >= 0 && open < text.Length && text[open] == '(')
            {
                var inside = ReadBalancedParentheses(text, open);
                if (!string.IsNullOrWhiteSpace(inside))
                {
                    yield return inside.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                }

                idx = open + inside.Length + 2;
                continue;
            }

            // Column-level UNIQUE (no columns listed): handled by the caller with the current column.
            // Return empty list to signal "unique on this column".
            yield return Array.Empty<string>();
        }
    }

    private static IEnumerable<string> ExtractCheckExpressions(string constraints)
    {
        var text = constraints;
        var idx = 0;

        while (true)
        {
            idx = text.IndexOf("CHECK", idx, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
            {
                yield break;
            }

            idx += "CHECK".Length;
            var open = NextNonWhitespaceIndex(text, idx);
            if (open < 0 || open >= text.Length || text[open] != '(')
            {
                continue;
            }

            var inside = ReadBalancedParentheses(text, open);
            if (!string.IsNullOrWhiteSpace(inside))
            {
                yield return inside;
            }

            idx = open + inside.Length + 2;
        }
    }

    private static int NextNonWhitespaceIndex(string text, int start)
    {
        var i = start;
        while (i < text.Length && char.IsWhiteSpace(text[i]))
        {
            i++;
        }

        return i;
    }

    private static string ReadBalancedParentheses(string text, int openParenIndex)
    {
        var depth = 0;
        var inSingleQuotes = false;

        for (var i = openParenIndex; i < text.Length; i++)
        {
            var c = text[i];

            if (c == '\'')
            {
                // Handle escaped '' inside quoted strings.
                if (inSingleQuotes && i + 1 < text.Length && text[i + 1] == '\'')
                {
                    i++;
                    continue;
                }

                inSingleQuotes = !inSingleQuotes;
                continue;
            }

            if (inSingleQuotes)
            {
                continue;
            }

            if (c == '(')
            {
                depth++;
                continue;
            }

            if (c == ')')
            {
                depth--;
                if (depth == 0)
                {
                    return text.Substring(openParenIndex + 1, i - openParenIndex - 1).Trim();
                }
            }
        }

        return string.Empty;
    }

    private static string SnakeToPascal(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var parts = value.Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return value;
        }

        return string.Concat(parts.Select(p => char.ToUpperInvariant(p[0]) + p[1..]));
    }
}
