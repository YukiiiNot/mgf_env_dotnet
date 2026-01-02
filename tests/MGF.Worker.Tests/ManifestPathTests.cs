using MGF.FolderProvisioning;
using MGF.Worker.ProjectArchive;
using MGF.Worker.ProjectBootstrap;
using MGF.Worker.ProjectDelivery;

namespace MGF.Worker.Tests;

public sealed class ManifestPathTests
{
    [Fact]
    public void BootstrapManifestPath_UsesMgfManifestFolder()
    {
        var root = Path.Combine(Path.GetTempPath(), $"mgf_manifest_{Guid.NewGuid():N}");
        var relative = Path.Combine("00_Admin", ".mgf", "manifest");

        var plan = new FolderPlan(
            root,
            new[]
            {
                new PlanItem(
                    PlanItemKind.Folder,
                    relative,
                    Path.Combine(root, relative),
                    Optional: false,
                    SourceRelpath: null,
                    ContentTemplateKey: null)
            }
        );

        var manifestPath = ProjectBootstrapper.ResolveManifestPath(plan);
        var expectedSuffix = Path.Combine("00_Admin", ".mgf", "manifest", "folder_manifest.json");

        Assert.EndsWith(expectedSuffix, manifestPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ArchiveManifestPath_UsesMgfManifestFolder()
    {
        var root = Path.Combine(Path.GetTempPath(), $"mgf_manifest_{Guid.NewGuid():N}");
        var relative = Path.Combine("00_Admin", ".mgf", "manifest");

        var plan = new FolderPlan(
            root,
            new[]
            {
                new PlanItem(
                    PlanItemKind.Folder,
                    relative,
                    Path.Combine(root, relative),
                    Optional: false,
                    SourceRelpath: null,
                    ContentTemplateKey: null)
            }
        );

        var manifestPath = ProjectArchiver.ResolveManifestPath(plan);
        var expectedSuffix = Path.Combine("00_Admin", ".mgf", "manifest", "folder_manifest.json");

        Assert.EndsWith(expectedSuffix, manifestPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DeliveryManifestPath_UsesMgfManifestFolder()
    {
        var root = Path.Combine(Path.GetTempPath(), $"mgf_delivery_{Guid.NewGuid():N}");
        var expectedSuffix = Path.Combine("00_Admin", ".mgf", "manifest", "delivery_manifest.json");

        var manifestPath = ProjectDeliverer.ResolveDeliveryManifestPath(root);

        Assert.EndsWith(expectedSuffix, manifestPath, StringComparison.OrdinalIgnoreCase);
    }
}


