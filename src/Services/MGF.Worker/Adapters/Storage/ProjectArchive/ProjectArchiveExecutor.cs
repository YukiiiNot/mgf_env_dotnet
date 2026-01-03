namespace MGF.Worker.Adapters.Storage.ProjectArchive;

using Microsoft.Extensions.Configuration;
using MGF.Contracts.Abstractions.ProjectArchive;
using MGF.FolderProvisioning;
using MGF.Worker.Adapters.Storage.ProjectBootstrap;

public sealed class ProjectArchiveExecutor : IProjectArchiveExecutor
{
    private readonly IConfiguration configuration;
    private readonly FolderTemplateLoader templateLoader = new();
    private readonly FolderTemplatePlanner planner = new();
    private readonly FolderPlanExecutor executor;
    private readonly LocalFileStore fileStore = new();

    public ProjectArchiveExecutor(IConfiguration configuration)
    {
        this.configuration = configuration;
        executor = new FolderPlanExecutor(fileStore);
    }

    public Task<string> ResolveProjectFolderNameAsync(
        ProjectArchiveTokens tokens,
        CancellationToken cancellationToken = default)
    {
        var provisioningTokens = ToProvisioningTokens(tokens);
        var templatesRoot = ResolveTemplatesRoot();
        var templatePath = Path.Combine(templatesRoot, "dropbox_project_container.json");
        return ResolveProjectFolderNameAsync(templatePath, provisioningTokens, cancellationToken);
    }

    private static ProvisioningTokens ToProvisioningTokens(ProjectArchiveTokens tokens)
    {
        return ProvisioningTokens.Create(
            tokens.ProjectCode,
            tokens.ProjectName,
            tokens.ClientName,
            tokens.EditorInitials);
    }
    public async Task<ProjectArchiveDomainResult> ProcessDropboxAsync(
        ProjectArchivePayload payload,
        string projectFolderName,
        ProjectArchivePathTemplates pathTemplates,
        CancellationToken cancellationToken)
    {
        var notes = new List<string>();
        var actions = new List<ArchiveActionSummary>();

        var rootPath = ResolveDomainRootPath("dropbox");
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            return new ProjectArchiveDomainResult(
                DomainKey: "dropbox",
                RootPath: string.Empty,
                RootState: "skipped_unconfigured",
                DomainRootProvisioning: null,
                TargetProvisioning: null,
                Actions: actions,
                Notes: new[] { "Dropbox root not configured." }
            );
        }

        if (!Directory.Exists(rootPath))
        {
            return new ProjectArchiveDomainResult(
                DomainKey: "dropbox",
                RootPath: rootPath,
                RootState: "blocked_missing_root",
                DomainRootProvisioning: null,
                TargetProvisioning: null,
                Actions: actions,
                Notes: new[] { "Dropbox root missing." }
            );
        }

        var baseRoot = payload.TestMode ? Path.Combine(rootPath, "99_TestRuns") : rootPath;
        var activeRelpath = pathTemplates.DropboxActiveRelpath;
        var toArchiveRelpath = pathTemplates.DropboxToArchiveRelpath;
        var archiveRelpath = pathTemplates.DropboxArchiveRelpath;

        var activePath = Path.Combine(baseRoot, activeRelpath, projectFolderName);
        var toArchivePath = Path.Combine(baseRoot, toArchiveRelpath, projectFolderName);
        var archivePath = Path.Combine(baseRoot, archiveRelpath, projectFolderName);

        string? flatTestActive = null;
        if (payload.TestMode)
        {
            flatTestActive = Path.Combine(baseRoot, projectFolderName);
            if (!Directory.Exists(activePath) && Directory.Exists(flatTestActive))
            {
                activePath = flatTestActive;
            }

            var cleanupTargets = new[] { toArchivePath, archivePath, flatTestActive }
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Where(path => !string.Equals(path, activePath, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(Directory.Exists)
                .ToArray();

            if (cleanupTargets.Length > 0)
            {
                foreach (var target in cleanupTargets)
                {
                    if (!ProjectBootstrapGuards.TryValidateTestCleanup(rootPath, target, payload.AllowTestCleanup, out var cleanupError))
                    {
                        notes.Add(cleanupError ?? "Test cleanup blocked.");
                        return new ProjectArchiveDomainResult(
                            DomainKey: "dropbox",
                            RootPath: rootPath,
                            RootState: "blocked_test_cleanup",
                            DomainRootProvisioning: null,
                            TargetProvisioning: null,
                            Actions: actions,
                            Notes: notes
                        );
                    }

                    var cleanup = await ProjectBootstrapCleanupHelper.TryDeleteWithRetryAsync(target, cancellationToken);
                    if (!cleanup.Success)
                    {
                        notes.Add(cleanup.Error ?? "Test cleanup failed (locked; cleanup skipped).");
                        return new ProjectArchiveDomainResult(
                            DomainKey: "dropbox",
                            RootPath: rootPath,
                            RootState: "cleanup_locked",
                            DomainRootProvisioning: null,
                            TargetProvisioning: null,
                            Actions: actions,
                            Notes: notes
                        );
                    }
                }
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(toArchivePath) ?? baseRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(archivePath) ?? baseRoot);

        var plan = BuildDropboxMovePlan(
            hasActive: Directory.Exists(activePath),
            hasToArchive: Directory.Exists(toArchivePath),
            hasArchive: Directory.Exists(archivePath)
        );

        if (!plan.ShouldMoveToArchive)
        {
            var note = plan.RootState == "container_missing" ? "Dropbox active container not found." : null;
            if (!string.IsNullOrWhiteSpace(note))
            {
                notes.Add(note);
            }

            return new ProjectArchiveDomainResult(
                DomainKey: "dropbox",
                RootPath: rootPath,
                RootState: plan.RootState,
                DomainRootProvisioning: null,
                TargetProvisioning: null,
                Actions: actions,
                Notes: notes
            );
        }

        try
        {
            Directory.Move(activePath, toArchivePath);
            actions.Add(new ArchiveActionSummary(
                Action: "move_to_archive_staging",
                SourcePath: activePath,
                DestinationPath: toArchivePath,
                Success: true,
                Error: null
            ));
            return new ProjectArchiveDomainResult(
                DomainKey: "dropbox",
                RootPath: rootPath,
                RootState: "ready_to_archive",
                DomainRootProvisioning: null,
                TargetProvisioning: null,
                Actions: actions,
                Notes: notes
            );
        }
        catch (Exception ex)
        {
            actions.Add(new ArchiveActionSummary(
                Action: "move_to_archive_staging",
                SourcePath: activePath,
                DestinationPath: toArchivePath,
                Success: false,
                Error: ex.Message
            ));
            return new ProjectArchiveDomainResult(
                DomainKey: "dropbox",
                RootPath: rootPath,
                RootState: "move_failed",
                DomainRootProvisioning: null,
                TargetProvisioning: null,
                Actions: actions,
                Notes: new[] { ex.Message }
            );
        }
    }

    public async Task<ProjectArchiveDomainResult> FinalizeDropboxArchiveAsync(
        ProjectArchiveDomainResult dropboxResult,
        ProjectArchivePayload payload,
        string projectFolderName,
        ProjectArchivePathTemplates pathTemplates,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var actions = dropboxResult.Actions.ToList();
        var notes = dropboxResult.Notes.ToList();

        var rootPath = ResolveDomainRootPath("dropbox");
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            return dropboxResult;
        }

        var baseRoot = payload.TestMode ? Path.Combine(rootPath, "99_TestRuns") : rootPath;
        var toArchiveRelpath = pathTemplates.DropboxToArchiveRelpath;
        var archiveRelpath = pathTemplates.DropboxArchiveRelpath;
        var toArchivePath = Path.Combine(baseRoot, toArchiveRelpath, projectFolderName);
        var archivePath = Path.Combine(baseRoot, archiveRelpath, projectFolderName);

        if (Directory.Exists(archivePath))
        {
            return dropboxResult with { RootState = "already_archived" };
        }

        if (!Directory.Exists(toArchivePath))
        {
            notes.Add("Dropbox staging folder not found when finalizing archive.");
            return dropboxResult with { RootState = "archive_move_missing", Notes = notes };
        }

        try
        {
            Directory.Move(toArchivePath, archivePath);
            actions.Add(new ArchiveActionSummary(
                Action: "move_to_archive",
                SourcePath: toArchivePath,
                DestinationPath: archivePath,
                Success: true,
                Error: null
            ));
            return dropboxResult with { RootState = "archived", Actions = actions, Notes = notes };
        }
        catch (Exception ex)
        {
            actions.Add(new ArchiveActionSummary(
                Action: "move_to_archive",
                SourcePath: toArchivePath,
                DestinationPath: archivePath,
                Success: false,
                Error: ex.Message
            ));
            notes.Add(ex.Message);
            return dropboxResult with { RootState = "archive_move_failed", Actions = actions, Notes = notes };
        }
    }
    public Task<ProjectArchiveDomainResult> ProcessLucidlinkAsync(
        ProjectArchivePayload payload,
        string projectFolderName)
    {
        var rootPath = ResolveDomainRootPath("lucidlink");
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            return Task.FromResult(new ProjectArchiveDomainResult(
                DomainKey: "lucidlink",
                RootPath: string.Empty,
                RootState: "skipped_unconfigured",
                DomainRootProvisioning: null,
                TargetProvisioning: null,
                Actions: Array.Empty<ArchiveActionSummary>(),
                Notes: new[] { "LucidLink root not configured." }
            ));
        }

        var baseRoot = payload.TestMode
            ? Path.Combine(rootPath, "99_TestRuns")
            : Path.Combine(rootPath, "01_Productions_Active");
        var sourcePath = Path.Combine(baseRoot, projectFolderName);

        if (!Directory.Exists(sourcePath))
        {
            return Task.FromResult(new ProjectArchiveDomainResult(
                DomainKey: "lucidlink",
                RootPath: rootPath,
                RootState: "source_missing",
                DomainRootProvisioning: null,
                TargetProvisioning: null,
                Actions: Array.Empty<ArchiveActionSummary>(),
                Notes: new[] { "LucidLink production container not found." }
            ));
        }

        return Task.FromResult(new ProjectArchiveDomainResult(
            DomainKey: "lucidlink",
            RootPath: rootPath,
            RootState: "source_found",
            DomainRootProvisioning: null,
            TargetProvisioning: null,
            Actions: Array.Empty<ArchiveActionSummary>(),
            Notes: Array.Empty<string>()
        ));
    }

    public async Task<ProjectArchiveDomainResult> ProcessNasAsync(
        ProjectArchivePayload payload,
        string projectFolderName,
        ProjectArchiveDomainResult lucidlinkResult,
        ProjectArchiveTokens tokens,
        ProjectArchivePathTemplates pathTemplates,
        CancellationToken cancellationToken)
    {
        var notes = new List<string>();
        var actions = new List<ArchiveActionSummary>();
        var provisioningTokens = ToProvisioningTokens(tokens);
        var templatesRoot = ResolveTemplatesRoot();

        var rootPath = ResolveDomainRootPath("nas");
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            return new ProjectArchiveDomainResult(
                DomainKey: "nas",
                RootPath: string.Empty,
                RootState: "skipped_unconfigured",
                DomainRootProvisioning: null,
                TargetProvisioning: null,
                Actions: actions,
                Notes: new[] { "NAS root not configured." }
            );
        }

        if (!Directory.Exists(rootPath))
        {
            return new ProjectArchiveDomainResult(
                DomainKey: "nas",
                RootPath: rootPath,
                RootState: "blocked_missing_root",
                DomainRootProvisioning: null,
                TargetProvisioning: null,
                Actions: actions,
                Notes: new[] { "NAS root missing." }
            );
        }

        var baseRoot = payload.TestMode
            ? Path.Combine(rootPath, "99_TestRuns")
            : Path.Combine(rootPath, pathTemplates.NasArchiveRelpath);
        var targetPath = Path.Combine(baseRoot, projectFolderName);

        if (payload.TestMode && Directory.Exists(targetPath))
        {
            if (!ProjectBootstrapGuards.TryValidateTestCleanup(rootPath, targetPath, payload.AllowTestCleanup, out var cleanupError))
            {
                notes.Add(cleanupError ?? "Test cleanup blocked.");
                return new ProjectArchiveDomainResult(
                    DomainKey: "nas",
                    RootPath: rootPath,
                    RootState: "blocked_test_cleanup",
                    DomainRootProvisioning: null,
                    TargetProvisioning: null,
                    Actions: actions,
                    Notes: notes
                );
            }

            var cleanup = await ProjectBootstrapCleanupHelper.TryDeleteWithRetryAsync(targetPath, cancellationToken);
            if (!cleanup.Success)
            {
                notes.Add(cleanup.Error ?? "Test cleanup failed (locked; cleanup skipped).");
                return new ProjectArchiveDomainResult(
                    DomainKey: "nas",
                    RootPath: rootPath,
                    RootState: "cleanup_locked",
                    DomainRootProvisioning: null,
                    TargetProvisioning: null,
                    Actions: actions,
                    Notes: notes
                );
            }
        }

        ProvisioningSummary? targetProvisioning = null;
        ProvisioningResult? verified = null;

        try
        {
            _ = await ExecuteTemplateAsync(
                templatePath: Path.Combine(templatesRoot, "nas_archive_container.json"),
                basePath: baseRoot,
                tokens: provisioningTokens,
                mode: ProvisioningMode.Apply,
                forceOverwrite: false,
                rootNameOverride: null,
                cancellationToken: cancellationToken
            );

            verified = await ExecuteTemplateAsync(
                templatePath: Path.Combine(templatesRoot, "nas_archive_container.json"),
                basePath: baseRoot,
                tokens: provisioningTokens,
                mode: ProvisioningMode.Verify,
                forceOverwrite: false,
                rootNameOverride: null,
                cancellationToken: cancellationToken
            );

            targetProvisioning = ToSummary(verified);
        }
        catch (Exception ex)
        {
            notes.Add(ex.Message);
            return new ProjectArchiveDomainResult(
                DomainKey: "nas",
                RootPath: rootPath,
                RootState: "archive_apply_failed",
                DomainRootProvisioning: null,
                TargetProvisioning: targetProvisioning,
                Actions: actions,
                Notes: notes
            );
        }

        if (verified is null || !verified.Success)
        {
            notes.Add("NAS archive template verify failed.");
            return new ProjectArchiveDomainResult(
                DomainKey: "nas",
                RootPath: rootPath,
                RootState: "archive_verify_failed",
                DomainRootProvisioning: null,
                TargetProvisioning: targetProvisioning,
                Actions: actions,
                Notes: notes
            );
        }

        if (!string.Equals(lucidlinkResult.RootState, "source_found", StringComparison.OrdinalIgnoreCase))
        {
            notes.Add("LucidLink source missing; skip copy.");
            return new ProjectArchiveDomainResult(
                DomainKey: "nas",
                RootPath: rootPath,
                RootState: "source_missing",
                DomainRootProvisioning: null,
                TargetProvisioning: targetProvisioning,
                Actions: actions,
                Notes: notes
            );
        }

        var lucidlinkRoot = ResolveDomainRootPath("lucidlink");
        var lucidlinkBase = payload.TestMode
            ? Path.Combine(lucidlinkRoot, "99_TestRuns")
            : Path.Combine(lucidlinkRoot, "01_Productions_Active");
        var sourcePath = Path.Combine(lucidlinkBase, projectFolderName);

        var snapshotPath = Path.Combine(targetPath, "03_ProjectFiles_Snapshots", "LucidLink");

        try
        {
            await CopyDirectoryAsync(sourcePath, snapshotPath, cancellationToken);
            actions.Add(new ArchiveActionSummary(
                Action: "copy_lucidlink_to_nas",
                SourcePath: sourcePath,
                DestinationPath: snapshotPath,
                Success: true,
                Error: null
            ));
        }
        catch (Exception ex)
        {
            actions.Add(new ArchiveActionSummary(
                Action: "copy_lucidlink_to_nas",
                SourcePath: sourcePath,
                DestinationPath: snapshotPath,
                Success: false,
                Error: ex.Message
            ));
            notes.Add(ex.Message);
            return new ProjectArchiveDomainResult(
                DomainKey: "nas",
                RootPath: rootPath,
                RootState: "copy_failed",
                DomainRootProvisioning: null,
                TargetProvisioning: targetProvisioning,
                Actions: actions,
                Notes: notes
            );
        }

        return new ProjectArchiveDomainResult(
            DomainKey: "nas",
            RootPath: rootPath,
            RootState: "archive_verified",
            DomainRootProvisioning: null,
            TargetProvisioning: targetProvisioning,
            Actions: actions,
            Notes: notes
        );
    }

    private async Task<ProvisioningResult> ExecuteTemplateAsync(
        string templatePath,
        string basePath,
        ProvisioningTokens tokens,
        ProvisioningMode mode,
        bool forceOverwrite,
        string? rootNameOverride,
        CancellationToken cancellationToken)
    {
        var loaded = await templateLoader.LoadAsync(templatePath, schemaPathOverride: null, cancellationToken);

        if (!string.IsNullOrWhiteSpace(rootNameOverride))
        {
            loaded.Template.Root.Name = rootNameOverride;
        }

        if (tokens.EditorInitials.Count == 0)
        {
            EnsureEditorTokenNotInRoot(loaded.Template.Root);
            MarkEditorNodesOptional(loaded.Template.Root);
        }

        var templateHash = Hashing.Sha256Hex(loaded.TemplateBytes);
        var plan = planner.Plan(loaded.Template, tokens, basePath);
        var seedsPath = ResolveSeedsPath(null, loaded.TemplatePath);

        ExecutionResult executionResult = mode switch
        {
            ProvisioningMode.Plan => new ExecutionResult(),
            ProvisioningMode.Verify => await executor.VerifyAsync(plan, cancellationToken),
            ProvisioningMode.Apply => await executor.ApplyAsync(plan, seedsPath, tokens, allowSeedOverwrite: false, cancellationToken),
            ProvisioningMode.Repair => await executor.ApplyAsync(plan, seedsPath, tokens, forceOverwrite, cancellationToken),
            _ => throw new InvalidOperationException($"Unknown mode {mode}")
        };

        var manifestPath = ResolveManifestPath(plan);
        var manifest = BuildManifest(loaded.Template.TemplateKey, templateHash, mode, tokens, plan, executionResult);

        await ProvisioningManifestWriter.WriteAsync(fileStore, manifest, manifestPath, cancellationToken);

        return new ProvisioningResult(
            Mode: mode,
            TemplateKey: loaded.Template.TemplateKey,
            TemplateHash: templateHash,
            Tokens: tokens,
            TargetRoot: plan.TargetRoot,
            ExpectedItems: plan.Items,
            CreatedItems: executionResult.CreatedItems,
            MissingRequired: executionResult.MissingRequired,
            Warnings: executionResult.Warnings,
            Errors: executionResult.Errors,
            ManifestPath: manifestPath
        );
    }

    private static ProvisioningManifest BuildManifest(
        string templateKey,
        string templateHash,
        ProvisioningMode mode,
        ProvisioningTokens tokens,
        FolderPlan plan,
        ExecutionResult executionResult)
    {
        return new ProvisioningManifest
        {
            TemplateKey = templateKey,
            TemplateHash = templateHash,
            RunMode = mode.ToString().ToLowerInvariant(),
            TimestampUtc = DateTimeOffset.UtcNow,
            Tokens = tokens.ToDictionary(),
            TargetRoot = plan.TargetRoot,
            ExpectedItems = plan.Items.Select(ToManifestItem).ToList(),
            CreatedItems = executionResult.CreatedItems.Select(ToManifestItem).ToList(),
            MissingRequired = executionResult.MissingRequired.ToList(),
            Warnings = executionResult.Warnings.ToList(),
            Errors = executionResult.Errors.ToList()
        };
    }

    private static ManifestItem ToManifestItem(PlanItem item)
    {
        return new ManifestItem
        {
            Path = item.RelativePath,
            Kind = item.Kind.ToString().ToLowerInvariant(),
            Optional = item.Optional
        };
    }

    private static string ResolveSeedsPath(string? seedsPath, string templatePath)
    {
        if (!string.IsNullOrWhiteSpace(seedsPath))
        {
            return Path.GetFullPath(seedsPath);
        }

        var templateDir = Path.GetDirectoryName(templatePath) ?? string.Empty;
        return Path.GetFullPath(Path.Combine(templateDir, "seeds"));
    }

    internal static string ResolveManifestPath(FolderPlan plan)
    {
        var manifestFolderRelative = Path.Combine("00_Admin", ".mgf", "manifest");
        var manifestFolder = plan.Items.FirstOrDefault(
            item =>
                item.Kind == PlanItemKind.Folder
                && string.Equals(item.RelativePath, manifestFolderRelative, StringComparison.OrdinalIgnoreCase)
        );

        var manifestDir = manifestFolder?.AbsolutePath ?? Path.Combine(plan.TargetRoot, manifestFolderRelative);
        return Path.Combine(manifestDir, "folder_manifest.json");
    }

    private static void EnsureEditorTokenNotInRoot(FolderNode root)
    {
        if (root.Name.Contains("{EDITOR_INITIALS}", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Editor initials are required when root name contains {EDITOR_INITIALS}.");
        }
    }

    private static void MarkEditorNodesOptional(FolderNode node)
    {
        if (node.Name.Contains("{EDITOR_INITIALS}", StringComparison.Ordinal))
        {
            node.Optional = true;
        }

        if (node.Children is null)
        {
            return;
        }

        foreach (var child in node.Children)
        {
            MarkEditorNodesOptional(child);
        }
    }

    private static ProvisioningSummary ToSummary(ProvisioningResult result)
    {
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

    internal static DropboxMovePlan BuildDropboxMovePlan(bool hasActive, bool hasToArchive, bool hasArchive)
    {
        if (hasArchive)
        {
            return new DropboxMovePlan("already_archived", ShouldMoveToArchive: false);
        }

        if (hasToArchive)
        {
            return new DropboxMovePlan("ready_to_archive", ShouldMoveToArchive: false);
        }

        if (hasActive)
        {
            return new DropboxMovePlan("ready_to_archive", ShouldMoveToArchive: true);
        }

        return new DropboxMovePlan("container_missing", ShouldMoveToArchive: false);
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

    private static string ResolveTemplatesRoot()
    {
        var baseDir = AppContext.BaseDirectory;
        var templatesRoot = Path.Combine(baseDir, "artifacts", "templates");
        if (Directory.Exists(templatesRoot))
        {
            return templatesRoot;
        }

        throw new DirectoryNotFoundException($"Templates folder not found at {templatesRoot}.");
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

    private static async Task CopyDirectoryAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        if (!Directory.Exists(sourcePath))
        {
            throw new DirectoryNotFoundException($"Source directory not found: {sourcePath}");
        }

        Directory.CreateDirectory(destinationPath);

        foreach (var file in Directory.EnumerateFiles(sourcePath))
        {
            var fileName = Path.GetFileName(file);
            var destFile = Path.Combine(destinationPath, fileName);
            File.Copy(file, destFile, overwrite: true);
        }

        foreach (var directory in Directory.EnumerateDirectories(sourcePath))
        {
            var name = Path.GetFileName(directory);
            var destDir = Path.Combine(destinationPath, name);
            await CopyDirectoryAsync(directory, destDir, cancellationToken);
        }
    }
}

internal sealed record DropboxMovePlan(string RootState, bool ShouldMoveToArchive);



