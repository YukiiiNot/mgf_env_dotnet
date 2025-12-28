namespace MGF.Worker.ProjectArchive;

public sealed record ProjectArchivePayload(
    string ProjectId,
    IReadOnlyList<string> EditorInitials,
    bool TestMode,
    bool AllowTestCleanup,
    bool AllowNonReal,
    bool Force
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
