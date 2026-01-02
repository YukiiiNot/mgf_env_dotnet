using System.Text.Json;
using System.Text.Json.Nodes;
using MGF.Contracts.Abstractions.Email;
using MGF.Contracts.Abstractions.ProjectDelivery;
using MGF.Data.Stores.Delivery;
using MGF.Email.Composition;
using MGF.Email.Models;
using MGF.FolderProvisioning;
using MGF.Worker.Adapters.Storage.ProjectDelivery;

namespace MGF.Worker.Tests;

public sealed class ProjectDeliveryEmailTests
{
    private static readonly JsonSerializerOptions CamelCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public void BuildDeliveryEmailBody_IncludesLinkVersionAndFiles()
    {
        var files = new[]
        {
            new DeliveryFileSummary("final.mp4", 10, DateTimeOffset.UtcNow),
            new DeliveryFileSummary("notes.pdf", 20, DateTimeOffset.UtcNow)
        };

        var body = ProjectDeliveryExecutor.BuildDeliveryEmailBody(
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

        var html = ProjectDeliveryExecutor.BuildDeliveryEmailBodyHtml(
            "https://dropbox.test/final",
            "v1",
            new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero),
            files,
            tokens,
            logoUrl: null);

        Assert.Contains("Deliverables", html, StringComparison.Ordinal);
        Assert.Contains("Ready", html, StringComparison.Ordinal);
        Assert.Contains("MGF25-TEST", html, StringComparison.Ordinal);
        Assert.Contains("Sample Project", html, StringComparison.Ordinal);
        Assert.Contains("https://dropbox.test/final", html, StringComparison.Ordinal);
        Assert.Contains("Version:", html, StringComparison.Ordinal);
        Assert.Contains("Access until:", html, StringComparison.Ordinal);
        Assert.Contains("Showing 50 of 55 files", html, StringComparison.Ordinal);
    }

    [Fact]
    public void DeliveryReadyComposer_BuildsTextAndHtml()
    {
        var tokens = ProvisioningTokens.Create("MGF25-TEST", "Sample Project", "Client", new[] { "TE" });
        var files = new[]
        {
            new DeliveryEmailFileSummary("final.mp4", 10, DateTimeOffset.UtcNow),
            new DeliveryEmailFileSummary("notes.pdf", 20, DateTimeOffset.UtcNow)
        };

        var context = new DeliveryReadyEmailContext(
            tokens,
            "https://dropbox.test/final",
            "v2",
            new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero),
            files,
            new[] { "client@example.com" },
            "info@mgfilms.pro",
            "https://logo.test/logo.png",
            "MG Films");
        var composer = new DeliveryReadyEmailComposer();
        var composed = composer.Build(context);

        Assert.Contains("https://dropbox.test/final", composed.BodyText, StringComparison.Ordinal);
        Assert.Contains("v2", composed.BodyText, StringComparison.Ordinal);
        Assert.Contains("final.mp4", composed.BodyText, StringComparison.Ordinal);
        Assert.Contains("https://dropbox.test/final", composed.HtmlBody ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains("Version:", composed.HtmlBody ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public void UpdateDeliveryCurrent_WritesLastEmail()
    {
        var metadata = JsonDocument.Parse("{}").RootElement;
        var email = new EmailSendResult(
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

        var runResultJson = JsonSerializer.SerializeToElement(run, CamelCaseOptions);
        var updatedJson = DeliveryMetadataUpdater.AppendDeliveryRun(metadata, runResultJson);

        using var updatedDoc = JsonDocument.Parse(updatedJson);
        var lastEmail = updatedDoc.RootElement
            .GetProperty("delivery")
            .GetProperty("current")
            .GetProperty("lastEmail");

        Assert.Equal("sent", lastEmail.GetProperty("status").GetString());
    }

    [Fact]
    public void ApplyLastEmail_PreservesCurrentVersion()
    {
        var metadataNode = new JsonObject
        {
            ["delivery"] = new JsonObject
            {
                ["current"] = new JsonObject
                {
                    ["currentVersion"] = "v1",
                    ["stablePath"] = @"C:\dropbox\Final"
                }
            }
        };
        var metadata = JsonDocument.Parse(metadataNode.ToJsonString()).RootElement;

        var email = new EmailSendResult(
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

        var emailResultJson = JsonSerializer.SerializeToElement(email, CamelCaseOptions);
        var updatedJson = DeliveryMetadataUpdater.AppendDeliveryEmail(metadata, emailResultJson);

        using var updatedDoc = JsonDocument.Parse(updatedJson);
        var current = updatedDoc.RootElement.GetProperty("delivery").GetProperty("current");

        Assert.Equal("v1", current.GetProperty("currentVersion").GetString());
        Assert.Equal("failed", current.GetProperty("lastEmail").GetProperty("status").GetString());
    }

    // no extra helpers
}


