namespace MGF.Worker.ProjectBootstrap;

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
