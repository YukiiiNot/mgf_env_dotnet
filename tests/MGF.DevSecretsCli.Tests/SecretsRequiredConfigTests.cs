using MGF.DevSecretsCli;

public class SecretsRequiredConfigTests
{
    [Fact]
    public void Load_ThrowsWhenRequiredKeysMissing()
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, """{"schemaVersion":2,"requiredKeys":[]}""");

        var ex = Assert.Throws<InvalidOperationException>(() => SecretsRequiredConfig.Load(path));
        Assert.Contains("missing required keys", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExportFile_LoadAsync_ThrowsWhenSecretsMissing()
    {
        var path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, """{"schemaVersion":2,"secrets":{}}""");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => SecretsExportFile.LoadAsync(path, CancellationToken.None));
        Assert.Contains("missing secrets", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
