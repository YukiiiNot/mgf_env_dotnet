using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using MGF.Contracts.Abstractions.Dropbox;
using MGF.Contracts.Abstractions.Email;
using MGF.Worker.Adapters.Storage.ProjectDelivery;

namespace MGF.Worker.Tests;

public sealed class ProjectDeliveryShareLinkTests
{
    [Fact]
    public void DetermineShareLinkDecision_ReusesWhenFresh()
    {
        var state = new ProjectDeliveryExecutor.DeliveryShareState(
            ShareUrl: "https://dropbox.test/share",
            ShareId: "id:123",
            ShareStatus: "created",
            LastVerifiedAtUtc: DateTimeOffset.UtcNow.AddDays(-1)
        );

        var decision = ProjectDeliveryExecutor.DetermineShareLinkDecision(
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
        var state = new ProjectDeliveryExecutor.DeliveryShareState(
            ShareUrl: "https://dropbox.test/share",
            ShareId: "id:123",
            ShareStatus: "created",
            LastVerifiedAtUtc: DateTimeOffset.UtcNow
        );

        var decision = ProjectDeliveryExecutor.DetermineShareLinkDecision(
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
        var state = new ProjectDeliveryExecutor.DeliveryShareState(
            ShareUrl: "https://dropbox.test/share",
            ShareId: "id:123",
            ShareStatus: "failed",
            LastVerifiedAtUtc: DateTimeOffset.UtcNow.AddDays(-2)
        );

        var decision = ProjectDeliveryExecutor.DetermineShareLinkDecision(
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
        var state = new ProjectDeliveryExecutor.DeliveryShareState(
            ShareUrl: "https://dropbox.test/share",
            ShareId: "id:123",
            ShareStatus: "created",
            LastVerifiedAtUtc: DateTimeOffset.UtcNow.AddDays(-10)
        );

        var decision = ProjectDeliveryExecutor.DetermineShareLinkDecision(
            state,
            refreshRequested: false,
            testMode: false,
            nowUtc: DateTimeOffset.UtcNow
        );

        Assert.True(decision.ShouldCreate);

        var testModeDecision = ProjectDeliveryExecutor.DetermineShareLinkDecision(
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
        var stable = ProjectDeliveryExecutor.IsStableSharePath(@"C:\dropbox\Final");
        var versioned = ProjectDeliveryExecutor.IsStableSharePath(@"C:\dropbox\Final\v2");

        Assert.True(stable);
        Assert.False(versioned);
    }

    [Fact]
    public async Task ShareLinkPath_UsesStableFinal_FromFilesystemRoot()
    {
        var config = new ConfigurationBuilder().Build();
        var capture = new CaptureShareLinkClient();
        var deliverer = new ProjectDeliveryExecutor(
            config,
            shareLinkClient: capture,
            accessTokenProvider: new FakeTokenProvider(),
            dropboxFilesClient: new FakeFilesClient(),
            emailSender: new FakeEmailSender());

        var dropboxRoot = @"C:\dropbox_root\99_TestRuns";
        var stablePath = Path.Combine(dropboxRoot, "04_Client_Deliveries", "Client", "MGF25-TEST_Project", "01_Deliverables", "Final");

        await deliverer.EnsureDropboxShareLinkAsync(
            dropboxRoot,
            stablePath,
            "04_Client_Deliveries",
            new ProjectDeliveryExecutor.DeliveryShareState(null, null, null, null),
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
        var deliverer = new ProjectDeliveryExecutor(
            config,
            shareLinkClient: capture,
            accessTokenProvider: new FakeTokenProvider(),
            dropboxFilesClient: new FakeFilesClient(),
            emailSender: new FakeEmailSender());

        var dropboxRoot = @"C:\dropbox_root\99_TestRuns";
        var stablePath = Path.Combine(dropboxRoot, "04_Client_Deliveries", "Client", "MGF25-TEST_Project", "01_Deliverables", "Final");

        await deliverer.EnsureDropboxShareLinkAsync(
            dropboxRoot,
            stablePath,
            "04_Client_Deliveries",
            new ProjectDeliveryExecutor.DeliveryShareState(null, null, null, null),
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

    private sealed class FakeFilesClient : IDropboxFilesClient
    {
        public Task EnsureFolderAsync(string accessToken, string dropboxPath, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task UploadFileAsync(string accessToken, string dropboxPath, string localFilePath, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task UploadBytesAsync(string accessToken, string dropboxPath, byte[] content, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class FakeEmailSender : IEmailSender
    {
        public Task<EmailSendResult> SendAsync(EmailMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new EmailSendResult(
                Status: "skipped",
                Provider: "email",
                FromAddress: request.FromAddress,
                To: request.To,
                Subject: request.Subject,
                SentAtUtc: null,
                ProviderMessageId: null,
                Error: "Fake sender",
                TemplateVersion: request.TemplateVersion,
                ReplyTo: request.ReplyTo));
        }
    }
}
