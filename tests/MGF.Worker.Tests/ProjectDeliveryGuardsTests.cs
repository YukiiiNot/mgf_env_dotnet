using MGF.UseCases.Operations.ProjectDelivery.RunProjectDelivery;

public sealed class ProjectDeliveryGuardsTests
{
    [Fact]
    public void TryValidateStart_ReturnsTrueForReadyStatus()
    {
        var allowed = ProjectDeliveryGuards.TryValidateStart(
            ProjectDeliveryGuards.StatusReady,
            force: false,
            out var error,
            out var alreadyDelivering
        );

        Assert.True(allowed);
        Assert.Null(error);
        Assert.False(alreadyDelivering);
    }

    [Fact]
    public void TryValidateStart_AllowsRetryForDeliveryFailed()
    {
        var allowed = ProjectDeliveryGuards.TryValidateStart(
            ProjectDeliveryGuards.StatusDeliveryFailed,
            force: false,
            out var error,
            out var alreadyDelivering
        );

        Assert.True(allowed);
        Assert.Null(error);
        Assert.False(alreadyDelivering);
    }

    [Fact]
    public void TryValidateStart_AllowsRerunForDelivered()
    {
        var allowed = ProjectDeliveryGuards.TryValidateStart(
            ProjectDeliveryGuards.StatusDelivered,
            force: false,
            out var error,
            out var alreadyDelivering
        );

        Assert.True(allowed);
        Assert.Null(error);
        Assert.False(alreadyDelivering);
    }

    [Fact]
    public void TryValidateStart_BlocksWhenAlreadyDelivering()
    {
        var allowed = ProjectDeliveryGuards.TryValidateStart(
            ProjectDeliveryGuards.StatusDelivering,
            force: false,
            out var error,
            out var alreadyDelivering
        );

        Assert.False(allowed);
        Assert.True(alreadyDelivering);
        Assert.NotNull(error);
    }
}
