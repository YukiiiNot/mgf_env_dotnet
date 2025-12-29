using System.Text.Json.Nodes;
using MGF.Tools.Provisioner;
using MGF.Worker.Integrations.Email;
using MGF.Worker.ProjectDelivery;

namespace MGF.Worker.Tests;

public sealed class ProjectDeliveryEmailTests
{
    [Fact]
    public void BuildDeliveryEmailBody_IncludesLinkVersionAndFiles()
    {
        var files = new[]
        {
            new DeliveryFileSummary("final.mp4", 10, DateTimeOffset.UtcNow),
            new DeliveryFileSummary("notes.pdf", 20, DateTimeOffset.UtcNow)
        };

        var body = ProjectDeliverer.BuildDeliveryEmailBody(
            "https://dropbox.test/final",
            "v2",
            new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero),
            files);

        Assert.Contains("https://dropbox.test/final", body, StringComparison.Ordinal);
        Assert.Contains("v2", body, StringComparison.Ordinal);
        Assert.Contains("final.mp4", body, StringComparison.Ordinal);
        Assert.Contains("notes.pdf", body, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildDeliveryEmailBodyHtml_IncludesHeadlineProjectAndLink()
    {
        var tokens = ProvisioningTokens.Create("MGF25-TEST", "Sample Project", "Client", new[] { "TE" });
        var files = Enumerable.Range(1, 55)
            .Select(index => new DeliveryFileSummary($"file_{index}.mp4", 10, DateTimeOffset.UtcNow))
            .ToArray();

        var html = ProjectDeliverer.BuildDeliveryEmailBodyHtml(
            "https://dropbox.test/final",
            "v1",
            new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero),
            files,
            tokens,
            logoUrl: null);

        Assert.Contains("Your deliverables are ready", html, StringComparison.Ordinal);
        Assert.Contains("MGF25-TEST", html, StringComparison.Ordinal);
        Assert.Contains("Sample Project", html, StringComparison.Ordinal);
        Assert.Contains("https://dropbox.test/final", html, StringComparison.Ordinal);
        Assert.Contains("Current delivery version: v1", html, StringComparison.Ordinal);
        Assert.Contains("Showing 50 of 55 files", html, StringComparison.Ordinal);
    }

    [Fact]
    public void UpdateDeliveryCurrent_WritesLastEmail()
    {
        var delivery = new JsonObject();
        var email = new DeliveryEmailResult(
            Status: "sent",
            Provider: "smtp",
            FromAddress: "deliveries@mgfilms.pro",
            To: new[] { "client@example.com" },
            Subject: "Your deliverables are ready \u2014 MGF25-TEST Sample",
            SentAtUtc: DateTimeOffset.UtcNow,
            ProviderMessageId: "msg-1",
            Error: null,
            TemplateVersion: "v1-html",
            ReplyTo: "info@mgfilms.pro");

        var run = new ProjectDeliveryRunResult(
            JobId: "job_test",
            ProjectId: "prj_test",
            EditorInitials: new[] { "TE" },
            StartedAtUtc: DateTimeOffset.UtcNow,
            TestMode: false,
            AllowTestCleanup: false,
            AllowNonReal: false,
            Force: false,
            SourcePath: null,
            DestinationPath: @"C:\dropbox\Final",
            ApiStablePath: null,
            ApiVersionPath: null,
            VersionLabel: "v1",
            RetentionUntilUtc: DateTimeOffset.UtcNow.AddMonths(3),
            Files: Array.Empty<DeliveryFileSummary>(),
            Domains: Array.Empty<ProjectDeliveryDomainResult>(),
            HasErrors: false,
            LastError: null,
            ShareStatus: "created",
            ShareUrl: "https://dropbox.test/final",
            ShareId: "id-1",
            ShareError: null,
            Email: email
        );

        ProjectDeliverer.UpdateDeliveryCurrent(delivery, run);

        var current = delivery["current"] as JsonObject;
        Assert.NotNull(current);
        var lastEmail = current?["lastEmail"] as JsonObject;
        Assert.NotNull(lastEmail);
        Assert.Equal("sent", lastEmail?["status"]?.GetValue<string>());
    }

    [Fact]
    public void ApplyLastEmail_PreservesCurrentVersion()
    {
        var current = new JsonObject
        {
            ["currentVersion"] = "v1",
            ["stablePath"] = @"C:\dropbox\Final"
        };

        var email = new DeliveryEmailResult(
            Status: "failed",
            Provider: "smtp",
            FromAddress: "deliveries@mgfilms.pro",
            To: new[] { "client@example.com" },
            Subject: "Your deliverables are ready \u2014 MGF25-TEST Sample",
            SentAtUtc: null,
            ProviderMessageId: null,
            Error: "SMTP disabled",
            TemplateVersion: "v1-html",
            ReplyTo: null);

        ProjectDeliverer.ApplyLastEmail(current, email);

        Assert.Equal("v1", current["currentVersion"]?.GetValue<string>());
        var lastEmail = current["lastEmail"] as JsonObject;
        Assert.NotNull(lastEmail);
        Assert.Equal("failed", lastEmail?["status"]?.GetValue<string>());
    }
}
