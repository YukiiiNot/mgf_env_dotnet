using System.Text.RegularExpressions;

namespace MGF.Tools.Provisioner;

public sealed class FolderTemplatePlanner
{
    private static readonly Regex TopLevelPrefixRegex = new("^\\d{2}_.+", RegexOptions.Compiled);

    public FolderPlan Plan(FolderTemplate template, ProvisioningTokens tokens, string basePath)
    {
        if (template.Root is null)
        {
            throw new InvalidOperationException("Template root is required.");
        }

        if (template.Root.Children is null || template.Root.Children.Count == 0)
        {
            throw new InvalidOperationException("Template root must contain at least one child.");
        }

        var expandedRootName = TokenExpander.ExpandRootName(template.Root.Name, tokens);
        PathSafety.EnsureSafeSegment(expandedRootName, "root name");

        var fullBasePath = Path.GetFullPath(basePath);
        var targetRoot = Path.GetFullPath(Path.Combine(fullBasePath, expandedRootName));

        var items = new List<PlanItem>();

        foreach (var child in template.Root.Children)
        {
            ExpandNode(child, parentRelativePath: string.Empty, topLevelName: null, parentOptional: false, tokens, items);
        }

        var ordered = items
            .OrderBy(i => i.Kind == PlanItemKind.Folder ? 0 : 1)
            .ThenBy(i => i.RelativePath, StringComparer.Ordinal)
            .ToList();

        EnsureNoDuplicates(ordered);

        var withAbsolute = ordered
            .Select(item => item with
            {
                AbsolutePath = Path.GetFullPath(Path.Combine(targetRoot, item.RelativePath))
            })
            .ToList();

        return new FolderPlan(targetRoot, withAbsolute);
    }

    private static void ExpandNode(
        FolderNode node,
        string parentRelativePath,
        string? topLevelName,
        bool parentOptional,
        ProvisioningTokens tokens,
        List<PlanItem> items)
    {
        var effectiveOptional = parentOptional || node.Optional;
        var expandedNames = TokenExpander.ExpandNodeName(node.Name, tokens, effectiveOptional);

        foreach (var expandedName in expandedNames)
        {
            var currentTopLevelName = topLevelName ?? expandedName;
            if (topLevelName is null)
            {
                if (!TopLevelPrefixRegex.IsMatch(expandedName))
                {
                    throw new InvalidOperationException($"Top-level folder '{expandedName}' must match ^\\d{{2}}_.+.");
                }
            }

            PathSafety.EnsureSafeSegment(expandedName, "node name");

            if (string.Equals(expandedName, ".mgf", StringComparison.Ordinal) && !string.Equals(currentTopLevelName, "00_Admin", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(".mgf folder is only allowed under 00_Admin.");
            }

            var relativePath = string.IsNullOrEmpty(parentRelativePath)
                ? expandedName
                : Path.Combine(parentRelativePath, expandedName);

            var kind = node.Kind ?? FolderNodeKind.Folder;
            if (kind == FolderNodeKind.File && node.Children is { Count: > 0 })
            {
                throw new InvalidOperationException($"File node '{relativePath}' cannot have children.");
            }

            if (kind == FolderNodeKind.Folder && (!string.IsNullOrWhiteSpace(node.ContentTemplateKey) || !string.IsNullOrWhiteSpace(node.SourceRelpath)))
            {
                throw new InvalidOperationException($"Folder node '{relativePath}' cannot declare content templates or source files.");
            }

            if (!string.IsNullOrWhiteSpace(node.SourceRelpath))
            {
                PathSafety.EnsureSafeRelativePath(node.SourceRelpath, $"SourceRelpath for {relativePath}");
            }

            items.Add(
                new PlanItem(
                    Kind: kind == FolderNodeKind.Folder ? PlanItemKind.Folder : PlanItemKind.File,
                    RelativePath: relativePath,
                    AbsolutePath: string.Empty,
                    Optional: effectiveOptional,
                    SourceRelpath: node.SourceRelpath,
                    ContentTemplateKey: node.ContentTemplateKey
                )
            );

            if (kind == FolderNodeKind.Folder && node.Children is { Count: > 0 })
            {
                foreach (var child in node.Children)
                {
                    ExpandNode(child, relativePath, currentTopLevelName, effectiveOptional, tokens, items);
                }
            }
        }
    }

    private static void EnsureNoDuplicates(IEnumerable<PlanItem> items)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            if (!seen.Add(item.RelativePath))
            {
                throw new InvalidOperationException($"Duplicate planned path detected: {item.RelativePath}");
            }
        }
    }
}
