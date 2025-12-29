using Microsoft.Extensions.Configuration;
using MGF.Worker.Integrations.Email;

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
            To: new[] { "client@example.com" },
            Subject: "Test",
            BodyText: "Body",
            ReplyTo: null);

        var result = await sender.SendAsync(request, CancellationToken.None);

        Assert.Equal("failed", result.Status);
        Assert.Contains("deliveries@mgfilms.pro", result.Error ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }
}
