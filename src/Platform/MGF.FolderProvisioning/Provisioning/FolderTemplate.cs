namespace MGF.FolderProvisioning;

public sealed class FolderTemplate
{
    public string TemplateKey { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public NamingRules? NamingRules { get; set; }
    public FolderNode Root { get; set; } = new();
}

public sealed class NamingRules
{
    public ProjectFileRules? ProjectFiles { get; set; }
    public ExportRules? Exports { get; set; }
}

public sealed class ProjectFileRules
{
    public string? Master { get; set; }
    public string? Editor { get; set; }
    public string? Notes { get; set; }
}

public sealed class ExportRules
{
    public string? Versioned { get; set; }
    public string? Notes { get; set; }
}

public sealed class FolderNode
{
    public string Name { get; set; } = string.Empty;
    public FolderNodeKind? Kind { get; set; }
    public List<FolderNode>? Children { get; set; }
    public bool Optional { get; set; }
    public string? Notes { get; set; }
    public string? ContentTemplateKey { get; set; }
    public string? SourceRelpath { get; set; }
}

public enum FolderNodeKind
{
    Folder,
    File
}


