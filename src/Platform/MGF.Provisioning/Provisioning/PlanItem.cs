namespace MGF.Provisioning;

public enum PlanItemKind
{
    Folder,
    File
}

public sealed record PlanItem(
    PlanItemKind Kind,
    string RelativePath,
    string AbsolutePath,
    bool Optional,
    string? SourceRelpath,
    string? ContentTemplateKey
);

public sealed record FolderPlan(
    string TargetRoot,
    IReadOnlyList<PlanItem> Items
);

