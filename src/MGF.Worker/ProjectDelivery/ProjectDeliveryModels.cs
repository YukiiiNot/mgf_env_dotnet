namespace MGF.Worker.ProjectDelivery;

public sealed record ProjectDeliveryPayload(
    string ProjectId,
    IReadOnlyList<string> EditorInitials,
    bool TestMode,
    bool AllowTestCleanup,
    bool AllowNonReal,
    bool Force,
    bool RefreshShareLink
);

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
    string? VersionLabel,
    DateTimeOffset? RetentionUntilUtc,
    IReadOnlyList<DeliveryFileSummary> Files,
    IReadOnlyList<ProjectDeliveryDomainResult> Domains,
    bool HasErrors,
    string? LastError,
    string? ShareStatus,
    string? ShareUrl,
    string? ShareId,
    string? ShareError
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
