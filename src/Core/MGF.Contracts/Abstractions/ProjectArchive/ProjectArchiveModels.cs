namespace MGF.Contracts.Abstractions.ProjectArchive;

using System.Text.Json;

public sealed record ProjectArchivePayload(
    string ProjectId,
    IReadOnlyList<string> EditorInitials,
    bool TestMode,
    bool AllowTestCleanup,
    bool AllowNonReal,
    bool Force
)
{
    public static ProjectArchivePayload Parse(string payloadJson)
    {
        using var doc = JsonDocument.Parse(payloadJson);
        var root = doc.RootElement;

        var projectId = ProjectArchivePayloadParser.TryGetString(root, "projectId");
        if (string.IsNullOrWhiteSpace(projectId))
        {
            throw new InvalidOperationException("projectId is required in project.archive payload.");
        }

        var editorInitials = ProjectArchivePayloadParser.ReadEditorInitials(root);

        return new ProjectArchivePayload(
            ProjectId: projectId,
            EditorInitials: editorInitials,
            TestMode: ProjectArchivePayloadParser.ReadBoolean(root, "testMode", false),
            AllowTestCleanup: ProjectArchivePayloadParser.ReadBoolean(root, "allowTestCleanup", false),
            AllowNonReal: ProjectArchivePayloadParser.ReadBoolean(root, "allowNonReal", false),
            Force: ProjectArchivePayloadParser.ReadBoolean(root, "force", false)
        );
    }
}

public sealed record ProjectArchiveTokens(
    string? ProjectCode,
    string? ProjectName,
    string? ClientName,
    IReadOnlyList<string> EditorInitials
);

public sealed record ProjectArchivePathTemplates(
    string DropboxActiveRelpath,
    string DropboxToArchiveRelpath,
    string DropboxArchiveRelpath,
    string NasArchiveRelpath
);

public sealed record ProjectArchiveRunResult(
    string JobId,
    string ProjectId,
    IReadOnlyList<string> EditorInitials,
    DateTimeOffset StartedAtUtc,
    bool TestMode,
    bool AllowTestCleanup,
    bool AllowNonReal,
    bool Force,
    IReadOnlyList<ProjectArchiveDomainResult> Domains,
    bool HasErrors,
    string? LastError
);

public sealed record ProjectArchiveDomainResult(
    string DomainKey,
    string RootPath,
    string RootState,
    ProvisioningSummary? DomainRootProvisioning,
    ProvisioningSummary? TargetProvisioning,
    IReadOnlyList<ArchiveActionSummary> Actions,
    IReadOnlyList<string> Notes
);

public sealed record ArchiveActionSummary(
    string Action,
    string SourcePath,
    string DestinationPath,
    bool Success,
    string? Error
);

public sealed record ProvisioningSummary(
    string Mode,
    string TemplateKey,
    string TargetRoot,
    string ManifestPath,
    bool Success,
    IReadOnlyList<string> MissingRequired,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings
);

internal static class ProjectArchivePayloadParser
{
    internal static bool ReadBoolean(JsonElement root, string name, bool defaultValue)
    {
        if (root.TryGetProperty(name, out var element) && element.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return element.GetBoolean();
        }

        return defaultValue;
    }

    internal static IReadOnlyList<string> ReadEditorInitials(JsonElement root)
    {
        if (!root.TryGetProperty("editorInitials", out var element))
        {
            return Array.Empty<string>();
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            return element
                .EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => e.GetString() ?? string.Empty)
                .SelectMany(value => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            var raw = element.GetString() ?? string.Empty;
            return raw
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        return Array.Empty<string>();
    }

    internal static string? TryGetString(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }
}
