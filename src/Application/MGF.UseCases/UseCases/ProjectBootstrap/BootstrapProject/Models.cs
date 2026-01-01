namespace MGF.UseCases.ProjectBootstrap.BootstrapProject;

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
);

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
