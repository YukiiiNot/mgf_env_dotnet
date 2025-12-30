using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using MGF.Worker.Integrations.Dropbox;
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

    [Fact]
    public async Task ShareLinkPath_UsesStableFinal_FromFilesystemRoot()
    {
        var config = new ConfigurationBuilder().Build();
        var capture = new CaptureShareLinkClient();
        var deliverer = new ProjectDeliverer(config, shareLinkClient: capture, accessTokenProvider: new FakeTokenProvider());

        var dropboxRoot = @"C:\dropbox_root\99_TestRuns";
        var stablePath = Path.Combine(dropboxRoot, "04_Client_Deliveries", "Client", "MGF25-TEST_Project", "01_Deliverables", "Final");

        await deliverer.EnsureDropboxShareLinkAsync(
            dropboxRoot,
            stablePath,
            "04_Client_Deliveries",
            new ProjectDeliverer.DeliveryShareState(null, null, null, null),
            refreshRequested: false,
            testMode: false,
            cancellationToken: CancellationToken.None);

        Assert.NotNull(capture.LastPath);
        Assert.Contains("/01_Deliverables/Final", capture.LastPath, StringComparison.Ordinal);
        Assert.DoesNotContain("/Final/v", capture.LastPath, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatch(new Regex(@"/v\\d+$", RegexOptions.IgnoreCase), capture.LastPath ?? string.Empty);
    }

    [Fact]
    public async Task ShareLinkPath_UsesApiRootFolderStableFinal()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Integrations:Dropbox:UseApiRootFolder"] = "true",
                ["Integrations:Dropbox:ApiRootFolder"] = "MGFILMS.DELIVERIES"
            })
            .Build();

        var capture = new CaptureShareLinkClient();
        var deliverer = new ProjectDeliverer(config, shareLinkClient: capture, accessTokenProvider: new FakeTokenProvider());

        var dropboxRoot = @"C:\dropbox_root\99_TestRuns";
        var stablePath = Path.Combine(dropboxRoot, "04_Client_Deliveries", "Client", "MGF25-TEST_Project", "01_Deliverables", "Final");

        await deliverer.EnsureDropboxShareLinkAsync(
            dropboxRoot,
            stablePath,
            "04_Client_Deliveries",
            new ProjectDeliverer.DeliveryShareState(null, null, null, null),
            refreshRequested: false,
            testMode: false,
            cancellationToken: CancellationToken.None);

        Assert.NotNull(capture.LastPath);
        Assert.Equal(
            "/MGFILMS.DELIVERIES/04_Client_Deliveries/Client/MGF25-TEST_Project/01_Deliverables/Final",
            capture.LastPath);
        Assert.DoesNotContain("/Final/v", capture.LastPath, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatch(new Regex(@"/v\\d+$", RegexOptions.IgnoreCase), capture.LastPath ?? string.Empty);
    }

    private sealed class FakeTokenProvider : IDropboxAccessTokenProvider
    {
        public Task<DropboxAccessTokenResult> GetAccessTokenAsync(CancellationToken cancellationToken)
            => Task.FromResult(new DropboxAccessTokenResult("token", "test", "test", null));
    }

    private sealed class CaptureShareLinkClient : IDropboxShareLinkClient
    {
        public string? LastPath { get; private set; }

        public Task ValidateAccessTokenAsync(string accessToken, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<DropboxShareLinkResult> GetOrCreateSharedLinkAsync(
            string accessToken,
            string dropboxPath,
            CancellationToken cancellationToken)
        {
            LastPath = dropboxPath;
            return Task.FromResult(new DropboxShareLinkResult("https://dropbox.test/share", "id:123", IsNew: false));
        }
    }
}
