namespace MGF.Contracts.Abstractions.ProjectBootstrap;

using System.Text.Json;

public sealed record BootstrapProjectRequest(
    string JobId,
    string ProjectId,
    IReadOnlyList<string> EditorInitials,
    bool VerifyDomainRoots,
    bool CreateDomainRoots,
    bool ProvisionProjectContainers,
    bool AllowRepair,
    bool ForceSandbox,
    bool AllowNonReal,
    bool Force,
    bool TestMode,
    bool AllowTestCleanup
);

public sealed record BootstrapProjectResult(ProjectBootstrapRunResult RunResult);

public sealed record ProjectBootstrapPayload(
    string ProjectId,
    IReadOnlyList<string> EditorInitials,
    bool VerifyDomainRoots,
    bool CreateDomainRoots,
    bool ProvisionProjectContainers,
    bool AllowRepair,
    bool ForceSandbox,
    bool AllowNonReal,
    bool Force,
    bool TestMode,
    bool AllowTestCleanup
)
{
    public static ProjectBootstrapPayload Parse(string payloadJson)
    {
        using var doc = JsonDocument.Parse(payloadJson);
        var root = doc.RootElement;

        var projectId = root.TryGetProperty("projectId", out var projectIdElement) ? projectIdElement.GetString() : null;
        if (string.IsNullOrWhiteSpace(projectId))
        {
            throw new InvalidOperationException("projectId is required in project.bootstrap payload.");
        }

        var editorInitials = ReadEditorInitials(root);

        return new ProjectBootstrapPayload(
            ProjectId: projectId,
            EditorInitials: editorInitials,
            VerifyDomainRoots: ReadBoolean(root, "verifyDomainRoots", true),
            CreateDomainRoots: ReadBoolean(root, "createDomainRoots", false),
            ProvisionProjectContainers: ReadBoolean(root, "provisionProjectContainers", false),
            AllowRepair: ReadBoolean(root, "allowRepair", false),
            ForceSandbox: ReadBoolean(root, "forceSandbox", false),
            AllowNonReal: ReadBoolean(root, "allowNonReal", false),
            Force: ReadBoolean(root, "force", false),
            TestMode: ReadBoolean(root, "testMode", false),
            AllowTestCleanup: ReadBoolean(root, "allowTestCleanup", false)
        );
    }

    private static bool ReadBoolean(JsonElement root, string name, bool defaultValue)
    {
        if (root.TryGetProperty(name, out var element) && element.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return element.GetBoolean();
        }

        return defaultValue;
    }

    private static IReadOnlyList<string> ReadEditorInitials(JsonElement root)
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
}

public sealed record ProjectBootstrapRunResult(
    string JobId,
    string ProjectId,
    IReadOnlyList<string> EditorInitials,
    DateTimeOffset StartedAtUtc,
    bool VerifyDomainRoots,
    bool CreateDomainRoots,
    bool ProvisionProjectContainers,
    bool AllowRepair,
    bool ForceSandbox,
    bool AllowNonReal,
    bool Force,
    bool TestMode,
    bool AllowTestCleanup,
    IReadOnlyList<ProjectBootstrapDomainResult> Domains,
    bool HasErrors,
    string? LastError
);

public sealed record ProjectBootstrapDomainResult(
    string DomainKey,
    string RootPath,
    string RootState,
    ProvisioningSummary? DomainRootProvisioning,
    ProvisioningSummary? ProjectContainerProvisioning,
    IReadOnlyList<string> Notes
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

public sealed record ProjectBootstrapContext(
    string ProjectId,
    string ProjectCode,
    string ProjectName,
    string ClientId,
    string? ClientName,
    string StatusKey,
    string DataProfile,
    JsonElement Metadata
);

public sealed record ProjectBootstrapStorageRootCandidate(
    string DomainKey,
    string StorageProviderKey,
    string RootKey,
    string FolderRelpath
);

public sealed record ProjectBootstrapExecutionResult(
    ProjectBootstrapRunResult RunResult,
    IReadOnlyList<ProjectBootstrapStorageRootCandidate> StorageRootCandidates,
    Exception? Exception
);
