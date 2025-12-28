namespace MGF.Worker.ProjectBootstrap;

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MGF.Infrastructure.Data;
using MGF.Tools.Provisioner;

public sealed class ProjectBootstrapper
{
    private const int MaxRunsToKeep = 10;
    private const string StatusReady = "ready_to_provision";
    private const string StatusProvisioning = "provisioning";
    private const string StatusActive = "active";
    private const string StatusProvisionFailed = "provision_failed";
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

    public async Task<ProjectBootstrapRunResult> RunAsync(
        AppDbContext db,
        ProjectBootstrapPayload payload,
        string jobId,
        CancellationToken cancellationToken)
    {
        var project = await db.Projects.AsNoTracking()
            .SingleOrDefaultAsync(p => p.ProjectId == payload.ProjectId, cancellationToken);

        if (project is null)
        {
            throw new InvalidOperationException($"Project not found: {payload.ProjectId}");
        }

        var repoRoot = FindRepoRoot();
        var domains = BuildDomainDefinitions(repoRoot);

        var startedAt = DateTimeOffset.UtcNow;
        var results = new List<ProjectBootstrapDomainResult>();
        var hasErrors = false;
        string? lastError = null;

        if (!payload.AllowNonReal && !string.Equals(project.DataProfile, "real", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var domain in domains)
            {
                results.Add(new ProjectBootstrapDomainResult(
                    DomainKey: domain.DomainKey,
                    RootPath: ResolveDomainRootPath(domain, payload, repoRoot),
                    RootState: "blocked_non_real",
                    DomainRootProvisioning: null,
                    ProjectContainerProvisioning: null,
                    Notes: new[] { $"Project data_profile='{project.DataProfile}' is not eligible for provisioning." }
                ));
            }

            var blockedResult = new ProjectBootstrapRunResult(
                JobId: jobId,
                ProjectId: payload.ProjectId,
                EditorInitials: payload.EditorInitials,
                StartedAtUtc: startedAt,
                VerifyDomainRoots: payload.VerifyDomainRoots,
                CreateDomainRoots: payload.CreateDomainRoots,
                ProvisionProjectContainers: payload.ProvisionProjectContainers,
                AllowRepair: payload.AllowRepair,
                ForceSandbox: payload.ForceSandbox,
                AllowNonReal: payload.AllowNonReal,
                Force: payload.Force,
                TestMode: payload.TestMode,
                AllowTestCleanup: payload.AllowTestCleanup,
                Domains: results,
                HasErrors: true,
                LastError: $"Project data_profile='{project.DataProfile}' is not eligible for provisioning."
            );

            await AppendProvisioningRunAsync(db, project.ProjectId, project.Metadata, blockedResult, cancellationToken);
            return blockedResult;
        }

        if (!ProjectBootstrapGuards.TryValidateStart(project.StatusKey, payload.Force, out var statusError, out var alreadyProvisioning))
        {
            foreach (var domain in domains)
            {
                results.Add(new ProjectBootstrapDomainResult(
                    DomainKey: domain.DomainKey,
                    RootPath: ResolveDomainRootPath(domain, payload, repoRoot),
                    RootState: alreadyProvisioning ? "blocked_already_provisioning" : "blocked_status_not_ready",
                    DomainRootProvisioning: null,
                    ProjectContainerProvisioning: null,
                    Notes: new[] { statusError ?? "Project status not eligible for provisioning." }
                ));
            }

            var blockedResult = new ProjectBootstrapRunResult(
                JobId: jobId,
                ProjectId: payload.ProjectId,
                EditorInitials: payload.EditorInitials,
                StartedAtUtc: startedAt,
                VerifyDomainRoots: payload.VerifyDomainRoots,
                CreateDomainRoots: payload.CreateDomainRoots,
                ProvisionProjectContainers: payload.ProvisionProjectContainers,
                AllowRepair: payload.AllowRepair,
                ForceSandbox: payload.ForceSandbox,
                AllowNonReal: payload.AllowNonReal,
                Force: payload.Force,
                TestMode: payload.TestMode,
                AllowTestCleanup: payload.AllowTestCleanup,
                Domains: results,
                HasErrors: true,
                LastError: statusError
            );

            await AppendProvisioningRunAsync(db, project.ProjectId, project.Metadata, blockedResult, cancellationToken);
            return blockedResult;
        }

        await UpdateProjectStatusAsync(db, project.ProjectId, StatusProvisioning, cancellationToken);

        var clientName = await db.Clients.AsNoTracking()
            .Where(c => c.ClientId == project.ClientId)
            .Select(c => c.DisplayName)
            .SingleOrDefaultAsync(cancellationToken);

        var tokens = ProvisioningTokens.Create(project.ProjectCode, project.Name, clientName, payload.EditorInitials);

        try
        {
            foreach (var domain in domains)
            {
                var result = await ProcessDomainAsync(domain, payload, tokens, repoRoot, cancellationToken);
                results.Add(result);

                if (result.DomainRootProvisioning is { Success: false }
                    || result.ProjectContainerProvisioning is { Success: false }
                    || IsErrorRootState(result.RootState))
                {
                    hasErrors = true;
                }
            }

            if (hasErrors)
            {
                lastError = BuildLastError(results);
            }

            var runResult = new ProjectBootstrapRunResult(
                JobId: jobId,
                ProjectId: payload.ProjectId,
                EditorInitials: payload.EditorInitials,
                StartedAtUtc: startedAt,
                VerifyDomainRoots: payload.VerifyDomainRoots,
                CreateDomainRoots: payload.CreateDomainRoots,
                ProvisionProjectContainers: payload.ProvisionProjectContainers,
                AllowRepair: payload.AllowRepair,
                ForceSandbox: payload.ForceSandbox,
                AllowNonReal: payload.AllowNonReal,
                Force: payload.Force,
                TestMode: payload.TestMode,
                AllowTestCleanup: payload.AllowTestCleanup,
                Domains: results,
                HasErrors: hasErrors,
                LastError: lastError
            );

            await AppendProvisioningRunAsync(db, project.ProjectId, project.Metadata, runResult, cancellationToken);

            var finalStatus = hasErrors ? StatusProvisionFailed : StatusActive;
            await UpdateProjectStatusAsync(db, project.ProjectId, finalStatus, cancellationToken);

            return runResult;
        }
        catch (Exception ex)
        {
            var failedResult = new ProjectBootstrapRunResult(
                JobId: jobId,
                ProjectId: payload.ProjectId,
                EditorInitials: payload.EditorInitials,
                StartedAtUtc: startedAt,
                VerifyDomainRoots: payload.VerifyDomainRoots,
                CreateDomainRoots: payload.CreateDomainRoots,
                ProvisionProjectContainers: payload.ProvisionProjectContainers,
                AllowRepair: payload.AllowRepair,
                ForceSandbox: payload.ForceSandbox,
                AllowNonReal: payload.AllowNonReal,
                Force: payload.Force,
                TestMode: payload.TestMode,
                AllowTestCleanup: payload.AllowTestCleanup,
                Domains: results,
                HasErrors: true,
                LastError: ex.Message
            );

            await AppendProvisioningRunAsync(db, project.ProjectId, project.Metadata, failedResult, cancellationToken);
            await UpdateProjectStatusAsync(db, project.ProjectId, StatusProvisionFailed, cancellationToken);
            throw;
        }
    }

    public static ProjectBootstrapPayload ParsePayload(string payloadJson)
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

    private async Task<ProjectBootstrapDomainResult> ProcessDomainAsync(
        DomainDefinition domain,
        ProjectBootstrapPayload payload,
        ProvisioningTokens tokens,
        string repoRoot,
        CancellationToken cancellationToken)
    {
        var notes = new List<string>();
        var rootPath = ResolveDomainRootPath(domain, payload, repoRoot);

        if (string.IsNullOrWhiteSpace(rootPath))
        {
            return new ProjectBootstrapDomainResult(
                DomainKey: domain.DomainKey,
                RootPath: string.Empty,
                RootState: "skipped_unconfigured",
                DomainRootProvisioning: null,
                ProjectContainerProvisioning: null,
                Notes: new[] { "Root path not configured." }
            );
        }

        if (payload.ForceSandbox && !rootPath.StartsWith(repoRoot, StringComparison.OrdinalIgnoreCase))
        {
            return new ProjectBootstrapDomainResult(
                DomainKey: domain.DomainKey,
                RootPath: rootPath,
                RootState: "blocked_sandbox_outside_repo",
                DomainRootProvisioning: null,
                ProjectContainerProvisioning: null,
                Notes: new[] { "Sandbox root must be within repo runtime." }
            );
        }

        var rootExists = Directory.Exists(rootPath);
        ProvisioningSummary? domainRootProvisioning = null;
        var rootState = rootExists ? "ready_existing_root" : "missing_root";

        if (!rootExists)
        {
            if (!payload.CreateDomainRoots)
            {
                return new ProjectBootstrapDomainResult(
                    DomainKey: domain.DomainKey,
                    RootPath: rootPath,
                    RootState: "blocked_missing_root",
                    DomainRootProvisioning: null,
                    ProjectContainerProvisioning: null,
                    Notes: new[] { "Root path missing and CreateDomainRoots=false." }
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

            if (created.Success && payload.VerifyDomainRoots)
            {
                var verified = await VerifyOrRepairAsync(
                    templatePath: domain.DomainRootTemplatePath,
                    basePath: GetParentPath(rootPath, domain.DomainKey),
                    tokens: tokens,
                    allowRepair: payload.AllowRepair,
                    rootNameOverride: GetRootFolderName(rootPath),
                    cancellationToken: cancellationToken
                );
                domainRootProvisioning = ToSummary(verified);
                rootState = verified.Success ? "root_verified" : "root_verify_failed";
            }
        }
        else if (payload.VerifyDomainRoots)
        {
            var verified = await VerifyOrRepairAsync(
                templatePath: domain.DomainRootTemplatePath,
                basePath: GetParentPath(rootPath, domain.DomainKey),
                tokens: tokens,
                allowRepair: payload.AllowRepair,
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
        if (payload.ProvisionProjectContainers && rootExists && IsRootReady(rootState))
        {
            var containerBasePath = payload.TestMode
                ? ProjectBootstrapGuards.BuildTestContainerBasePath(rootPath)
                : Path.Combine(rootPath, domain.ProjectContainerSubfolder);

            var testTargetPath = payload.TestMode
                ? ProjectBootstrapGuards.BuildTestContainerTargetPath(rootPath, tokens)
                : null;

            if (payload.TestMode && testTargetPath is not null && Directory.Exists(testTargetPath))
            {
                if (!ProjectBootstrapGuards.TryValidateTestCleanup(rootPath, testTargetPath, payload.AllowTestCleanup, out var cleanupError))
                {
                    notes.Add(cleanupError ?? "Test cleanup blocked.");
                    return new ProjectBootstrapDomainResult(
                        DomainKey: domain.DomainKey,
                        RootPath: rootPath,
                        RootState: "blocked_test_cleanup",
                        DomainRootProvisioning: domainRootProvisioning,
                        ProjectContainerProvisioning: null,
                        Notes: notes
                    );
                }

                Directory.Delete(testTargetPath, recursive: true);
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

            containerProvisioning = ToSummary(applied);

            var verified = await VerifyOrRepairAsync(
                templatePath: domain.ProjectContainerTemplatePath,
                basePath: containerBasePath,
                tokens: tokens,
                allowRepair: payload.AllowRepair,
                rootNameOverride: null,
                cancellationToken: cancellationToken
            );

            containerProvisioning = ToSummary(verified);
        }
        else if (payload.ProvisionProjectContainers && !IsRootReady(rootState))
        {
            notes.Add("Project containers skipped because root is not ready.");
        }
        else if (!payload.ProvisionProjectContainers)
        {
            notes.Add("Project containers skipped (ProvisionProjectContainers=false).");
        }

        return new ProjectBootstrapDomainResult(
            DomainKey: domain.DomainKey,
            RootPath: rootPath,
            RootState: rootState,
            DomainRootProvisioning: domainRootProvisioning,
            ProjectContainerProvisioning: containerProvisioning,
            Notes: notes
        );
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

    private static string ResolveManifestPath(FolderPlan plan)
    {
        var mgfFolderRelative = Path.Combine("00_Admin", ".mgf");
        var mgfFolder = plan.Items.FirstOrDefault(
            item =>
                item.Kind == PlanItemKind.Folder
                && string.Equals(item.RelativePath, mgfFolderRelative, StringComparison.OrdinalIgnoreCase)
        );

        var manifestDir = mgfFolder?.AbsolutePath ?? plan.TargetRoot;
        return Path.Combine(manifestDir, "folder_manifest.json");
    }

    private async Task AppendProvisioningRunAsync(
        AppDbContext db,
        string projectId,
        JsonElement metadata,
        ProjectBootstrapRunResult runResult,
        CancellationToken cancellationToken)
    {
        var root = JsonNode.Parse(metadata.GetRawText()) as JsonObject ?? new JsonObject();
        var provisioning = root["provisioning"] as JsonObject ?? new JsonObject();
        var runs = provisioning["runs"] as JsonArray ?? new JsonArray();

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

        provisioning["runs"] = runs;
        root["provisioning"] = provisioning;

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

    private string ResolveDomainRootPath(DomainDefinition domain, ProjectBootstrapPayload payload, string repoRoot)
    {
        if (payload.ForceSandbox)
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
            || rootState.EndsWith("_failed", StringComparison.OrdinalIgnoreCase);
    }

    private static string? BuildLastError(IEnumerable<ProjectBootstrapDomainResult> results)
    {
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

    private static IReadOnlyList<DomainDefinition> BuildDomainDefinitions(string repoRoot)
    {
        return new[]
        {
            new DomainDefinition(
                DomainKey: "dropbox",
                DomainRootTemplatePath: Path.Combine(repoRoot, "docs", "templates", "domain_dropbox_root.json"),
                ProjectContainerTemplatePath: Path.Combine(repoRoot, "docs", "templates", "dropbox_project_container.json"),
                ProjectContainerSubfolder: "02_Projects_Active"
            ),
            new DomainDefinition(
                DomainKey: "lucidlink",
                DomainRootTemplatePath: Path.Combine(repoRoot, "docs", "templates", "domain_lucidlink_root.json"),
                ProjectContainerTemplatePath: Path.Combine(repoRoot, "docs", "templates", "lucidlink_production_container.json"),
                ProjectContainerSubfolder: "01_Productions_Active"
            ),
            new DomainDefinition(
                DomainKey: "nas",
                DomainRootTemplatePath: Path.Combine(repoRoot, "docs", "templates", "domain_nas_root.json"),
                ProjectContainerTemplatePath: Path.Combine(repoRoot, "docs", "templates", "nas_archive_container.json"),
                ProjectContainerSubfolder: "01_Projects_Archive"
            )
        };
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
        string DomainRootTemplatePath,
        string ProjectContainerTemplatePath,
        string ProjectContainerSubfolder
    );
}
