namespace MGF.Contracts.Abstractions.ProjectDelivery;

using System.Text.Json;
using MGF.Contracts.Abstractions.Email;

public sealed record ProjectDeliveryPayload(
    string ProjectId,
    IReadOnlyList<string> EditorInitials,
    IReadOnlyList<string> ToEmails,
    string? ReplyToEmail,
    bool TestMode,
    bool AllowTestCleanup,
    bool AllowNonReal,
    bool Force,
    bool RefreshShareLink
)
{
    public static ProjectDeliveryPayload Parse(string payloadJson)
    {
        using var doc = JsonDocument.Parse(payloadJson);
        var root = doc.RootElement;

        var projectId = ProjectDeliveryPayloadParser.TryGetString(root, "projectId");
        if (string.IsNullOrWhiteSpace(projectId))
        {
            throw new InvalidOperationException("projectId is required in project.delivery payload.");
        }

        var editorInitials = ProjectDeliveryPayloadParser.ReadEditorInitials(root);
        var toEmails = ProjectDeliveryPayloadParser.ReadEmailAddresses(root, "toEmails");
        var replyToEmail = ProjectDeliveryPayloadParser.TryGetString(root, "replyToEmail");

        return new ProjectDeliveryPayload(
            ProjectId: projectId,
            EditorInitials: editorInitials,
            ToEmails: toEmails,
            ReplyToEmail: replyToEmail,
            TestMode: ProjectDeliveryPayloadParser.ReadBoolean(root, "testMode", false),
            AllowTestCleanup: ProjectDeliveryPayloadParser.ReadBoolean(root, "allowTestCleanup", false),
            AllowNonReal: ProjectDeliveryPayloadParser.ReadBoolean(root, "allowNonReal", false),
            Force: ProjectDeliveryPayloadParser.ReadBoolean(root, "force", false),
            RefreshShareLink: ProjectDeliveryPayloadParser.ReadBoolean(root, "refreshShareLink", false)
        );
    }
}

public sealed record ProjectDeliveryEmailPayload(
    string ProjectId,
    IReadOnlyList<string> EditorInitials,
    IReadOnlyList<string> ToEmails,
    string? ReplyToEmail
)
{
    public static ProjectDeliveryEmailPayload Parse(string payloadJson)
    {
        using var doc = JsonDocument.Parse(payloadJson);
        var root = doc.RootElement;

        var projectId = ProjectDeliveryPayloadParser.TryGetString(root, "projectId");
        if (string.IsNullOrWhiteSpace(projectId))
        {
            throw new InvalidOperationException("projectId is required in project.delivery_email payload.");
        }

        var editorInitials = ProjectDeliveryPayloadParser.ReadEditorInitials(root);
        var toEmails = ProjectDeliveryPayloadParser.ReadEmailAddresses(root, "toEmails");
        var replyToEmail = ProjectDeliveryPayloadParser.TryGetString(root, "replyToEmail");

        return new ProjectDeliveryEmailPayload(
            ProjectId: projectId,
            EditorInitials: editorInitials,
            ToEmails: toEmails,
            ReplyToEmail: replyToEmail
        );
    }
}

public sealed record ProjectDeliveryRunResult(
    string JobId,
    string ProjectId,
    IReadOnlyList<string> EditorInitials,
    DateTimeOffset StartedAtUtc,
    bool TestMode,
    bool AllowTestCleanup,
    bool AllowNonReal,
    bool Force,
    string? SourcePath,
    string? DestinationPath,
    string? ApiStablePath,
    string? ApiVersionPath,
    string? VersionLabel,
    DateTimeOffset? RetentionUntilUtc,
    IReadOnlyList<DeliveryFileSummary> Files,
    IReadOnlyList<ProjectDeliveryDomainResult> Domains,
    bool HasErrors,
    string? LastError,
    string? ShareStatus,
    string? ShareUrl,
    string? ShareId,
    string? ShareError,
    EmailSendResult? Email
);

public sealed record ProjectDeliveryDomainResult(
    string DomainKey,
    string RootPath,
    string RootState,
    ProvisioningSummary? DeliveryContainerProvisioning,
    IReadOnlyList<DeliveryFileSummary> Deliverables,
    string? VersionLabel,
    string? DestinationPath,
    IReadOnlyList<string> Notes
);

public sealed record DeliveryFileSummary(
    /// <summary>
    /// Relative to the version folder (Final\vN).
    /// </summary>
    string RelativePath,
    long SizeBytes,
    DateTimeOffset LastWriteTimeUtc
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

public sealed record ProjectDeliverySourceResult(
    string? SourcePath,
    IReadOnlyList<DeliveryFile> Files,
    ProjectDeliveryDomainResult DomainResult
);

public sealed record ProjectDeliveryTargetResult(
    string? DestinationPath,
    string? ApiStablePath,
    string? ApiVersionPath,
    string? VersionLabel,
    DateTimeOffset? RetentionUntilUtc,
    ProjectDeliveryDomainResult DomainResult,
    string? ShareStatus,
    string? ShareUrl,
    string? ShareId,
    string? ShareError
);

public sealed record DeliveryFile(
    string SourcePath,
    string RelativePath,
    long SizeBytes,
    DateTimeOffset LastWriteTimeUtc
);

public sealed record DeliveryTokens(
    string? ProjectCode,
    string? ProjectName,
    string? ClientName,
    IReadOnlyList<string> EditorInitials
);

internal static class ProjectDeliveryPayloadParser
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

    internal static IReadOnlyList<string> ReadEmailAddresses(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var element))
        {
            return Array.Empty<string>();
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            var values = element
                .EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => e.GetString() ?? string.Empty);
            return NormalizeEmailList(values);
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            var raw = element.GetString() ?? string.Empty;
            return NormalizeEmailList(new[] { raw });
        }

        return Array.Empty<string>();
    }

    internal static string? TryGetString(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static IReadOnlyList<string> NormalizeEmailList(IEnumerable<string> values)
    {
        return values
            .SelectMany(value => (value ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
