using System.Text.Json;
using MGF.FolderProvisioning;
using MGF.Email.Composition;
using MGF.Email.Models;

namespace MGF.Worker.Tests;

public sealed class EmailTemplateSnapshotTests
{
    [Theory]
    [InlineData("basic")]
    [InlineData("large_files")]
    [InlineData("no_logo")]
    [InlineData("long_url")]
    public void DeliveryReady_HtmlMatchesSnapshot(string fixtureName)
    {
        var context = BuildContextFromFixture(fixtureName);
        var renderer = EmailTemplateRenderer.CreateDefault();
        var html = renderer.RenderHtml("delivery_ready.html", context);

        var snapshotPath = GetSnapshotPath($"delivery_ready_{fixtureName}.html");
        var expected = Normalize(File.ReadAllText(snapshotPath));
        var actual = Normalize(html);

        Assert.Equal(expected, actual);
    }

    private static DeliveryReadyEmailContext BuildContextFromFixture(string fixtureName)
    {
        var fixturePath = GetFixturePath(fixtureName);
        var json = File.ReadAllText(fixturePath);
        var fixture = JsonSerializer.Deserialize<DeliveryPreviewFixture>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(fixture);

        var tokens = ProvisioningTokens.Create(
            fixture!.ProjectCode ?? "MGF25-TEST",
            fixture.ProjectName ?? "Delivery Preview",
            fixture.ClientName ?? "Client",
            Array.Empty<string>());

        var files = fixture.Files?.Select(file =>
            new DeliveryEmailFileSummary(
                file.RelativePath ?? "deliverable.mp4",
                file.SizeBytes ?? 0,
                file.LastWriteTimeUtc ?? DateTimeOffset.UtcNow))
            .ToArray() ?? Array.Empty<DeliveryEmailFileSummary>();

        var recipients = fixture.Recipients?.Where(email => !string.IsNullOrWhiteSpace(email)).ToArray()
            ?? new[] { "client@example.com" };

        var logoUrl = string.IsNullOrWhiteSpace(fixture.LogoUrl) ? null : fixture.LogoUrl;

        return new DeliveryReadyEmailContext(
            tokens,
            fixture.ShareUrl ?? "https://dropbox.test/final",
            fixture.VersionLabel ?? "v1",
            fixture.RetentionUntilUtc ?? DateTimeOffset.UtcNow.AddMonths(3),
            files,
            recipients,
            "info@mgfilms.pro",
            logoUrl,
            "MG Films");
    }

    private static string GetFixturePath(string fixtureName)
    {
        var baseDir = AppContext.BaseDirectory;
        var fixturePath = Path.Combine(baseDir, "Email", "Templates", "fixtures", $"{fixtureName}.json");
        if (!File.Exists(fixturePath))
        {
            throw new FileNotFoundException("Fixture not found.", fixturePath);
        }

        return fixturePath;
    }

    private static string GetSnapshotPath(string fileName)
    {
        var repoRoot = FindRepoRoot(AppContext.BaseDirectory)
            ?? throw new DirectoryNotFoundException("Repo root not found.");
        return Path.Combine(repoRoot, "tests", "MGF.Worker.Tests", "EmailSnapshots", fileName);
    }

    private static string? FindRepoRoot(string start)
    {
        var dir = new DirectoryInfo(start);
        while (dir is not null)
        {
            if (dir.EnumerateFiles("MGF.sln").Any())
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return null;
    }

    private static string Normalize(string value)
        => value.Replace("\r\n", "\n");

    private sealed record DeliveryPreviewFixture(
        string? ProjectCode,
        string? ProjectName,
        string? ClientName,
        string? ShareUrl,
        string? VersionLabel,
        DateTimeOffset? RetentionUntilUtc,
        DeliveryPreviewFixtureFile[]? Files,
        string[]? Recipients,
        string? LogoUrl);

    private sealed record DeliveryPreviewFixtureFile(
        string? RelativePath,
        long? SizeBytes,
        DateTimeOffset? LastWriteTimeUtc);
}


