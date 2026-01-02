using MGF.Contracts.Abstractions;

namespace MGF.FolderProvisioning;

public sealed class FolderPlanExecutor
{
    private readonly IFileStore fileStore;

    public FolderPlanExecutor(IFileStore fileStore)
    {
        this.fileStore = fileStore;
    }

    public async Task<ExecutionResult> ApplyAsync(
        FolderPlan plan,
        string seedsPath,
        ProvisioningTokens tokens,
        bool allowSeedOverwrite,
        CancellationToken cancellationToken)
    {
        var result = new ExecutionResult();

        if (!Directory.Exists(plan.TargetRoot))
        {
            await fileStore.CreateDirectoryAsync(plan.TargetRoot, cancellationToken);
        }

        foreach (var item in plan.Items.Where(i => i.Kind == PlanItemKind.Folder))
        {
            if (!Directory.Exists(item.AbsolutePath))
            {
                await fileStore.CreateDirectoryAsync(item.AbsolutePath, cancellationToken);
                result.CreatedItems.Add(item);
            }
        }

        foreach (var item in plan.Items.Where(i => i.Kind == PlanItemKind.File))
        {
            var exists = File.Exists(item.AbsolutePath);
            if (exists && !(allowSeedOverwrite && IsSeeded(item)))
            {
                continue;
            }

            if (!await EnsureParentDirectoryAsync(item.AbsolutePath, cancellationToken))
            {
                result.Errors.Add($"Missing parent directory for {item.RelativePath}.");
                continue;
            }

            if (!await TryWriteFileAsync(item, seedsPath, tokens, cancellationToken, allowSeedOverwrite, exists, result))
            {
                continue;
            }

            if (!exists || allowSeedOverwrite)
            {
                result.CreatedItems.Add(item);
            }
        }

        return result;
    }

    public Task<ExecutionResult> VerifyAsync(FolderPlan plan, CancellationToken cancellationToken)
    {
        var result = new ExecutionResult();

        foreach (var item in plan.Items)
        {
            if (item.Optional)
            {
                continue;
            }

            var exists = item.Kind == PlanItemKind.Folder
                ? Directory.Exists(item.AbsolutePath)
                : File.Exists(item.AbsolutePath);

            if (!exists)
            {
                result.MissingRequired.Add(item.RelativePath);
            }
        }

        return Task.FromResult(result);
    }

    private static bool IsSeeded(PlanItem item)
    {
        return !string.IsNullOrWhiteSpace(item.SourceRelpath) || !string.IsNullOrWhiteSpace(item.ContentTemplateKey);
    }

    private async Task<bool> EnsureParentDirectoryAsync(string filePath, CancellationToken cancellationToken)
    {
        var parent = Path.GetDirectoryName(filePath);
        if (string.IsNullOrWhiteSpace(parent))
        {
            return false;
        }

        if (!Directory.Exists(parent))
        {
            await fileStore.CreateDirectoryAsync(parent, cancellationToken);
        }

        return true;
    }

    private async Task<bool> TryWriteFileAsync(
        PlanItem item,
        string seedsPath,
        ProvisioningTokens tokens,
        CancellationToken cancellationToken,
        bool allowSeedOverwrite,
        bool exists,
        ExecutionResult result)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(item.SourceRelpath))
            {
                var sourcePath = Path.GetFullPath(Path.Combine(seedsPath, item.SourceRelpath));
                if (!File.Exists(sourcePath))
                {
                    var message = $"Seed file not found for {item.RelativePath}: {sourcePath}";
                    if (item.Optional)
                    {
                        result.Warnings.Add(message);
                        return false;
                    }

                    result.Errors.Add(message);
                    return false;
                }

                if (!exists || allowSeedOverwrite)
                {
                    await fileStore.CopyAsync(sourcePath, item.AbsolutePath, cancellationToken);
                }

                return true;
            }

            if (!string.IsNullOrWhiteSpace(item.ContentTemplateKey))
            {
                if (!ContentTemplates.TryGenerate(item.ContentTemplateKey, tokens, out var content))
                {
                    result.Errors.Add($"Unknown content template '{item.ContentTemplateKey}' for {item.RelativePath}.");
                    return false;
                }

                if (!exists || allowSeedOverwrite)
                {
                    await fileStore.WriteAllBytesAsync(item.AbsolutePath, content, cancellationToken);
                }

                return true;
            }

            if (!exists)
            {
                await fileStore.WriteAllBytesAsync(item.AbsolutePath, Array.Empty<byte>(), cancellationToken);
            }

            return true;
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Failed to write {item.RelativePath}: {ex.Message}");
            return false;
        }
    }
}

public sealed class ExecutionResult
{
    public List<PlanItem> CreatedItems { get; } = new();
    public List<string> MissingRequired { get; } = new();
    public List<string> Warnings { get; } = new();
    public List<string> Errors { get; } = new();
}



