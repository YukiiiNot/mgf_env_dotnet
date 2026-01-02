using MGF.FolderProvisioning;
using MGF.Email.Composition;
using MGF.Email.Models;

namespace MGF.Worker.Tests;

public sealed class EmailTemplateRendererTests
{
    [Fact]
    public void RenderHtml_EscapesProjectAndFileNames()
    {
        var renderer = EmailTemplateRenderer.CreateDefault();
        var tokens = ProvisioningTokens.Create("MGF25-TEST", "<script>alert(1)</script>", "Client", new[] { "TE" });
        var files = new[]
        {
            new DeliveryEmailFileSummary("<b>final.mp4</b>", 10, DateTimeOffset.UtcNow)
        };

        var context = new DeliveryReadyEmailContext(
            tokens,
            "https://dropbox.test/final?x=1&y=2",
            "v1",
            new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero),
            files,
            new[] { "client@example.com" },
            "info@mgfilms.pro",
            null,
            "MG Films");

        var html = renderer.RenderHtml("delivery_ready.html", context);

        Assert.DoesNotContain("<script>", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("&lt;b&gt;final.mp4&lt;/b&gt;", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("https://dropbox.test/final?x=1&amp;y=2", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RenderHtml_LogoOptional_DoesNotRenderImageWhenMissing()
    {
        var renderer = EmailTemplateRenderer.CreateDefault();
        var tokens = ProvisioningTokens.Create("MGF25-TEST", "Sample Project", "Client", new[] { "TE" });
        var context = new DeliveryReadyEmailContext(
            tokens,
            "https://dropbox.test/final",
            "v1",
            new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero),
            Array.Empty<DeliveryEmailFileSummary>(),
            new[] { "client@example.com" },
            "info@mgfilms.pro",
            null,
            "MG Films");

        var html = renderer.RenderHtml("delivery_ready.html", context);

        Assert.DoesNotContain("<img", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Deliverables", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Ready", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RenderHtml_CapsFileListAndShowsCountNote()
    {
        var renderer = EmailTemplateRenderer.CreateDefault();
        var tokens = ProvisioningTokens.Create("MGF25-TEST", "Sample Project", "Client", new[] { "TE" });
        var files = Enumerable.Range(1, 55)
            .Select(index => new DeliveryEmailFileSummary($"file_{index}.mp4", 10, DateTimeOffset.UtcNow))
            .ToArray();

        var context = new DeliveryReadyEmailContext(
            tokens,
            "https://dropbox.test/final",
            "v1",
            new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero),
            files,
            new[] { "client@example.com" },
            "info@mgfilms.pro",
            null,
            "MG Films");

        var html = renderer.RenderHtml("delivery_ready.html", context);

        Assert.Contains("Showing 50 of 55 files.", html, StringComparison.OrdinalIgnoreCase);
    }
}


