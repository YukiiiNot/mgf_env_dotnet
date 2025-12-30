using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Json.Schema;

namespace MGF.TemplateValidation.Tests;

public sealed class TemplateSchemaValidationTests
{
    private static readonly Regex TemplateKeyRegex = new("^[a-z0-9_-]+$", RegexOptions.Compiled);

    [Fact]
    public void AllTemplatesValidateAgainstSchema()
    {
        var templates = GetTemplatePaths();
        Assert.NotEmpty(templates);

        foreach (var templatePath in templates)
        {
            ValidateTemplateSchema(templatePath);
        }
    }

    [Fact]
    public void AllTemplatesHaveValidTemplateKey()
    {
        var templates = GetTemplatePaths();
        Assert.NotEmpty(templates);

        foreach (var templatePath in templates)
        {
            var json = LoadJsonNode(templatePath) as JsonObject
                ?? throw new InvalidOperationException($"Template JSON must be an object: {templatePath}");

            var templateKey = json["templateKey"]?.GetValue<string>();
            Assert.False(string.IsNullOrWhiteSpace(templateKey), $"Template is missing templateKey: {templatePath}");
            Assert.Matches(TemplateKeyRegex, templateKey!);
        }
    }

    private static IReadOnlyList<string> GetTemplatePaths()
    {
        var repoRoot = RepoRootFinder.FindRepoRoot();
        var templatesDir = Path.Combine(repoRoot, "docs", "templates");

        return Directory.EnumerateFiles(templatesDir, "*.json", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void ValidateTemplateSchema(string templatePath)
    {
        var json = LoadJsonNode(templatePath) as JsonObject
            ?? throw new InvalidOperationException($"Template JSON must be an object: {templatePath}");

        var schemaValue = json["$schema"]?.GetValue<string>();
        Assert.False(string.IsNullOrWhiteSpace(schemaValue), $"Template missing $schema: {templatePath}");

        if (schemaValue!.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Remote schemas are not allowed: {templatePath} -> {schemaValue}");
        }

        var schemaPath = ResolveSchemaPath(templatePath, schemaValue);
        Assert.True(File.Exists(schemaPath), $"Schema file not found: {schemaPath}");

        var namingSchemaPath = Path.Combine(Path.GetDirectoryName(schemaPath) ?? string.Empty, "mgf.namingRules.schema.json");
        Assert.True(File.Exists(namingSchemaPath), $"Naming rules schema file not found: {namingSchemaPath}");

        var schema = LoadSchemaFromFile(schemaPath);
        var namingSchema = LoadSchemaFromFile(namingSchemaPath);

        var options = new EvaluationOptions
        {
            OutputFormat = OutputFormat.List,
            RequireFormatValidation = true
        };

        options.SchemaRegistry.Register(schema);
        options.SchemaRegistry.Register(namingSchema);

        var validationNode = json.DeepClone() as JsonObject
            ?? throw new InvalidOperationException($"Template JSON must be an object: {templatePath}");
        validationNode.Remove("$schema");

        var results = schema.Evaluate(validationNode, options);

        Assert.True(results.IsValid, $"Schema validation failed for {templatePath}. Result: {FormatResults(results)}");
    }

    private static JsonNode LoadJsonNode(string templatePath)
    {
        var bytes = File.ReadAllBytes(templatePath);
        var documentOptions = new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        return JsonNode.Parse(bytes, new JsonNodeOptions { PropertyNameCaseInsensitive = true }, documentOptions)
            ?? throw new InvalidOperationException($"Template JSON is empty: {templatePath}");
    }

    private static string ResolveSchemaPath(string templatePath, string schemaValue)
    {
        if (Uri.TryCreate(schemaValue, UriKind.Absolute, out var schemaUri))
        {
            if (schemaUri.IsFile)
            {
                return Path.GetFullPath(schemaUri.LocalPath);
            }
        }

        var templateDir = Path.GetDirectoryName(templatePath) ?? string.Empty;
        return Path.GetFullPath(Path.Combine(templateDir, schemaValue));
    }

    private static JsonSchema LoadSchemaFromFile(string schemaPath)
    {
        var text = File.ReadAllText(schemaPath);
        var normalized = NormalizeSchemaJson(text);
        return JsonSchema.FromText(normalized);
    }

    private static string NormalizeSchemaJson(string text)
    {
        text = Regex.Replace(text, @"(?<!\\)\\-", @"\\-");
        text = Regex.Replace(text, @"(?<!\\)\\#", @"\\#");
        return text;
    }

    private static string FormatResults(EvaluationResults results)
    {
        var builder = new System.Text.StringBuilder();
        AppendResultDetails(results, builder, depth: 0);
        return builder.ToString();
    }

    private static void AppendResultDetails(object result, System.Text.StringBuilder builder, int depth)
    {
        var type = result.GetType();
        var indent = new string(' ', depth * 2);

        var isValid = type.GetProperty("IsValid")?.GetValue(result);
        var instance = type.GetProperty("InstanceLocation")?.GetValue(result);
        var schema = type.GetProperty("SchemaLocation")?.GetValue(result);
        var message = type.GetProperty("Message")?.GetValue(result);

        builder.Append(indent)
            .Append(type.Name);

        if (isValid is not null)
        {
            builder.Append(" IsValid=").Append(isValid);
        }

        if (instance is not null)
        {
            builder.Append(" Instance=").Append(instance);
        }

        if (schema is not null)
        {
            builder.Append(" Schema=").Append(schema);
        }

        if (message is not null)
        {
            builder.Append(" Message=").Append(message);
        }

        builder.AppendLine();

        var details = type.GetProperty("Details")?.GetValue(result) as System.Collections.IEnumerable;
        if (details is null)
        {
            return;
        }

        foreach (var detail in details)
        {
            if (detail is null)
            {
                continue;
            }

            AppendResultDetails(detail, builder, depth + 1);
        }
    }
}

internal static class RepoRootFinder
{
    public static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "MGF.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException($"Could not locate repo root from {AppContext.BaseDirectory}.");
    }
}
