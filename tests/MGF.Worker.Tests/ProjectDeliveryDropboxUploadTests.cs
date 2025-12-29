using MGF.Worker.ProjectDelivery;

namespace MGF.Worker.Tests;

public sealed class ProjectDeliveryDropboxUploadTests
{
    [Fact]
    public void BuildDropboxUploadPaths_UsesVersionRoot()
    {
        var versionPath = "/MGFILMS.DELIVERIES/04_Client_Deliveries/Client/MGF25-TEST_Sample/01_Deliverables/Final/v1";
        var files = new[]
        {
            new ProjectDeliverer.DeliveryFile("C:\\src\\video.mp4", "video.mp4", 10, DateTimeOffset.UtcNow),
            new ProjectDeliverer.DeliveryFile("C:\\src\\sub\\clip.mp4", "sub\\clip.mp4", 11, DateTimeOffset.UtcNow)
        };

        var paths = ProjectDeliverer.BuildDropboxUploadPaths(versionPath, files);

        Assert.Contains("/MGFILMS.DELIVERIES/04_Client_Deliveries/Client/MGF25-TEST_Sample/01_Deliverables/Final/v1/video.mp4", paths);
        Assert.Contains("/MGFILMS.DELIVERIES/04_Client_Deliveries/Client/MGF25-TEST_Sample/01_Deliverables/Final/v1/sub/clip.mp4", paths);
    }

    [Fact]
    public void BuildDropboxUploadFolders_ReturnsParentDirectories()
    {
        var versionPath = "/MGFILMS.DELIVERIES/04_Client_Deliveries/Client/MGF25-TEST_Sample/01_Deliverables/Final/v1";
        var files = new[]
        {
            new ProjectDeliverer.DeliveryFile("C:\\src\\video.mp4", "video.mp4", 10, DateTimeOffset.UtcNow),
            new ProjectDeliverer.DeliveryFile("C:\\src\\sub\\clip.mp4", "sub\\clip.mp4", 11, DateTimeOffset.UtcNow)
        };

        var folders = ProjectDeliverer.BuildDropboxUploadFolders(versionPath, files);

        Assert.Contains("/MGFILMS.DELIVERIES/04_Client_Deliveries/Client/MGF25-TEST_Sample/01_Deliverables/Final/v1/sub", folders);
        Assert.DoesNotContain("/MGFILMS.DELIVERIES/04_Client_Deliveries/Client/MGF25-TEST_Sample/01_Deliverables/Final/v1", folders);
    }

    [Fact]
    public void DetermineVersionFromHistory_ReusesWhenFilesMatch()
    {
        var versionPath = "/MGFILMS.DELIVERIES/04_Client_Deliveries/Client/MGF25-TEST_Sample/01_Deliverables/Final";
        var timestamp = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var files = new[]
        {
            new ProjectDeliverer.DeliveryFile("C:\\src\\video.mp4", "video.mp4", 10, timestamp)
        };

        var history = new
        {
            CurrentVersion = "v1",
            LastFiles = new[] { new DeliveryFileSummary("video.mp4", 10, timestamp) }
        };

        var plan = ProjectDeliverer.DetermineVersionFromHistory(
            versionPath,
            new ProjectDeliverer.DeliveryHistory(history.CurrentVersion, history.LastFiles),
            files);

        Assert.False(plan.IsNewVersion);
        Assert.Equal("v1", plan.VersionLabel);
        Assert.Equal("/MGFILMS.DELIVERIES/04_Client_Deliveries/Client/MGF25-TEST_Sample/01_Deliverables/Final/v1", plan.VersionRoot);
    }
}
