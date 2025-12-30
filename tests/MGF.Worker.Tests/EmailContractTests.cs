using Microsoft.Extensions.Configuration;
using MGF.Worker.Email.Models;
using MGF.Worker.Email.Registry;
using MGF.Worker.Email.Sending;
using System.Net.Mime;

namespace MGF.Worker.Tests;

public sealed class EmailContractTests
{
    [Fact]
    public void BuildMessage_IncludesTextAndHtmlBodies()
    {
        var request = new DeliveryEmailRequest(
            FromAddress: "deliveries@mgfilms.pro",
            FromName: "MG Films",
            To: new[] { "client@example.com" },
            Subject: "Test",
            BodyText: "Text body",
            HtmlBody: "<p>Html body</p>",
            TemplateVersion: "v1-html",
            ReplyTo: null);

        var message = SmtpEmailSender.BuildMessage(request);

        Assert.Contains(message.AlternateViews, view => view.ContentType.MediaType == MediaTypeNames.Text.Plain);
        Assert.Contains(message.AlternateViews, view => view.ContentType.MediaType == MediaTypeNames.Text.Html);
    }

    [Fact]
    public async Task SendAsync_RejectsFromAddressOutsideAllowlist()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Integrations:Email:Enabled"] = "true",
                ["Integrations:Email:Smtp:Host"] = ""
            })
            .Build();

        var sender = new SmtpEmailSender(config);
        var request = new DeliveryEmailRequest(
            FromAddress: "noreply@example.com",
            FromName: "Bad Sender",
            To: new[] { "client@example.com" },
            Subject: "Test",
            BodyText: "Body",
            HtmlBody: "<p>Body</p>",
            TemplateVersion: "v1-html",
            ReplyTo: null);

        var result = await sender.SendAsync(request, CancellationToken.None);

        Assert.Equal("failed", result.Status);
        Assert.Contains("deliveries@mgfilms.pro", result.Error ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Allowlist_IncludesBillingAddress()
    {
        var config = new ConfigurationBuilder().Build();
        var profile = EmailProfileResolver.Resolve(config, EmailProfiles.Deliveries);

        Assert.True(EmailProfileResolver.IsAllowedFrom(profile, "billing@mgfilms.pro"));
    }
}
