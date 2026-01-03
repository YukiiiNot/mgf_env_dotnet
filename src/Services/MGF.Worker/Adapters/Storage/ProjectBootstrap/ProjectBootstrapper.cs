namespace MGF.Worker.Adapters.Storage.ProjectBootstrap;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MGF.Contracts.Abstractions.ProjectBootstrap;
using MGF.FolderProvisioning;

public sealed class ProjectBootstrapper
{
    private readonly IConfiguration configuration;
    private readonly FolderTemplateLoader templateLoader = new();
    private readonly FolderTemplatePlanner planner = new();
    private readonly FolderPlanExecutor executor;
    private readonly LocalFileStore fileStore = new();

    public ProjectBootstrapper(IConfiguration configuration)
    {
        this.configuration = configuration;
        executor = new FolderPlanExecutor(fileStore);
    }

    public async Task<ProjectBootstrapExecutionResult> RunAsync(
        ProjectBootstrapContext context,
        BootstrapProjectRequest request,
        CancellationToken cancellationToken)
    {
        var repoRoot = FindRepoRoot();
        var templatesRoot = ResolveTemplatesRoot();
        var domains = BuildDomainDefinitions(templatesRoot);

        var startedAt = DateTimeOffset.UtcNow;
        var results = new List<ProjectBootstrapDomainResult>();
        var storageRootCandidates = new List<ProjectBootstrapStorageRootCandidate>();
        var hasErrors = false;
        string? lastError = null;

        var tokens = ProvisioningTokens.Create(
            context.ProjectCode,
            context.ProjectName,
            context.ClientName,
            request.EditorInitials);

        try
        {
            foreach (var domain in domains)
            {
                var result = await ProcessDomainAsync(
                    request,
                    domain,
                    tokens,
                    repoRoot,
                    cancellationToken);
                results.Add(result.DomainResult);
                if (result.StorageRootCandidate is not null)
                {
                    storageRootCandidates.Add(result.StorageRootCandidate);
                }
            }

            var anySuccess = results.Any(IsDomainSuccess);
            var hardFailures = results.Any(IsHardFailure);
            hasErrors = hardFailures || !anySuccess;

            if (hasErrors)
            {
                lastError = BuildLastError(results, anySuccess, hardFailures);
            }

            var runResult = new ProjectBootstrapRunResult(
                JobId: request.JobId,
                ProjectId: request.ProjectId,
                EditorInitials: request.EditorInitials,
                StartedAtUtc: startedAt,
                VerifyDomainRoots: request.VerifyDomainRoots,
                CreateDomainRoots: request.CreateDomainRoots,
                ProvisionProjectContainers: request.ProvisionProjectContainers,
                AllowRepair: request.AllowRepair,
                ForceSandbox: request.ForceSandbox,
                AllowNonReal: request.AllowNonReal,
                Force: request.Force,
                TestMode: request.TestMode,
                AllowTestCleanup: request.AllowTestCleanup,
                Domains: results,
                HasErrors: hasErrors,
                LastError: lastError
            );

            return new ProjectBootstrapExecutionResult(runResult, storageRootCandidates, null);
        }
        catch (Exception ex)
        {
            var failedResult = new ProjectBootstrapRunResult(
                JobId: request.JobId,
                ProjectId: request.ProjectId,
                EditorInitials: request.EditorInitials,
                StartedAtUtc: startedAt,
                VerifyDomainRoots: request.VerifyDomainRoots,
                CreateDomainRoots: request.CreateDomainRoots,
                ProvisionProjectContainers: request.ProvisionProjectContainers,
                AllowRepair: request.AllowRepair,
                ForceSandbox: request.ForceSandbox,
                AllowNonReal: request.AllowNonReal,
                Force: request.Force,
                TestMode: request.TestMode,
                AllowTestCleanup: request.AllowTestCleanup,
                Domains: results,
                HasErrors: true,
                LastError: ex.Message
            );

            return new ProjectBootstrapExecutionResult(failedResult, storageRootCandidates, ex);
        }
    }

    public ProjectBootstrapRunResult BuildBlockedNonRealResult(
        ProjectBootstrapContext context,
        BootstrapProjectRequest request)
    {
        var note = $"Project data_profile='{context.DataProfile}' is not eligible for provisioning.";
        return BuildBlockedResult(context, request, "blocked_non_real", note);
    }

    public ProjectBootstrapRunResult BuildBlockedStatusResult(
        ProjectBootstrapContext context,
        BootstrapProjectRequest request,
        string? statusError,
        bool alreadyProvisioning)
    {
        var note = statusError ?? "Project status not eligible for provisioning.";
        var rootState = alreadyProvisioning ? "blocked_already_provisioning" : "blocked_status_not_ready";
        return BuildBlockedResult(context, request, rootState, note);
    }

    private ProjectBootstrapRunResult BuildBlockedResult(
        ProjectBootstrapContext context,
        BootstrapProjectRequest request,
        string rootState,
        string note)
    {
        var repoRoot = FindRepoRoot();
        var templatesRoot = ResolveTemplatesRoot();
        var domains = BuildDomainDefinitions(templatesRoot);
        var startedAt = DateTimeOffset.UtcNow;

        var results = new List<ProjectBootstrapDomainResult>();
        foreach (var domain in domains)
        {
            results.Add(new ProjectBootstrapDomainResult(
                DomainKey: domain.DomainKey,
                RootPath: ResolveDomainRootPath(domain, request, repoRoot),
                RootState: rootState,
                DomainRootProvisioning: null,
                ProjectContainerProvisioning: null,
                Notes: new[] { note }
            ));
        }

        return new ProjectBootstrapRunResult(
            JobId: request.JobId,
            ProjectId: request.ProjectId,
            EditorInitials: request.EditorInitials,
            StartedAtUtc: startedAt,
            VerifyDomainRoots: request.VerifyDomainRoots,
            CreateDomainRoots: request.CreateDomainRoots,
            ProvisionProjectContainers: request.ProvisionProjectContainers,
            AllowRepair: request.AllowRepair,
            ForceSandbox: request.ForceSandbox,
            AllowNonReal: request.AllowNonReal,
            Force: request.Force,
            TestMode: request.TestMode,
            AllowTestCleanup: request.AllowTestCleanup,
            Domains: results,
            HasErrors: true,
            LastError: note
        );
    }

    private async Task<ProjectBootstrapDomainExecutionResult> ProcessDomainAsync(
        BootstrapProjectRequest request,
        DomainDefinition domain,
        ProvisioningTokens tokens,
        string repoRoot,
        CancellationToken cancellationToken)
    {
        var notes = new List<string>();
        var rootPath = ResolveDomainRootPath(domain, request, repoRoot);

        if (string.IsNullOrWhiteSpace(rootPath))
        {
            return new ProjectBootstrapDomainExecutionResult(
                new ProjectBootstrapDomainResult(
                    DomainKey: domain.DomainKey,
                    RootPath: string.Empty,
                    RootState: "skipped_unconfigured",
                    DomainRootProvisioning: null,
                    ProjectContainerProvisioning: null,
                    Notes: new[] { "Root path not configured." }
                ),
                null
            );
        }

        if (request.ForceSandbox && !rootPath.StartsWith(repoRoot, StringComparison.OrdinalIgnoreCase))
        {
            return new ProjectBootstrapDomainExecutionResult(
                new ProjectBootstrapDomainResult(
                    DomainKey: domain.DomainKey,
                    RootPath: rootPath,
                    RootState: "blocked_sandbox_outside_repo",
                    DomainRootProvisioning: null,
                    ProjectContainerProvisioning: null,
                    Notes: new[] { "Sandbox root must be within repo runtime." }
                ),
                null
            );
        }

        var rootExists = Directory.Exists(rootPath);
        ProvisioningSummary? domainRootProvisioning = null;
        var rootState = rootExists ? "ready_existing_root" : "missing_root";

        if (!rootExists)
        {
            if (!request.CreateDomainRoots)
            {
                return new ProjectBootstrapDomainExecutionResult(
                    new ProjectBootstrapDomainResult(
                        DomainKey: domain.DomainKey,
                        RootPath: rootPath,
                        RootState: "blocked_missing_root",
                        DomainRootProvisioning: null,
                        ProjectContainerProvisioning: null,
                        Notes: new[] { "Root path missing and CreateDomainRoots=false." }
                    ),
                    null
                );
            }

            var created = await ExecuteTemplateAsync(
                templatePath: domain.DomainRootTemplatePath,
                basePath: GetParentPath(rootPath, domain.DomainKey),
                tokens: tokens,
                mode: ProvisioningMode.Apply,
                forceOverwrite: false,
                rootNameOverride: GetRootFolderName(rootPath),
                cancellationToken: cancellationToken
            );

            domainRootProvisioning = ToSummary(created);
            rootExists = Directory.Exists(rootPath);
            rootState = created.Success ? "root_created" : "root_create_failed";

            if (created.Success && request.VerifyDomainRoots)
            {
                var verified = await VerifyOrRepairAsync(
                    templatePath: domain.DomainRootTemplatePath,
                    basePath: GetParentPath(rootPath, domain.DomainKey),
                    tokens: tokens,
                    allowRepair: request.AllowRepair,
                    rootNameOverride: GetRootFolderName(rootPath),
                    cancellationToken: cancellationToken
                );
                domainRootProvisioning = ToSummary(verified);
                rootState = verified.Success ? "root_verified" : "root_verify_failed";
            }
        }
        else if (request.VerifyDomainRoots)
        {
            var verified = await VerifyOrRepairAsync(
                templatePath: domain.DomainRootTemplatePath,
                basePath: GetParentPath(rootPath, domain.DomainKey),
                tokens: tokens,
                allowRepair: request.AllowRepair,
                rootNameOverride: GetRootFolderName(rootPath),
                cancellationToken: cancellationToken
            );

            domainRootProvisioning = ToSummary(verified);
            rootState = verified.Success ? "root_verified" : "root_verify_failed";
        }

        if (!rootExists)
        {
            notes.Add("Root path still missing after domain root attempt.");
        }

        ProvisioningSummary? containerProvisioning = null;
        ProvisioningResult? containerResult = null;
        if (request.ProvisionProjectContainers && rootExists && IsRootReady(rootState))
        {
            var containerBasePath = request.TestMode
                ? ProjectBootstrapGuards.BuildTestContainerBasePath(rootPath)
                : Path.Combine(rootPath, domain.ProjectContainerSubfolder);

            var testTargetPath = request.TestMode
                ? ProjectBootstrapGuards.BuildTestContainerTargetPath(rootPath, tokens)
                : null;

            if (request.TestMode && testTargetPath is not null && Directory.Exists(testTargetPath))
            {
                if (!ProjectBootstrapGuards.TryValidateTestCleanup(rootPath, testTargetPath, request.AllowTestCleanup, out var cleanupError))
                {
                    notes.Add(cleanupError ?? "Test cleanup blocked.");
                    return new ProjectBootstrapDomainExecutionResult(
                        new ProjectBootstrapDomainResult(
                            DomainKey: domain.DomainKey,
                            RootPath: rootPath,
                            RootState: "blocked_test_cleanup",
                            DomainRootProvisioning: domainRootProvisioning,
                            ProjectContainerProvisioning: null,
                            Notes: notes
                        ),
                        null
                    );
                }

                var cleanup = await ProjectBootstrapCleanupHelper.TryDeleteWithRetryAsync(
                    testTargetPath,
                    cancellationToken
                );
                if (!cleanup.Success)
                {
                    notes.Add(cleanup.Error ?? "Test cleanup failed (locked; cleanup skipped).");
                    return new ProjectBootstrapDomainExecutionResult(
                        new ProjectBootstrapDomainResult(
                            DomainKey: domain.DomainKey,
                            RootPath: rootPath,
                            RootState: "cleanup_locked",
                            DomainRootProvisioning: domainRootProvisioning,
                            ProjectContainerProvisioning: null,
                            Notes: notes
                        ),
                        null
                    );
                }
            }

            var applied = await ExecuteTemplateAsync(
                templatePath: domain.ProjectContainerTemplatePath,
                basePath: containerBasePath,
                tokens: tokens,
                mode: ProvisioningMode.Apply,
                forceOverwrite: false,
                rootNameOverride: null,
                cancellationToken: cancellationToken
            );

            var verified = await VerifyOrRepairAsync(
                templatePath: domain.ProjectContainerTemplatePath,
                basePath: containerBasePath,
                tokens: tokens,
                allowRepair: request.AllowRepair,
                rootNameOverride: null,
                cancellationToken: cancellationToken
            );

            containerResult = verified;
            containerProvisioning = ToSummary(verified);
        }
        else if (request.ProvisionProjectContainers && !IsRootReady(rootState))
        {
            notes.Add("Project containers skipped because root is not ready.");
        }
        else if (!request.ProvisionProjectContainers)
        {
            notes.Add("Project containers skipped (ProvisionProjectContainers=false).");
        }

        ProjectBootstrapStorageRootCandidate? storageRootCandidate = null;
        if (ProjectStorageRootHelper.ShouldUpsert(rootState, containerProvisioning))
        {
            var relpathError = "Container result was missing.";
            if (containerResult is not null
                && ProjectStorageRootHelper.TryBuildFolderRelpath(
                    rootPath,
                    containerResult.TargetRoot,
                    out var folderRelpath,
                    out relpathError))
            {
                var rootKey = ProjectStorageRootHelper.GetRootKey(request.TestMode);
                storageRootCandidate = new ProjectBootstrapStorageRootCandidate(
                    DomainKey: domain.DomainKey,
                    StorageProviderKey: domain.StorageProviderKey,
                    RootKey: rootKey,
                    FolderRelpath: folderRelpath
                );
            }
            else
            {
                notes.Add(relpathError);
                rootState = "storage_root_failed";
            }
        }

        var domainResult = new ProjectBootstrapDomainResult(
            DomainKey: domain.DomainKey,
            RootPath: rootPath,
            RootState: rootState,
            DomainRootProvisioning: domainRootProvisioning,
            ProjectContainerProvisioning: containerProvisioning,
            Notes: notes
        );

        return new ProjectBootstrapDomainExecutionResult(domainResult, storageRootCandidate);
    }

    private async Task<ProvisioningResult> VerifyOrRepairAsync(
        string templatePath,
        string basePath,
        ProvisioningTokens tokens,
        bool allowRepair,
        string? rootNameOverride,
        CancellationToken cancellationToken)
    {
        var verified = await ExecuteTemplateAsync(
            templatePath,
            basePath,
            tokens,
            ProvisioningMode.Verify,
            forceOverwrite: false,
            rootNameOverride,
            cancellationToken
        );

        if (verified.Success || !allowRepair)
        {
            return verified;
        }

        var repaired = await ExecuteTemplateAsync(
            templatePath,
            basePath,
            tokens,
            ProvisioningMode.Repair,
            forceOverwrite: true,
            rootNameOverride,
            cancellationToken
        );

        if (!repaired.Success)
        {
            return repaired;
        }

        return await ExecuteTemplateAsync(
            templatePath,
            basePath,
            tokens,
            ProvisioningMode.Verify,
            forceOverwrite: false,
            rootNameOverride,
            cancellationToken
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

    private string ResolveDomainRootPath(DomainDefinition domain, BootstrapProjectRequest request, string repoRoot)
    {
        if (request.ForceSandbox)
        {
            return Path.Combine(repoRoot, "runtime", $"bootstrap_sandbox_{domain.DomainKey}");
        }

        var raw = domain.DomainKey switch
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

    private static string GetParentPath(string rootPath, string domainKey)
    {
        var parent = Directory.GetParent(rootPath);
        if (parent is null)
        {
            throw new InvalidOperationException($"Root path has no parent for domain {domainKey}: {rootPath}");
        }

        return parent.FullName;
    }

    private static string GetRootFolderName(string rootPath)
    {
        var trimmed = rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var name = Path.GetFileName(trimmed);

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException($"Root folder name could not be determined from path: {rootPath}");
        }

        return name;
    }

    private static bool IsRootReady(string rootState)
    {
        return rootState is "ready_existing_root" or "root_created" or "root_verified";
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

    private static bool IsErrorRootState(string rootState)
    {
        return rootState.StartsWith("blocked_", StringComparison.OrdinalIgnoreCase)
            || rootState.EndsWith("_failed", StringComparison.OrdinalIgnoreCase)
            || rootState.StartsWith("cleanup_", StringComparison.OrdinalIgnoreCase);
    }

    private static string? BuildLastError(
        IReadOnlyList<ProjectBootstrapDomainResult> results,
        bool anySuccess,
        bool hardFailures)
    {
        if (!anySuccess)
        {
            return "No domain provisioning succeeded.";
        }

        if (!hardFailures)
        {
            return null;
        }

        foreach (var result in results)
        {
            if (result.DomainRootProvisioning?.Errors.Count > 0)
            {
                return result.DomainRootProvisioning.Errors[0];
            }

            if (result.ProjectContainerProvisioning?.Errors.Count > 0)
            {
                return result.ProjectContainerProvisioning.Errors[0];
            }

            if (result.Notes.Count > 0 && IsErrorRootState(result.RootState))
            {
                return result.Notes[0];
            }
        }

        return "project.bootstrap completed with provisioning errors.";
    }

    private static bool IsDomainSuccess(ProjectBootstrapDomainResult result)
    {
        return result.ProjectContainerProvisioning?.Success == true;
    }

    private static bool IsHardFailure(ProjectBootstrapDomainResult result)
    {
        if (result.RootState.StartsWith("blocked_", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (result.RootState.StartsWith("cleanup_", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (result.RootState.EndsWith("_failed", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (result.DomainRootProvisioning?.Errors.Count > 0)
        {
            return true;
        }

        if (result.ProjectContainerProvisioning?.Errors.Count > 0)
        {
            return true;
        }

        return false;
    }

    private static IReadOnlyList<DomainDefinition> BuildDomainDefinitions(string templatesRoot)
    {
        return new[]
        {
            new DomainDefinition(
                DomainKey: "dropbox",
                StorageProviderKey: "dropbox",
                DomainRootTemplatePath: Path.Combine(templatesRoot, "domain_dropbox_root.json"),
                ProjectContainerTemplatePath: Path.Combine(templatesRoot, "dropbox_project_container.json"),
                ProjectContainerSubfolder: "02_Projects_Active"
            ),
            new DomainDefinition(
                DomainKey: "lucidlink",
                StorageProviderKey: "lucidlink",
                DomainRootTemplatePath: Path.Combine(templatesRoot, "domain_lucidlink_root.json"),
                ProjectContainerTemplatePath: Path.Combine(templatesRoot, "lucidlink_production_container.json"),
                ProjectContainerSubfolder: "01_Productions_Active"
            ),
            new DomainDefinition(
                DomainKey: "nas",
                StorageProviderKey: "nas",
                DomainRootTemplatePath: Path.Combine(templatesRoot, "domain_nas_root.json"),
                ProjectContainerTemplatePath: Path.Combine(templatesRoot, "nas_archive_container.json"),
                ProjectContainerSubfolder: "01_Projects_Archive"
            )
        };
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

    private sealed record DomainDefinition(
        string DomainKey,
        string StorageProviderKey,
        string DomainRootTemplatePath,
        string ProjectContainerTemplatePath,
        string ProjectContainerSubfolder
    );

    private sealed record ProjectBootstrapDomainExecutionResult(
        ProjectBootstrapDomainResult DomainResult,
        ProjectBootstrapStorageRootCandidate? StorageRootCandidate
    );
}



