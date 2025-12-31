using MGF.Contracts.Abstractions;

namespace MGF.Tools.Provisioner;

public sealed class FolderProvisioner
{
    private const string ManifestFileName = "folder_manifest.json";

    private readonly IFileStore fileStore;
    private readonly FolderTemplateLoader templateLoader;
    private readonly FolderTemplatePlanner planner;
    private readonly FolderPlanExecutor executor;

    public FolderProvisioner(IFileStore fileStore)
    {
        this.fileStore = fileStore;
        templateLoader = new FolderTemplateLoader();
        planner = new FolderTemplatePlanner();
        executor = new FolderPlanExecutor(fileStore);
    }

    public async Task<ProvisioningResult> ExecuteAsync(ProvisioningRequest request, CancellationToken cancellationToken)
    {
        var loadedTemplate = await templateLoader.LoadAsync(request.TemplatePath, request.SchemaPath, cancellationToken);
        var templateHash = Hashing.Sha256Hex(loadedTemplate.TemplateBytes);

        var plan = planner.Plan(loadedTemplate.Template, request.Tokens, request.BasePath);

        var seedsPath = ResolveSeedsPath(request.SeedsPath, loadedTemplate.TemplatePath);

        ExecutionResult executionResult = request.Mode switch
        {
            ProvisioningMode.Plan => new ExecutionResult(),
            ProvisioningMode.Verify => await executor.VerifyAsync(plan, cancellationToken),
            ProvisioningMode.Apply => await executor.ApplyAsync(plan, seedsPath, request.Tokens, allowSeedOverwrite: false, cancellationToken),
            ProvisioningMode.Repair => await executor.ApplyAsync(plan, seedsPath, request.Tokens, request.ForceOverwriteSeededFiles, cancellationToken),
            _ => throw new InvalidOperationException($"Unknown mode {request.Mode}")
        };

        var manifestPath = ResolveManifestPath(plan);
        var manifest = BuildManifest(
            request,
            loadedTemplate.Template.TemplateKey,
            templateHash,
            plan,
            executionResult
        );

        await ProvisioningManifestWriter.WriteAsync(fileStore, manifest, manifestPath, cancellationToken);

        return new ProvisioningResult(
            Mode: request.Mode,
            TemplateKey: loadedTemplate.Template.TemplateKey,
            TemplateHash: templateHash,
            Tokens: request.Tokens,
            TargetRoot: plan.TargetRoot,
            ExpectedItems: plan.Items,
            CreatedItems: executionResult.CreatedItems,
            MissingRequired: executionResult.MissingRequired,
            Warnings: executionResult.Warnings,
            Errors: executionResult.Errors,
            ManifestPath: manifestPath
        );
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
        var manifestFolderRelative = Path.Combine("00_Admin", ".mgf", "manifest");
        var manifestFolder = plan.Items.FirstOrDefault(
            item =>
                item.Kind == PlanItemKind.Folder
                && string.Equals(item.RelativePath, manifestFolderRelative, StringComparison.OrdinalIgnoreCase)
        );

        var manifestDir = manifestFolder?.AbsolutePath ?? Path.Combine(plan.TargetRoot, manifestFolderRelative);
        return Path.Combine(manifestDir, ManifestFileName);
    }

    private static ProvisioningManifest BuildManifest(
        ProvisioningRequest request,
        string templateKey,
        string templateHash,
        FolderPlan plan,
        ExecutionResult executionResult)
    {
        return new ProvisioningManifest
        {
            TemplateKey = templateKey,
            TemplateHash = templateHash,
            RunMode = request.Mode.ToString().ToLowerInvariant(),
            TimestampUtc = DateTimeOffset.UtcNow,
            Tokens = request.Tokens.ToDictionary(),
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
}

