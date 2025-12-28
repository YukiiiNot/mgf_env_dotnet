using MGF.Worker.ProjectDelivery;

namespace MGF.Worker.Tests;

public sealed class ProjectDeliveryShareLinkTests
{
    [Fact]
    public void DetermineShareLinkDecision_ReusesWhenFresh()
    {
        var state = new ProjectDeliverer.DeliveryShareState(
            ShareUrl: "https://dropbox.test/share",
            ShareId: "id:123",
            ShareStatus: "created",
            LastVerifiedAtUtc: DateTimeOffset.UtcNow.AddDays(-1)
        );

        var decision = ProjectDeliverer.DetermineShareLinkDecision(
            state,
            refreshRequested: false,
            testMode: false,
            nowUtc: DateTimeOffset.UtcNow
        );

        Assert.False(decision.ShouldCreate);
        Assert.True(decision.ReuseExisting);
    }

    [Fact]
    public void DetermineShareLinkDecision_RefreshRequested_ForcesCreate()
    {
        var state = new ProjectDeliverer.DeliveryShareState(
            ShareUrl: "https://dropbox.test/share",
            ShareId: "id:123",
            ShareStatus: "created",
            LastVerifiedAtUtc: DateTimeOffset.UtcNow
        );

        var decision = ProjectDeliverer.DetermineShareLinkDecision(
            state,
            refreshRequested: true,
            testMode: false,
            nowUtc: DateTimeOffset.UtcNow
        );

        Assert.True(decision.ShouldCreate);
        Assert.False(decision.ReuseExisting);
    }

    [Fact]
    public void DetermineShareLinkDecision_FailedStatus_ForcesCreate()
    {
        var state = new ProjectDeliverer.DeliveryShareState(
            ShareUrl: "https://dropbox.test/share",
            ShareId: "id:123",
            ShareStatus: "failed",
            LastVerifiedAtUtc: DateTimeOffset.UtcNow.AddDays(-2)
        );

        var decision = ProjectDeliverer.DetermineShareLinkDecision(
            state,
            refreshRequested: false,
            testMode: false,
            nowUtc: DateTimeOffset.UtcNow
        );

        Assert.True(decision.ShouldCreate);
    }

    [Fact]
    public void DetermineShareLinkDecision_TtlExpired_AutomationOnly()
    {
        var state = new ProjectDeliverer.DeliveryShareState(
            ShareUrl: "https://dropbox.test/share",
            ShareId: "id:123",
            ShareStatus: "created",
            LastVerifiedAtUtc: DateTimeOffset.UtcNow.AddDays(-10)
        );

        var decision = ProjectDeliverer.DetermineShareLinkDecision(
            state,
            refreshRequested: false,
            testMode: false,
            nowUtc: DateTimeOffset.UtcNow
        );

        Assert.True(decision.ShouldCreate);

        var testModeDecision = ProjectDeliverer.DetermineShareLinkDecision(
            state,
            refreshRequested: false,
            testMode: true,
            nowUtc: DateTimeOffset.UtcNow
        );

        Assert.False(testModeDecision.ShouldCreate);
        Assert.True(testModeDecision.ReuseExisting);
    }

    [Fact]
    public void IsStableSharePath_RejectsVersionFolder()
    {
        var stable = ProjectDeliverer.IsStableSharePath(@"C:\dropbox\Final");
        var versioned = ProjectDeliverer.IsStableSharePath(@"C:\dropbox\Final\v2");

        Assert.True(stable);
        Assert.False(versioned);
    }
}
