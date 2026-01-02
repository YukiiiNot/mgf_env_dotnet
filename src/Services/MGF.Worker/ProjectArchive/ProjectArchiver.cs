namespace MGF.Worker.ProjectArchive;

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MGF.Data.Data;
using MGF.FolderProvisioning;
using MGF.Storage.ProjectBootstrap;

public sealed class ProjectArchiver
{
    private const int MaxRunsToKeep = 10;
    private readonly IConfiguration configuration;
    private readonly FolderTemplateLoader templateLoader = new();
    private readonly FolderTemplatePlanner planner = new();
    private readonly FolderPlanExecutor executor;
    private readonly LocalFileStore fileStore = new();

    public ProjectArchiver(IConfiguration configuration)
    {
        this.configuration = configuration;
        executor = new FolderPlanExecutor(fileStore);
    }

    private sealed record ArchivePathTemplates(
        string DropboxActiveRelpath,
        string DropboxToArchiveRelpath,
        string DropboxArchiveRelpath,
        string NasArchiveRelpath
    );

    public async Task<ProjectArchiveRunResult> RunAsync(
        AppDbContext db,
        ProjectArchivePayload payload,
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
        var results = new List<ProjectArchiveDomainResult>();

        if (!payload.AllowNonReal && !string.Equals(project.DataProfile, "real", StringComparison.OrdinalIgnoreCase))
        {
            var blocked = BuildBlockedResults(
                "blocked_non_real",
                $"Project data_profile='{project.DataProfile}' is not eligible for archive."
            );
            var blockedResult = new ProjectArchiveRunResult(
                JobId: jobId,
                ProjectId: payload.ProjectId,
                EditorInitials: payload.EditorInitials,
                StartedAtUtc: startedAt,
                TestMode: payload.TestMode,
                AllowTestCleanup: payload.AllowTestCleanup,
                AllowNonReal: payload.AllowNonReal,
                Force: payload.Force,
                Domains: blocked,
                HasErrors: true,
                LastError: $"Project data_profile='{project.DataProfile}' is not eligible for archive."
            );

            await AppendArchiveRunAsync(db, project.ProjectId, project.Metadata, blockedResult, cancellationToken);
            return blockedResult;
        }

        if (!ProjectArchiveGuards.TryValidateStart(project.StatusKey, payload.Force, out var statusError, out var alreadyArchiving))
        {
            var rootState = alreadyArchiving ? "blocked_already_archiving" : "blocked_status_not_ready";
            var blocked = BuildBlockedResults(rootState, statusError ?? "Project status not eligible for archiving.");
            var blockedResult = new ProjectArchiveRunResult(
                JobId: jobId,
                ProjectId: payload.ProjectId,
                EditorInitials: payload.EditorInitials,
                StartedAtUtc: startedAt,
                TestMode: payload.TestMode,
                AllowTestCleanup: payload.AllowTestCleanup,
                AllowNonReal: payload.AllowNonReal,
                Force: payload.Force,
                Domains: blocked,
                HasErrors: true,
                LastError: statusError
            );

            await AppendArchiveRunAsync(db, project.ProjectId, project.Metadata, blockedResult, cancellationToken);
            return blockedResult;
        }

        await UpdateProjectStatusAsync(db, project.ProjectId, ProjectArchiveGuards.StatusArchiving, cancellationToken);

        var clientName = await db.Clients.AsNoTracking()
            .Where(c => c.ClientId == project.ClientId)
            .Select(c => c.DisplayName)
            .SingleOrDefaultAsync(cancellationToken);

        var tokens = ProvisioningTokens.Create(project.ProjectCode, project.Name, clientName, payload.EditorInitials);
        var templatesRoot = ResolveTemplatesRoot();
        var pathTemplates = await LoadPathTemplatesAsync(db, cancellationToken);

        var projectFolderName = await ResolveProjectFolderNameAsync(
            Path.Combine(templatesRoot, "dropbox_project_container.json"),
            tokens,
            cancellationToken
        );

        var dropboxResult = await ProcessDropboxAsync(payload, projectFolderName, pathTemplates, cancellationToken);
        var lucidlinkResult = await ProcessLucidlinkAsync(payload, projectFolderName);
        var nasResult = await ProcessNasAsync(
            payload,
            templatesRoot,
            projectFolderName,
            lucidlinkResult,
            tokens,
            pathTemplates,
            cancellationToken
        );

        results.Add(dropboxResult);
        results.Add(lucidlinkResult);
        results.Add(nasResult);

        if (IsDomainSuccess(nasResult)
            && dropboxResult.RootState is "ready_to_archive" or "already_archived")
        {
            var updatedDropbox = await FinalizeDropboxArchiveAsync(
                dropboxResult,
                payload,
                projectFolderName,
                pathTemplates,
                cancellationToken
            );
            results[0] = updatedDropbox;
            dropboxResult = updatedDropbox;
        }

        var hasErrors = results.Any(IsDomainError);
        var lastError = hasErrors ? BuildLastError(results) : null;

        var runResult = new ProjectArchiveRunResult(
            JobId: jobId,
            ProjectId: payload.ProjectId,
            EditorInitials: payload.EditorInitials,
            StartedAtUtc: startedAt,
            TestMode: payload.TestMode,
            AllowTestCleanup: payload.AllowTestCleanup,
            AllowNonReal: payload.AllowNonReal,
            Force: payload.Force,
            Domains: results,
            HasErrors: hasErrors,
            LastError: lastError
        );

        await AppendArchiveRunAsync(db, project.ProjectId, project.Metadata, runResult, cancellationToken);

        var finalStatus = hasErrors ? ProjectArchiveGuards.StatusArchiveFailed : ProjectArchiveGuards.StatusArchived;
        await UpdateProjectStatusAsync(db, project.ProjectId, finalStatus, cancellationToken);

        return runResult;
    }

    public static ProjectArchivePayload ParsePayload(string payloadJson)
    {
        using var doc = JsonDocument.Parse(payloadJson);
        var root = doc.RootElement;

        var projectId = root.TryGetProperty("projectId", out var projectIdElement) ? projectIdElement.GetString() : null;
        if (string.IsNullOrWhiteSpace(projectId))
        {
            throw new InvalidOperationException("projectId is required in project.archive payload.");
        }

        var editorInitials = ReadEditorInitials(root);

        return new ProjectArchivePayload(
            ProjectId: projectId,
            EditorInitials: editorInitials,
            TestMode: ReadBoolean(root, "testMode", false),
            AllowTestCleanup: ReadBoolean(root, "allowTestCleanup", false),
            AllowNonReal: ReadBoolean(root, "allowNonReal", false),
            Force: ReadBoolean(root, "force", false)
        );
    }
    private async Task<ProjectArchiveDomainResult> ProcessDropboxAsync(
        ProjectArchivePayload payload,
        string projectFolderName,
        ArchivePathTemplates pathTemplates,
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

    private async Task<ProjectArchiveDomainResult> FinalizeDropboxArchiveAsync(
        ProjectArchiveDomainResult dropboxResult,
        ProjectArchivePayload payload,
        string projectFolderName,
        ArchivePathTemplates pathTemplates,
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
    private Task<ProjectArchiveDomainResult> ProcessLucidlinkAsync(
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

    private async Task<ProjectArchiveDomainResult> ProcessNasAsync(
        ProjectArchivePayload payload,
        string templatesRoot,
        string projectFolderName,
        ProjectArchiveDomainResult lucidlinkResult,
        ProvisioningTokens tokens,
        ArchivePathTemplates pathTemplates,
        CancellationToken cancellationToken)
    {
        var notes = new List<string>();
        var actions = new List<ArchiveActionSummary>();

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
                tokens: tokens,
                mode: ProvisioningMode.Apply,
                forceOverwrite: false,
                rootNameOverride: null,
                cancellationToken: cancellationToken
            );

            verified = await ExecuteTemplateAsync(
                templatePath: Path.Combine(templatesRoot, "nas_archive_container.json"),
                basePath: baseRoot,
                tokens: tokens,
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

    private static async Task<ArchivePathTemplates> LoadPathTemplatesAsync(
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var keys = new[]
        {
            "dropbox_active_container_root",
            "dropbox_to_archive_container_root",
            "dropbox_archive_container_root",
            "nas_archive_root",
        };

        var templateRows = await db.Set<Dictionary<string, object>>("path_templates")
            .Where(row => keys.Contains(EF.Property<string>(row, "path_key")))
            .Select(row => new
            {
                Key = EF.Property<string>(row, "path_key"),
                Relpath = EF.Property<string>(row, "relpath"),
            })
            .ToListAsync(cancellationToken);

        string Resolve(string key, string fallback)
        {
            var match = templateRows.FirstOrDefault(row => string.Equals(row.Key, key, StringComparison.OrdinalIgnoreCase));
            if (match is null || string.IsNullOrWhiteSpace(match.Relpath))
            {
                return fallback;
            }

            return match.Relpath.Trim();
        }

        return new ArchivePathTemplates(
            DropboxActiveRelpath: Resolve("dropbox_active_container_root", "02_Projects_Active"),
            DropboxToArchiveRelpath: Resolve("dropbox_to_archive_container_root", "03_Projects_ToArchive"),
            DropboxArchiveRelpath: Resolve("dropbox_archive_container_root", "98_Archive"),
            NasArchiveRelpath: Resolve("nas_archive_root", "01_Projects_Archive")
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

    private async Task AppendArchiveRunAsync(
        AppDbContext db,
        string projectId,
        JsonElement metadata,
        ProjectArchiveRunResult runResult,
        CancellationToken cancellationToken)
    {
        var root = JsonNode.Parse(metadata.GetRawText()) as JsonObject ?? new JsonObject();
        var archiving = root["archiving"] as JsonObject ?? new JsonObject();
        var runs = archiving["runs"] as JsonArray ?? new JsonArray();

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

        archiving["runs"] = runs;
        root["archiving"] = archiving;

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

    private static bool IsDomainSuccess(ProjectArchiveDomainResult result)
    {
        return result.RootState is "archived" or "already_archived" or "archive_verified" or "archive_repaired" or "source_found";
    }

    private static bool IsDomainError(ProjectArchiveDomainResult result)
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

        return result.RootState is "container_missing" or "source_missing" or "archive_move_missing";
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

    private static string? BuildLastError(IReadOnlyList<ProjectArchiveDomainResult> results)
    {
        foreach (var result in results)
        {
            if (result.Notes.Count > 0 && IsDomainError(result))
            {
                return result.Notes[0];
            }

            if (result.TargetProvisioning?.Errors.Count > 0)
            {
                return result.TargetProvisioning.Errors[0];
            }
        }

        return "project.archive completed with errors.";
    }

    private static List<ProjectArchiveDomainResult> BuildBlockedResults(string rootState, string note)
    {
        return new List<ProjectArchiveDomainResult>
        {
            new(
                DomainKey: "dropbox",
                RootPath: string.Empty,
                RootState: rootState,
                DomainRootProvisioning: null,
                TargetProvisioning: null,
                Actions: Array.Empty<ArchiveActionSummary>(),
                Notes: new[] { note }
            ),
            new(
                DomainKey: "lucidlink",
                RootPath: string.Empty,
                RootState: rootState,
                DomainRootProvisioning: null,
                TargetProvisioning: null,
                Actions: Array.Empty<ArchiveActionSummary>(),
                Notes: new[] { note }
            ),
            new(
                DomainKey: "nas",
                RootPath: string.Empty,
                RootState: rootState,
                DomainRootProvisioning: null,
                TargetProvisioning: null,
                Actions: Array.Empty<ArchiveActionSummary>(),
                Notes: new[] { note }
            )
        };
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



