using MGF.FolderProvisioning;
using MGF.Worker.ProjectDelivery;

namespace MGF.Worker.Tests;

public sealed class ProjectDeliveryManifestTests
{
    [Fact]
    public void BuildManifest_IncludesStableAndVersionPaths()
    {
        var tokens = ProvisioningTokens.Create("MGF25-TEST", "Sample", "Client", new[] { "TE" });
        var stablePath = Path.Combine("04_Client_Deliveries", "Client", "MGF25-TEST_Sample", "01_Deliverables", "Final");
        var versionPath = Path.Combine(stablePath, "v1");

        var manifest = ProjectDeliverer.BuildDeliveryManifest(
            "prj_test",
            tokens,
            @"C:\lucidlink\Final_Masters",
            stablePath,
            versionPath,
            "v1",
            DateTimeOffset.UtcNow.AddMonths(3),
            Array.Empty<ProjectDeliverer.DeliveryFile>(),
            "https://dropbox.test/share",
            apiStablePath: "/MGFILMS.DELIVERIES/04_Client_Deliveries/Client/MGF25-TEST_Sample/01_Deliverables/Final",
            apiVersionPath: "/MGFILMS.DELIVERIES/04_Client_Deliveries/Client/MGF25-TEST_Sample/01_Deliverables/Final/v1");

        Assert.Equal(2, manifest.SchemaVersion);
        Assert.Equal(stablePath, manifest.DestinationPath);
        Assert.Equal(stablePath, manifest.StablePath);
        Assert.Equal(versionPath, manifest.VersionPath);
        Assert.Equal("/MGFILMS.DELIVERIES/04_Client_Deliveries/Client/MGF25-TEST_Sample/01_Deliverables/Final", manifest.ApiStablePath);
        Assert.Equal("/MGFILMS.DELIVERIES/04_Client_Deliveries/Client/MGF25-TEST_Sample/01_Deliverables/Final/v1", manifest.ApiVersionPath);
        Assert.Equal("v1", manifest.VersionLabel);
        Assert.Equal("v1", manifest.CurrentVersion);
        Assert.Equal("https://dropbox.test/share", manifest.StableShareUrl);
    }
}


