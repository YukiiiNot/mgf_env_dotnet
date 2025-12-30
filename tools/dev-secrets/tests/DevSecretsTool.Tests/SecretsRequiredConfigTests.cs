using MGF.Tools.DevSecrets;

public class SecretsRequiredConfigTests
{
    [Fact]
    public void Load_ThrowsWhenProjectsMissing()
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, """{"projects": []}""");

        var ex = Assert.Throws<InvalidOperationException>(() => SecretsRequiredConfig.Load(path));
        Assert.Contains("missing required project entries", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExportFile_LoadAsync_ThrowsWhenProjectsMissing()
    {
        var path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, """{"exportedAtUtc":"2025-01-01T00:00:00Z","projects":[]}""");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => SecretsExportFile.LoadAsync(path, CancellationToken.None));
        Assert.Contains("missing projects", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
