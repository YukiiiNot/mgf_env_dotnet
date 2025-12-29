using Microsoft.Extensions.Configuration;
using MGF.Worker.Integrations.Email;
using System.Net.Mime;

namespace MGF.Worker.Tests;

public sealed class SmtpEmailSenderTests
{
    [Fact]
    public async Task SendAsync_RejectsInvalidFromAddress()
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
            FromAddress: "someone@example.com",
            FromName: "Someone",
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
    public void BuildMessage_IncludesHtmlAlternateView()
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

        Assert.True(message.AlternateViews.Count >= 2);
        Assert.Contains(message.AlternateViews, view => view.ContentType.MediaType == MediaTypeNames.Text.Html);
        Assert.Contains(message.AlternateViews, view => view.ContentType.MediaType == MediaTypeNames.Text.Plain);
    }
}
