using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Json.Schema;

namespace MGF.Provisioning;

public sealed class FolderTemplateLoader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
        }
    };

    public async Task<LoadedTemplate> LoadAsync(
        string templatePath,
        string? schemaPathOverride,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(templatePath))
        {
            throw new InvalidOperationException("Template path is required.");
        }

        var fullTemplatePath = Path.GetFullPath(templatePath);
        if (!File.Exists(fullTemplatePath))
        {
            throw new FileNotFoundException("Template file not found.", fullTemplatePath);
        }

        var templateBytes = await File.ReadAllBytesAsync(fullTemplatePath, cancellationToken);
        var templateJson = JsonNode.Parse(templateBytes) ?? throw new InvalidOperationException("Template JSON is empty.");
        var schemaPath = ResolveSchemaPath(fullTemplatePath, schemaPathOverride, templateJson);

        ValidateSchema(schemaPath, templateJson);

        var template = JsonSerializer.Deserialize<FolderTemplate>(templateBytes, SerializerOptions)
            ?? throw new InvalidOperationException("Template JSON did not deserialize.");

        if (template.Root is null)
        {
            throw new InvalidOperationException("Template root is required.");
        }

        return new LoadedTemplate(template, templateBytes, fullTemplatePath, schemaPath);
    }

    private static string ResolveSchemaPath(string templatePath, string? schemaPathOverride, JsonNode templateJson)
    {
        if (!string.IsNullOrWhiteSpace(schemaPathOverride))
        {
            return Path.GetFullPath(schemaPathOverride);
        }

        if (templateJson is not JsonObject obj || !obj.TryGetPropertyValue("$schema", out var schemaNode))
        {
            throw new InvalidOperationException("Template JSON is missing $schema. Provide --schema explicitly.");
        }

        var schemaValue = schemaNode?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(schemaValue))
        {
            throw new InvalidOperationException("Template JSON has empty $schema. Provide --schema explicitly.");
        }

        if (Uri.TryCreate(schemaValue, UriKind.Absolute, out var schemaUri))
        {
            if (schemaUri.Scheme is "http" or "https")
            {
                throw new InvalidOperationException("Remote $schema URLs are not supported. Provide --schema explicitly.");
            }

            if (schemaUri.IsFile)
            {
                return Path.GetFullPath(schemaUri.LocalPath);
            }
        }

        var templateDir = Path.GetDirectoryName(templatePath) ?? string.Empty;
        return Path.GetFullPath(Path.Combine(templateDir, schemaValue));
    }

    private static void ValidateSchema(string schemaPath, JsonNode instance)
    {
        if (!File.Exists(schemaPath))
        {
            throw new FileNotFoundException("Schema file not found.", schemaPath);
        }

        var namingSchemaPath = Path.Combine(Path.GetDirectoryName(schemaPath) ?? string.Empty, "mgf.namingRules.schema.json");
        if (!File.Exists(namingSchemaPath))
        {
            throw new FileNotFoundException("Naming rules schema file not found.", namingSchemaPath);
        }

        var schema = JsonSchema.FromFile(schemaPath);
        var namingSchema = JsonSchema.FromFile(namingSchemaPath);

        var options = new EvaluationOptions
        {
            OutputFormat = OutputFormat.List,
            RequireFormatValidation = true
        };

        options.SchemaRegistry.Register(schema);
        options.SchemaRegistry.Register(namingSchema);

        var results = schema.Evaluate(instance, options);

        if (!results.IsValid)
        {
            throw new InvalidOperationException("Schema validation failed. Fix the template or provide a valid schema.");
        }
    }
}

public sealed record LoadedTemplate(
    FolderTemplate Template,
    byte[] TemplateBytes,
    string TemplatePath,
    string SchemaPath
);

