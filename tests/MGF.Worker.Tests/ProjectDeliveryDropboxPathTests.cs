using MGF.Worker.ProjectDelivery;

namespace MGF.Worker.Tests;

public sealed class ProjectDeliveryDropboxPathTests
{
    [Fact]
    public void BuildDropboxApiPath_UsesApiRootAndDeliveryRelpath()
    {
        var apiRoot = "MGFILMS.DELIVERIES";
        var stablePath = @"C:\\Users\\dorme\\Dropbox\\MGFILMS.NET\\06_DevTest\\dropbox_root\\99_TestRuns\\04_Client_Deliveries\\Client\\MGF25-TEST_Client_SAMPLE\\01_Deliverables\\Final";
        var relpath = "04_Client_Deliveries";

        var result = ProjectDeliverer.BuildDropboxApiPath(apiRoot, stablePath, relpath);

        Assert.Equal("/MGFILMS.DELIVERIES/04_Client_Deliveries/Client/MGF25-TEST_Client_SAMPLE/01_Deliverables/Final", result);
    }

    [Fact]
    public void BuildDropboxApiPath_ThrowsWhenRelpathMissing()
    {
        var apiRoot = "MGFILMS.DELIVERIES";
        var stablePath = @"C:\\Users\\dorme\\Dropbox\\MGFILMS.NET\\06_DevTest\\dropbox_root\\99_TestRuns\\OtherRoot\\Client\\Project\\01_Deliverables\\Final";
        var relpath = "04_Client_Deliveries";

        var ex = Assert.Throws<InvalidOperationException>(() =>
            ProjectDeliverer.BuildDropboxApiPath(apiRoot, stablePath, relpath));

        Assert.Contains("outside Dropbox delivery root", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
