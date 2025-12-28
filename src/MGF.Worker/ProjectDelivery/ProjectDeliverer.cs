namespace MGF.Worker.ProjectDelivery;

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MGF.Infrastructure.Data;
using MGF.Tools.Provisioner;
using MGF.Worker.ProjectBootstrap;

public sealed class ProjectDeliverer
{
    private const int MaxRunsToKeep = 10;
    private const int DefaultRetentionMonths = 3;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mov", ".mxf", ".wav", ".mp3", ".m4a", ".aif", ".aiff", ".srt", ".vtt", ".xml", ".pdf"
    };

    private readonly IConfiguration configuration;
    private readonly LocalFileStore fileStore = new();
    private readonly FolderProvisioner provisioner;

    public ProjectDeliverer(IConfiguration configuration)
    {
        this.configuration = configuration;
        provisioner = new FolderProvisioner(fileStore);
    }

    public async Task<ProjectDeliveryRunResult> RunAsync(
        AppDbContext db,
        ProjectDeliveryPayload payload,
        string jobId,
        CancellationToken cancellationToken)
    {
        var project = await db.Projects.AsNoTracking()
            .SingleOrDefaultAsync(p => p.ProjectId == payload.ProjectId, cancellationToken);

        if (project is null)
        {
            throw new InvalidOperationException($"Project not found: {payload.ProjectId}");
        }

        var startedAt = DateTimeOffset.UtcNow;

        if (!payload.AllowNonReal && !string.Equals(project.DataProfile, "real", StringComparison.OrdinalIgnoreCase))
        {
            var blocked = BuildBlockedResults(
                "blocked_non_real",
                $"Project data_profile='{project.DataProfile}' is not eligible for delivery."
            );

            var blockedResult = new ProjectDeliveryRunResult(
                JobId: jobId,
                ProjectId: payload.ProjectId,
                EditorInitials: payload.EditorInitials,
                StartedAtUtc: startedAt,
                TestMode: payload.TestMode,
                AllowTestCleanup: payload.AllowTestCleanup,
                AllowNonReal: payload.AllowNonReal,
                Force: payload.Force,
                SourcePath: null,
                DestinationPath: null,
                VersionLabel: null,
                RetentionUntilUtc: null,
                Files: Array.Empty<DeliveryFileSummary>(),
                Domains: blocked,
                HasErrors: true,
                LastError: $"Project data_profile='{project.DataProfile}' is not eligible for delivery."
            );

            await AppendDeliveryRunAsync(db, project.ProjectId, project.Metadata, blockedResult, cancellationToken);
            return blockedResult;
        }

        if (!ProjectDeliveryGuards.TryValidateStart(project.StatusKey, payload.Force, out var statusError, out var alreadyDelivering))
        {
            var rootState = alreadyDelivering ? "blocked_already_delivering" : "blocked_status_not_ready";
            var blocked = BuildBlockedResults(rootState, statusError ?? "Project status not eligible for delivery.");

            var blockedResult = new ProjectDeliveryRunResult(
                JobId: jobId,
                ProjectId: payload.ProjectId,
                EditorInitials: payload.EditorInitials,
                StartedAtUtc: startedAt,
                TestMode: payload.TestMode,
                AllowTestCleanup: payload.AllowTestCleanup,
                AllowNonReal: payload.AllowNonReal,
                Force: payload.Force,
                SourcePath: null,
                DestinationPath: null,
                VersionLabel: null,
                RetentionUntilUtc: null,
                Files: Array.Empty<DeliveryFileSummary>(),
                Domains: blocked,
                HasErrors: true,
                LastError: statusError
            );

            await AppendDeliveryRunAsync(db, project.ProjectId, project.Metadata, blockedResult, cancellationToken);
            return blockedResult;
        }

        await UpdateProjectStatusAsync(db, project.ProjectId, ProjectDeliveryGuards.StatusDelivering, cancellationToken);

        var clientName = await db.Clients.AsNoTracking()
            .Where(c => c.ClientId == project.ClientId)
            .Select(c => c.DisplayName)
            .SingleOrDefaultAsync(cancellationToken);

        var tokens = ProvisioningTokens.Create(project.ProjectCode, project.Name, clientName, payload.EditorInitials);
        var repoRoot = FindRepoRoot();
        var deliveryTemplatePath = Path.Combine(repoRoot, "docs", "templates", "dropbox_delivery_container.json");
        var projectFolderName = await ResolveProjectFolderNameAsync(deliveryTemplatePath, tokens, cancellationToken);
        var dropboxDeliveryRelpath = await LoadPathTemplatesAsync(db, cancellationToken);

        var sourceResult = await ResolveLucidlinkSourceAsync(db, payload, tokens, cancellationToken);
        var results = new List<ProjectDeliveryDomainResult> { sourceResult.DomainResult };

        if (!string.Equals(sourceResult.DomainResult.RootState, "source_ready", StringComparison.OrdinalIgnoreCase))
        {
            var blocked = BuildBlockedResult("dropbox", "blocked_source_missing", "LucidLink source not ready.");
            results.Add(blocked);

            var blockedRun = new ProjectDeliveryRunResult(
                JobId: jobId,
                ProjectId: payload.ProjectId,
                EditorInitials: payload.EditorInitials,
                StartedAtUtc: startedAt,
                TestMode: payload.TestMode,
                AllowTestCleanup: payload.AllowTestCleanup,
                AllowNonReal: payload.AllowNonReal,
                Force: payload.Force,
                SourcePath: sourceResult.SourcePath,
                DestinationPath: null,
                VersionLabel: null,
                RetentionUntilUtc: null,
                Files: sourceResult.Files.Select(ToSummary).ToArray(),
                Domains: results,
                HasErrors: true,
                LastError: sourceResult.DomainResult.Notes.FirstOrDefault()
            );

            await AppendDeliveryRunAsync(db, project.ProjectId, project.Metadata, blockedRun, cancellationToken);
            await UpdateProjectStatusAsync(db, project.ProjectId, ProjectDeliveryGuards.StatusDeliveryFailed, cancellationToken);
            return blockedRun;
        }

        var dropboxResult = await ProcessDropboxAsync(
            payload,
            projectFolderName,
            tokens,
            sourceResult,
            dropboxDeliveryRelpath,
            deliveryTemplatePath,
            cancellationToken
        );

        results.Add(dropboxResult.DomainResult);

        var hasErrors = results.Any(IsDomainError);
        var lastError = hasErrors ? BuildLastError(results) : null;

        var runResult = new ProjectDeliveryRunResult(
            JobId: jobId,
            ProjectId: payload.ProjectId,
            EditorInitials: payload.EditorInitials,
            StartedAtUtc: startedAt,
            TestMode: payload.TestMode,
            AllowTestCleanup: payload.AllowTestCleanup,
            AllowNonReal: payload.AllowNonReal,
            Force: payload.Force,
            SourcePath: sourceResult.SourcePath,
            DestinationPath: dropboxResult.DestinationPath,
            VersionLabel: dropboxResult.VersionLabel,
            RetentionUntilUtc: dropboxResult.RetentionUntilUtc,
            Files: sourceResult.Files.Select(ToSummary).ToArray(),
            Domains: results,
            HasErrors: hasErrors,
            LastError: lastError
        );

        await AppendDeliveryRunAsync(db, project.ProjectId, project.Metadata, runResult, cancellationToken);

        var finalStatus = hasErrors ? ProjectDeliveryGuards.StatusDeliveryFailed : ProjectDeliveryGuards.StatusDelivered;
        await UpdateProjectStatusAsync(db, project.ProjectId, finalStatus, cancellationToken);

        return runResult;
    }

    public static ProjectDeliveryPayload ParsePayload(string payloadJson)
    {
        using var doc = JsonDocument.Parse(payloadJson);
        var root = doc.RootElement;

        var projectId = root.TryGetProperty("projectId", out var projectIdElement) ? projectIdElement.GetString() : null;
        if (string.IsNullOrWhiteSpace(projectId))
        {
            throw new InvalidOperationException("projectId is required in project.delivery payload.");
        }

        var editorInitials = ReadEditorInitials(root);

        return new ProjectDeliveryPayload(
            ProjectId: projectId,
            EditorInitials: editorInitials,
            TestMode: ReadBoolean(root, "testMode", false),
            AllowTestCleanup: ReadBoolean(root, "allowTestCleanup", false),
            AllowNonReal: ReadBoolean(root, "allowNonReal", false),
            Force: ReadBoolean(root, "force", false)
        );
    }

    private sealed record DeliverySourceInfo(
        string? SourcePath,
        IReadOnlyList<DeliveryFile> Files,
        ProjectDeliveryDomainResult DomainResult
    );

    private sealed record DeliveryTargetInfo(
        string? DestinationPath,
        string? VersionLabel,
        DateTimeOffset? RetentionUntilUtc,
        ProjectDeliveryDomainResult DomainResult
    );

    internal sealed record DeliveryFile(
        string SourcePath,
        string RelativePath,
        long SizeBytes,
        DateTimeOffset LastWriteTimeUtc
    );

    internal sealed record DeliveryVersionPlan(
        string VersionLabel,
        string DestinationRoot,
        bool IsNewVersion
    );

    private async Task<DeliverySourceInfo> ResolveLucidlinkSourceAsync(
        AppDbContext db,
        ProjectDeliveryPayload payload,
        ProvisioningTokens tokens,
        CancellationToken cancellationToken)
    {
        _ = tokens;
        var rootPath = ResolveDomainRootPath("lucidlink");
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            return new DeliverySourceInfo(
                SourcePath: null,
                Files: Array.Empty<DeliveryFile>(),
                DomainResult: new ProjectDeliveryDomainResult(
                    DomainKey: "lucidlink",
                    RootPath: string.Empty,
                    RootState: "skipped_unconfigured",
                    DeliveryContainerProvisioning: null,
                    Deliverables: Array.Empty<DeliveryFileSummary>(),
                    VersionLabel: null,
                    DestinationPath: null,
                    Notes: new[] { "LucidLink root not configured." }
                )
            );
        }

        if (!Directory.Exists(rootPath))
        {
            return new DeliverySourceInfo(
                SourcePath: null,
                Files: Array.Empty<DeliveryFile>(),
                DomainResult: new ProjectDeliveryDomainResult(
                    DomainKey: "lucidlink",
                    RootPath: rootPath,
                    RootState: "blocked_missing_root",
                    DeliveryContainerProvisioning: null,
                    Deliverables: Array.Empty<DeliveryFileSummary>(),
                    VersionLabel: null,
                    DestinationPath: null,
                    Notes: new[] { "LucidLink root missing." }
                )
            );
        }

        var rootKey = ProjectStorageRootHelper.GetRootKey(payload.TestMode);
        var relpath = await db.Set<Dictionary<string, object>>("project_storage_roots")
            .Where(row => EF.Property<string>(row, "project_id") == payload.ProjectId)
            .Where(row => EF.Property<string>(row, "storage_provider_key") == "lucidlink")
            .Where(row => EF.Property<string>(row, "root_key") == rootKey)
            .Select(row => EF.Property<string>(row, "folder_relpath"))
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(relpath))
        {
            return new DeliverySourceInfo(
                SourcePath: null,
                Files: Array.Empty<DeliveryFile>(),
                DomainResult: new ProjectDeliveryDomainResult(
                    DomainKey: "lucidlink",
                    RootPath: rootPath,
                    RootState: "container_missing",
                    DeliveryContainerProvisioning: null,
                    Deliverables: Array.Empty<DeliveryFileSummary>(),
                    VersionLabel: null,
                    DestinationPath: null,
                    Notes: new[] { "LucidLink storage root not found; run bootstrap first." }
                )
            );
        }

        var normalizedRelpath = relpath.Trim().TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (Path.IsPathRooted(normalizedRelpath) || normalizedRelpath.Contains("..", StringComparison.Ordinal))
        {
            return new DeliverySourceInfo(
                SourcePath: null,
                Files: Array.Empty<DeliveryFile>(),
                DomainResult: new ProjectDeliveryDomainResult(
                    DomainKey: "lucidlink",
                    RootPath: rootPath,
                    RootState: "container_missing",
                    DeliveryContainerProvisioning: null,
                    Deliverables: Array.Empty<DeliveryFileSummary>(),
                    VersionLabel: null,
                    DestinationPath: null,
                    Notes: new[] { $"LucidLink storage relpath is invalid: {relpath}" }
                )
            );
        }

        var containerPath = Path.Combine(rootPath, normalizedRelpath);
        var sourcePath = Path.Combine(containerPath, "02_Renders", "Final_Masters");

        if (!Directory.Exists(sourcePath))
        {
            return new DeliverySourceInfo(
                SourcePath: sourcePath,
                Files: Array.Empty<DeliveryFile>(),
                DomainResult: new ProjectDeliveryDomainResult(
                    DomainKey: "lucidlink",
                    RootPath: rootPath,
                    RootState: "no_source_folder",
                    DeliveryContainerProvisioning: null,
                    Deliverables: Array.Empty<DeliveryFileSummary>(),
                    VersionLabel: null,
                    DestinationPath: null,
                    Notes: new[] { "LucidLink Final_Masters folder not found." }
                )
            );
        }

        var files = FindDeliverableFiles(sourcePath);
        if (files.Count == 0)
        {
            return new DeliverySourceInfo(
                SourcePath: sourcePath,
                Files: Array.Empty<DeliveryFile>(),
                DomainResult: new ProjectDeliveryDomainResult(
                    DomainKey: "lucidlink",
                    RootPath: rootPath,
                    RootState: "no_deliverables_found",
                    DeliveryContainerProvisioning: null,
                    Deliverables: Array.Empty<DeliveryFileSummary>(),
                    VersionLabel: null,
                    DestinationPath: null,
                    Notes: new[] { "No deliverable files found in Final_Masters." }
                )
            );
        }

        return new DeliverySourceInfo(
            SourcePath: sourcePath,
            Files: files,
            DomainResult: new ProjectDeliveryDomainResult(
                DomainKey: "lucidlink",
                RootPath: rootPath,
                RootState: "source_ready",
                DeliveryContainerProvisioning: null,
                Deliverables: files.Select(ToSummary).ToArray(),
                VersionLabel: null,
                DestinationPath: null,
                Notes: Array.Empty<string>()
            )
        );
    }

    private async Task<DeliveryTargetInfo> ProcessDropboxAsync(
        ProjectDeliveryPayload payload,
        string projectFolderName,
        ProvisioningTokens tokens,
        DeliverySourceInfo source,
        string dropboxDeliveryRelpath,
        string deliveryTemplatePath,
        CancellationToken cancellationToken)
    {
        var notes = new List<string>();
        var rootPath = ResolveDomainRootPath("dropbox");
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            return new DeliveryTargetInfo(
                DestinationPath: null,
                VersionLabel: null,
                RetentionUntilUtc: null,
                DomainResult: new ProjectDeliveryDomainResult(
                    DomainKey: "dropbox",
                    RootPath: string.Empty,
                    RootState: "skipped_unconfigured",
                    DeliveryContainerProvisioning: null,
                    Deliverables: source.Files.Select(ToSummary).ToArray(),
                    VersionLabel: null,
                    DestinationPath: null,
                    Notes: new[] { "Dropbox root not configured." }
                )
            );
        }

        if (!Directory.Exists(rootPath))
        {
            return new DeliveryTargetInfo(
                DestinationPath: null,
                VersionLabel: null,
                RetentionUntilUtc: null,
                DomainResult: new ProjectDeliveryDomainResult(
                    DomainKey: "dropbox",
                    RootPath: rootPath,
                    RootState: "blocked_missing_root",
                    DeliveryContainerProvisioning: null,
                    Deliverables: source.Files.Select(ToSummary).ToArray(),
                    VersionLabel: null,
                    DestinationPath: null,
                    Notes: new[] { "Dropbox root missing." }
                )
            );
        }

        var clientFolder = tokens.ClientName ?? "Client";
        PathSafety.EnsureSafeSegment(clientFolder, "client folder name");

        var baseRoot = payload.TestMode
            ? Path.Combine(rootPath, "99_TestRuns")
            : rootPath;

        var basePath = Path.Combine(baseRoot, dropboxDeliveryRelpath, clientFolder);
        var targetPath = Path.Combine(basePath, projectFolderName);

        if (payload.TestMode && payload.AllowTestCleanup && Directory.Exists(targetPath))
        {
            if (!ProjectBootstrapGuards.TryValidateTestCleanup(rootPath, targetPath, payload.AllowTestCleanup, out var cleanupError))
            {
                notes.Add(cleanupError ?? "Test cleanup blocked.");
                return new DeliveryTargetInfo(
                    DestinationPath: targetPath,
                    VersionLabel: null,
                    RetentionUntilUtc: null,
                    DomainResult: new ProjectDeliveryDomainResult(
                        DomainKey: "dropbox",
                        RootPath: rootPath,
                        RootState: "blocked_test_cleanup",
                        DeliveryContainerProvisioning: null,
                        Deliverables: source.Files.Select(ToSummary).ToArray(),
                        VersionLabel: null,
                        DestinationPath: targetPath,
                        Notes: notes
                    )
                );
            }

            var cleanup = await ProjectBootstrapCleanupHelper.TryDeleteWithRetryAsync(targetPath, cancellationToken);
            if (!cleanup.Success)
            {
                notes.Add(cleanup.Error ?? "Test cleanup failed (locked; cleanup skipped).");
                return new DeliveryTargetInfo(
                    DestinationPath: targetPath,
                    VersionLabel: null,
                    RetentionUntilUtc: null,
                    DomainResult: new ProjectDeliveryDomainResult(
                        DomainKey: "dropbox",
                        RootPath: rootPath,
                        RootState: "cleanup_locked",
                        DeliveryContainerProvisioning: null,
                        Deliverables: source.Files.Select(ToSummary).ToArray(),
                        VersionLabel: null,
                        DestinationPath: targetPath,
                        Notes: notes
                    )
                );
            }
        }

        var applySummary = await ExecuteTemplateAsync(deliveryTemplatePath, basePath, tokens, ProvisioningMode.Apply, cancellationToken);
        var verifySummary = await ExecuteTemplateAsync(deliveryTemplatePath, basePath, tokens, ProvisioningMode.Verify, cancellationToken);

        if (!verifySummary.Success)
        {
            notes.Add("Delivery container verify failed.");
            return new DeliveryTargetInfo(
                DestinationPath: targetPath,
                VersionLabel: null,
                RetentionUntilUtc: null,
                DomainResult: new ProjectDeliveryDomainResult(
                    DomainKey: "dropbox",
                    RootPath: rootPath,
                    RootState: "container_verify_failed",
                    DeliveryContainerProvisioning: verifySummary,
                    Deliverables: source.Files.Select(ToSummary).ToArray(),
                    VersionLabel: null,
                    DestinationPath: targetPath,
                    Notes: notes
                )
            );
        }

        var finalRoot = Path.Combine(targetPath, "01_Deliverables", "Final");
        var versionPlan = DetermineVersion(finalRoot, source.Files);

        if (versionPlan.IsNewVersion)
        {
            Directory.CreateDirectory(versionPlan.DestinationRoot);
            foreach (var file in source.Files)
            {
                var destPath = Path.Combine(versionPlan.DestinationRoot, file.RelativePath);
                var destDir = Path.GetDirectoryName(destPath);
                if (!string.IsNullOrWhiteSpace(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }

                if (!File.Exists(destPath))
                {
                    File.Copy(file.SourcePath, destPath, overwrite: false);
                }
            }
        }

        var retentionUntil = DateTimeOffset.UtcNow.AddMonths(DefaultRetentionMonths);
        var manifestPath = ResolveDeliveryManifestPath(targetPath);
        var manifest = BuildDeliveryManifest(
            payload.ProjectId,
            tokens,
            source.SourcePath ?? string.Empty,
            versionPlan.DestinationRoot,
            versionPlan.VersionLabel,
            retentionUntil,
            source.Files
        );

        await WriteDeliveryManifestAsync(manifestPath, manifest, cancellationToken);

        return new DeliveryTargetInfo(
            DestinationPath: versionPlan.DestinationRoot,
            VersionLabel: versionPlan.VersionLabel,
            RetentionUntilUtc: retentionUntil,
            DomainResult: new ProjectDeliveryDomainResult(
                DomainKey: "dropbox",
                RootPath: rootPath,
                RootState: versionPlan.IsNewVersion ? "delivered" : "delivered_noop",
                DeliveryContainerProvisioning: verifySummary,
                Deliverables: source.Files.Select(ToSummary).ToArray(),
                VersionLabel: versionPlan.VersionLabel,
                DestinationPath: versionPlan.DestinationRoot,
                Notes: notes
            )
        );
    }

    private async Task<ProvisioningSummary> ExecuteTemplateAsync(
        string templatePath,
        string basePath,
        ProvisioningTokens tokens,
        ProvisioningMode mode,
        CancellationToken cancellationToken)
    {
        var request = new ProvisioningRequest(
            Mode: mode,
            TemplatePath: templatePath,
            BasePath: basePath,
            SchemaPath: null,
            SeedsPath: null,
            Tokens: tokens,
            ForceOverwriteSeededFiles: false
        );

        var result = await provisioner.ExecuteAsync(request, cancellationToken);

        return new ProvisioningSummary(
            Mode: result.Mode.ToString().ToLowerInvariant(),
            TemplateKey: result.TemplateKey,
            TargetRoot: result.TargetRoot,
            ManifestPath: result.ManifestPath,
            Success: result.Success,
            MissingRequired: result.MissingRequired.ToArray(),
            Errors: result.Errors.ToArray(),
            Warnings: result.Warnings.ToArray()
        );
    }

    private static async Task<string> ResolveProjectFolderNameAsync(
        string templatePath,
        ProvisioningTokens tokens,
        CancellationToken cancellationToken)
    {
        var loader = new FolderTemplateLoader();
        var loaded = await loader.LoadAsync(templatePath, schemaPathOverride: null, cancellationToken);
        var expanded = TokenExpander.ExpandRootName(loaded.Template.Root.Name, tokens);
        PathSafety.EnsureSafeSegment(expanded, "project folder name");
        return expanded;
    }

    private static IReadOnlyList<DeliveryFile> FindDeliverableFiles(string sourcePath)
    {
        var list = new List<DeliveryFile>();
        foreach (var filePath in Directory.EnumerateFiles(sourcePath, "*", SearchOption.AllDirectories))
        {
            var fileName = Path.GetFileName(filePath);
            if (IsIgnoredFileName(fileName))
            {
                continue;
            }

            var extension = Path.GetExtension(fileName);
            if (!AllowedExtensions.Contains(extension))
            {
                continue;
            }

            var info = new FileInfo(filePath);
            var relative = Path.GetRelativePath(sourcePath, filePath);
            list.Add(new DeliveryFile(filePath, relative, info.Length, info.LastWriteTimeUtc));
        }

        return list;
    }

    private static bool IsIgnoredFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return true;
        }

        if (fileName.StartsWith("._", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(fileName, ".ds_store", StringComparison.OrdinalIgnoreCase)
               || fileName.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase);
    }

    internal static DeliveryVersionPlan DetermineVersion(string finalRoot, IReadOnlyList<DeliveryFile> sourceFiles)
    {
        Directory.CreateDirectory(finalRoot);

        var sourceSet = BuildFileSet(sourceFiles);
        var versionDirs = Directory.GetDirectories(finalRoot)
            .Where(dir => IsVersionFolderName(Path.GetFileName(dir)))
            .ToArray();

        var versionNumbers = versionDirs.Select(dir => ParseVersionNumber(Path.GetFileName(dir))).Where(n => n > 0).ToArray();

        var baseSet = BuildFileSet(finalRoot, versionDirs);
        if (sourceSet.Count > 0 && baseSet.Count > 0 && SetsMatch(sourceSet, baseSet))
        {
            return new DeliveryVersionPlan("v1", finalRoot, false);
        }

        foreach (var versionDir in versionDirs.OrderByDescending(dir => ParseVersionNumber(Path.GetFileName(dir))))
        {
            var set = BuildFileSet(versionDir, Array.Empty<string>());
            if (sourceSet.Count > 0 && set.Count > 0 && SetsMatch(sourceSet, set))
            {
                var label = Path.GetFileName(versionDir);
                return new DeliveryVersionPlan(label, versionDir, false);
            }
        }

        var baseHasFiles = baseSet.Count > 0;
        var nextVersion = versionNumbers.Length > 0
            ? versionNumbers.Max() + 1
            : baseHasFiles
                ? 2
                : 1;

        if (nextVersion <= 1)
        {
            return new DeliveryVersionPlan("v1", finalRoot, true);
        }

        var versionLabel = $"v{nextVersion}";
        var destination = Path.Combine(finalRoot, versionLabel);
        return new DeliveryVersionPlan(versionLabel, destination, true);
    }

    private static Dictionary<string, DeliveryFingerprint> BuildFileSet(
        IReadOnlyList<DeliveryFile> files)
    {
        var map = new Dictionary<string, DeliveryFingerprint>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            map[file.RelativePath] = new DeliveryFingerprint(file.SizeBytes, file.LastWriteTimeUtc);
        }

        return map;
    }

    private static Dictionary<string, DeliveryFingerprint> BuildFileSet(
        string rootPath,
        IReadOnlyList<string> excludeDirs)
    {
        var exclusions = excludeDirs
            .Select(NormalizePath)
            .ToArray();

        var map = new Dictionary<string, DeliveryFingerprint>(StringComparer.OrdinalIgnoreCase);
        foreach (var filePath in Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories))
        {
            if (IsUnderAny(filePath, exclusions))
            {
                continue;
            }

            var name = Path.GetFileName(filePath);
            if (IsIgnoredFileName(name))
            {
                continue;
            }

            var extension = Path.GetExtension(name);
            if (!AllowedExtensions.Contains(extension))
            {
                continue;
            }

            var info = new FileInfo(filePath);
            var relative = Path.GetRelativePath(rootPath, filePath);
            map[relative] = new DeliveryFingerprint(info.Length, info.LastWriteTimeUtc);
        }

        return map;
    }

    private static bool IsUnderAny(string filePath, IReadOnlyList<string> prefixes)
    {
        var normalized = NormalizePath(filePath);
        foreach (var prefix in prefixes)
        {
            if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool SetsMatch(
        IReadOnlyDictionary<string, DeliveryFingerprint> left,
        IReadOnlyDictionary<string, DeliveryFingerprint> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        foreach (var pair in left)
        {
            if (!right.TryGetValue(pair.Key, out var rightValue))
            {
                return false;
            }

            if (pair.Value.SizeBytes != rightValue.SizeBytes)
            {
                return false;
            }

            if (pair.Value.LastWriteTimeUtc != rightValue.LastWriteTimeUtc)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsVersionFolderName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        return name.Length > 1
               && (name[0] == 'v' || name[0] == 'V')
               && int.TryParse(name[1..], out _);
    }

    private static int ParseVersionNumber(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return 0;
        }

        if (name.Length <= 1 || (name[0] != 'v' && name[0] != 'V'))
        {
            return 0;
        }

        return int.TryParse(name[1..], out var number) ? number : 0;
    }

    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    internal static string ResolveDeliveryManifestPath(string containerRoot)
    {
        var manifestDir = Path.Combine(containerRoot, "00_Admin", ".mgf", "manifest");
        return Path.Combine(manifestDir, "delivery_manifest.json");
    }

    private static async Task WriteDeliveryManifestAsync(
        string manifestPath,
        DeliveryManifest manifest,
        CancellationToken cancellationToken)
    {
        var manifestDir = Path.GetDirectoryName(manifestPath);
        if (!string.IsNullOrWhiteSpace(manifestDir))
        {
            Directory.CreateDirectory(manifestDir);
        }

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        var json = JsonSerializer.Serialize(manifest, options);
        await File.WriteAllTextAsync(manifestPath, json, cancellationToken);
    }

    private static DeliveryManifest BuildDeliveryManifest(
        string projectId,
        ProvisioningTokens tokens,
        string sourcePath,
        string destinationPath,
        string versionLabel,
        DateTimeOffset retentionUntilUtc,
        IReadOnlyList<DeliveryFile> files)
    {
        return new DeliveryManifest
        {
            SchemaVersion = 1,
            DeliveryRunId = Guid.NewGuid().ToString("N"),
            CreatedAtUtc = DateTimeOffset.UtcNow,
            ProjectId = projectId,
            ProjectCode = tokens.ProjectCode ?? string.Empty,
            ProjectName = tokens.ProjectName ?? string.Empty,
            ClientName = tokens.ClientName ?? string.Empty,
            SourcePath = sourcePath,
            DestinationPath = destinationPath,
            VersionLabel = versionLabel,
            RetentionUntilUtc = retentionUntilUtc,
            Files = files.Select(ToSummary).ToArray()
        };
    }

    private sealed record DeliveryFingerprint(long SizeBytes, DateTimeOffset LastWriteTimeUtc);

    private sealed record DeliveryManifest
    {
        public int SchemaVersion { get; init; }
        public string DeliveryRunId { get; init; } = string.Empty;
        public DateTimeOffset CreatedAtUtc { get; init; }
        public string ProjectId { get; init; } = string.Empty;
        public string ProjectCode { get; init; } = string.Empty;
        public string ProjectName { get; init; } = string.Empty;
        public string ClientName { get; init; } = string.Empty;
        public string SourcePath { get; init; } = string.Empty;
        public string DestinationPath { get; init; } = string.Empty;
        public string VersionLabel { get; init; } = string.Empty;
        public DateTimeOffset RetentionUntilUtc { get; init; }
        public IReadOnlyList<DeliveryFileSummary> Files { get; init; } = Array.Empty<DeliveryFileSummary>();
    }

    private static DeliveryFileSummary ToSummary(DeliveryFile file)
        => new(file.RelativePath, file.SizeBytes, file.LastWriteTimeUtc);

    private static DeliveryFileSummary ToSummary(DeliveryFileSummary file)
        => file;

    private static async Task<string> LoadPathTemplatesAsync(
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var templateRow = await db.Set<Dictionary<string, object>>("path_templates")
            .Where(row => EF.Property<string>(row, "path_key") == "dropbox_delivery_root")
            .Select(row => EF.Property<string>(row, "relpath"))
            .FirstOrDefaultAsync(cancellationToken);

        return string.IsNullOrWhiteSpace(templateRow) ? "04_Client_Deliveries" : templateRow.Trim();
    }

    private static List<ProjectDeliveryDomainResult> BuildBlockedResults(string rootState, string note)
    {
        return new List<ProjectDeliveryDomainResult>
        {
            BuildBlockedResult("lucidlink", rootState, note),
            BuildBlockedResult("dropbox", rootState, note)
        };
    }

    private static ProjectDeliveryDomainResult BuildBlockedResult(string domainKey, string rootState, string note)
    {
        return new ProjectDeliveryDomainResult(
            DomainKey: domainKey,
            RootPath: string.Empty,
            RootState: rootState,
            DeliveryContainerProvisioning: null,
            Deliverables: Array.Empty<DeliveryFileSummary>(),
            VersionLabel: null,
            DestinationPath: null,
            Notes: new[] { note }
        );
    }

    private static string? BuildLastError(IReadOnlyList<ProjectDeliveryDomainResult> results)
    {
        foreach (var result in results)
        {
            if (result.Notes.Count > 0 && IsDomainError(result))
            {
                return result.Notes[0];
            }
        }

        return "project.delivery completed with errors.";
    }

    private static bool IsDomainError(ProjectDeliveryDomainResult result)
    {
        if (result.RootState.StartsWith("blocked_", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (result.RootState.EndsWith("_failed", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (result.RootState.StartsWith("cleanup_", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return result.RootState is "container_missing" or "no_source_folder" or "no_deliverables_found";
    }

    private static async Task AppendDeliveryRunAsync(
        AppDbContext db,
        string projectId,
        JsonElement metadata,
        ProjectDeliveryRunResult runResult,
        CancellationToken cancellationToken)
    {
        var root = JsonNode.Parse(metadata.GetRawText()) as JsonObject ?? new JsonObject();
        var delivery = root["delivery"] as JsonObject ?? new JsonObject();
        var runs = delivery["runs"] as JsonArray ?? new JsonArray();

        var runNode = JsonSerializer.SerializeToNode(runResult, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        if (runNode is not null)
        {
            runs.Add(runNode);
        }

        while (runs.Count > MaxRunsToKeep)
        {
            runs.RemoveAt(0);
        }

        delivery["runs"] = runs;
        root["delivery"] = delivery;

        var updatedJson = root.ToJsonString(new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE public.projects
            SET metadata = {updatedJson}::jsonb,
                updated_at = now()
            WHERE project_id = {projectId};
            """,
            cancellationToken
        );
    }

    private static async Task UpdateProjectStatusAsync(
        AppDbContext db,
        string projectId,
        string statusKey,
        CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE public.projects
            SET status_key = {statusKey},
                updated_at = now()
            WHERE project_id = {projectId};
            """,
            cancellationToken
        );
    }

    private string ResolveDomainRootPath(string domainKey)
    {
        var raw = domainKey switch
        {
            "dropbox" => configuration["Storage:DropboxRoot"] ?? string.Empty,
            "lucidlink" => configuration["Storage:LucidLinkRoot"] ?? string.Empty,
            "nas" => configuration["Storage:NasRoot"] ?? string.Empty,
            _ => string.Empty
        };

        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        return Path.GetFullPath(raw.Trim());
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

    private static string FindRepoRoot()
    {
        var startPaths = new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory };
        foreach (var start in startPaths)
        {
            var current = new DirectoryInfo(start);
            while (current is not null)
            {
                if (File.Exists(Path.Combine(current.FullName, "MGF.sln")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }
        }

        throw new InvalidOperationException($"Could not locate repo root (MGF.sln) from {Directory.GetCurrentDirectory()}.");
    }
}
