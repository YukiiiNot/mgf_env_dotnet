namespace MGF.Worker.Tests;

using System.Text.Json;
using MGF.Data.Stores.Delivery;

public sealed class DeliveryMetadataTests
{
    private static readonly JsonSerializerOptions CamelCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public void AppendDeliveryEmail_WritesLastEmail()
    {
        var metadata = JsonDocument.Parse("{}").RootElement;
        var emailResult = JsonSerializer.SerializeToElement(
            new
            {
                Status = "sent",
                Provider = "smtp",
                FromAddress = "deliveries@mgfilms.pro",
                To = new[] { "test@example.com" },
                Subject = "Delivery ready",
                SentAtUtc = new DateTimeOffset(2024, 2, 1, 12, 0, 0, TimeSpan.Zero),
                ProviderMessageId = "msg_123",
                Error = (string?)null,
                TemplateVersion = "v1-html",
                ReplyTo = (string?)null
            },
            CamelCaseOptions);

        var updatedJson = DeliveryMetadataUpdater.AppendDeliveryEmail(metadata, emailResult);

        using var updatedDoc = JsonDocument.Parse(updatedJson);
        var lastEmail = updatedDoc.RootElement
            .GetProperty("delivery")
            .GetProperty("current")
            .GetProperty("lastEmail");

        Assert.Equal("sent", lastEmail.GetProperty("status").GetString());
        Assert.Equal("Delivery ready", lastEmail.GetProperty("subject").GetString());
    }

    [Fact]
    public void AppendDeliveryRun_AppendsRunAndUpdatesCurrent()
    {
        var metadata = JsonDocument.Parse("{\"delivery\":{\"current\":{\"stableShareUrl\":\"https://existing\"}}}").RootElement;
        var runResult = JsonSerializer.SerializeToElement(
            new
            {
                JobId = "job_123",
                ProjectId = "proj_456",
                EditorInitials = new[] { "AB" },
                StartedAtUtc = new DateTimeOffset(2024, 2, 1, 12, 0, 0, TimeSpan.Zero),
                TestMode = false,
                AllowTestCleanup = false,
                AllowNonReal = false,
                Force = false,
                SourcePath = "source",
                DestinationPath = "dest",
                ApiStablePath = "api/stable",
                ApiVersionPath = "api/v2",
                VersionLabel = "v2",
                RetentionUntilUtc = new DateTimeOffset(2024, 5, 1, 12, 0, 0, TimeSpan.Zero),
                Files = Array.Empty<object>(),
                Domains = Array.Empty<object>(),
                HasErrors = false,
                LastError = (string?)null,
                ShareStatus = "pending",
                ShareUrl = "https://share",
                ShareId = "share_123",
                ShareError = (string?)null,
                Email = (object?)null
            },
            CamelCaseOptions);

        var updatedJson = DeliveryMetadataUpdater.AppendDeliveryRun(metadata, runResult);

        using var updatedDoc = JsonDocument.Parse(updatedJson);
        var delivery = updatedDoc.RootElement.GetProperty("delivery");

        Assert.Equal(1, delivery.GetProperty("runs").GetArrayLength());

        var current = delivery.GetProperty("current");
        Assert.Equal("dest", current.GetProperty("stablePath").GetString());
        Assert.Equal("https://share", current.GetProperty("stableShareUrl").GetString());
        Assert.Equal("v2", current.GetProperty("currentVersion").GetString());
    }

    [Fact]
    public void BuildUpdateMetadataCommand_UsesExpectedArguments()
    {
        var command = DeliverySql.BuildUpdateMetadataCommand("proj_1", "{\"delivery\":{}}");

        Assert.Contains("UPDATE public.projects", command.Format);
        Assert.Contains("metadata = ", command.Format);

        var args = command.GetArguments();
        Assert.Equal("{\"delivery\":{}}", args[0]);
        Assert.Equal("proj_1", args[1]);
    }
}
