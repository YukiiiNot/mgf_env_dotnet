using MGF.Worker.ProjectDelivery;

public sealed class ProjectDeliveryVersioningTests
{
    [Fact]
    public void DetermineVersion_FirstRun_CreatesV1UnderFinal()
    {
        var root = Path.Combine(Path.GetTempPath(), $"mgf_delivery_{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        var sourceRoot = Path.Combine(Path.GetTempPath(), $"mgf_delivery_src_{Guid.NewGuid():N}");
        Directory.CreateDirectory(sourceRoot);
        var filePath = Path.Combine(sourceRoot, "sample.mp4");
        File.WriteAllBytes(filePath, new byte[] { 1, 2, 3 });
        var lastWrite = File.GetLastWriteTimeUtc(filePath);

        var sourceFiles = new[]
        {
            new ProjectDeliverer.DeliveryFile(filePath, "sample.mp4", 3, lastWrite)
        };

        var plan = ProjectDeliverer.DetermineVersion(root, sourceFiles);

        Assert.True(plan.IsNewVersion);
        Assert.Equal("v1", plan.VersionLabel);
        Assert.Equal(root, plan.StableRoot);
        Assert.EndsWith(Path.Combine(root, "v1"), plan.VersionRoot, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DetermineVersion_SameFilesInV1_IsNoop()
    {
        var root = Path.Combine(Path.GetTempPath(), $"mgf_delivery_{Guid.NewGuid():N}");
        var v1Root = Path.Combine(root, "v1");
        Directory.CreateDirectory(v1Root);

        var existingPath = Path.Combine(v1Root, "sample.mp4");
        File.WriteAllBytes(existingPath, new byte[] { 1, 2, 3 });
        var lastWrite = File.GetLastWriteTimeUtc(existingPath);

        var sourceRoot = Path.Combine(Path.GetTempPath(), $"mgf_delivery_src_{Guid.NewGuid():N}");
        Directory.CreateDirectory(sourceRoot);
        var filePath = Path.Combine(sourceRoot, "sample.mp4");
        File.WriteAllBytes(filePath, new byte[] { 1, 2, 3 });
        File.SetLastWriteTimeUtc(filePath, lastWrite);

        var sourceFiles = new[]
        {
            new ProjectDeliverer.DeliveryFile(filePath, "sample.mp4", 3, lastWrite)
        };

        var plan = ProjectDeliverer.DetermineVersion(root, sourceFiles);

        Assert.False(plan.IsNewVersion);
        Assert.Equal("v1", plan.VersionLabel);
        Assert.Equal(root, plan.StableRoot);
        Assert.Equal(v1Root, plan.VersionRoot);
    }

    [Fact]
    public void DetermineVersion_ChangedFiles_CreatesNewVersion()
    {
        var root = Path.Combine(Path.GetTempPath(), $"mgf_delivery_{Guid.NewGuid():N}");
        var v1Root = Path.Combine(root, "v1");
        Directory.CreateDirectory(v1Root);

        var existingPath = Path.Combine(v1Root, "sample.mp4");
        File.WriteAllBytes(existingPath, new byte[] { 1, 2, 3 });

        var sourceRoot = Path.Combine(Path.GetTempPath(), $"mgf_delivery_src_{Guid.NewGuid():N}");
        Directory.CreateDirectory(sourceRoot);
        var newSource = Path.Combine(sourceRoot, "new.mp4");
        File.WriteAllBytes(newSource, new byte[] { 4, 5, 6, 7 });
        var lastWrite = File.GetLastWriteTimeUtc(newSource);

        var sourceFiles = new[]
        {
            new ProjectDeliverer.DeliveryFile(newSource, "new.mp4", 4, lastWrite)
        };

        var plan = ProjectDeliverer.DetermineVersion(root, sourceFiles);

        Assert.True(plan.IsNewVersion);
        Assert.Equal("v2", plan.VersionLabel);
        Assert.EndsWith(Path.Combine(root, "v2"), plan.VersionRoot, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DetermineVersion_LegacyFinal_NoVersions_MatchesAsLegacy()
    {
        var root = Path.Combine(Path.GetTempPath(), $"mgf_delivery_{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        var existingPath = Path.Combine(root, "sample.mp4");
        File.WriteAllBytes(existingPath, new byte[] { 1, 2, 3 });
        var lastWrite = File.GetLastWriteTimeUtc(existingPath);

        var sourceRoot = Path.Combine(Path.GetTempPath(), $"mgf_delivery_src_{Guid.NewGuid():N}");
        Directory.CreateDirectory(sourceRoot);
        var filePath = Path.Combine(sourceRoot, "sample.mp4");
        File.WriteAllBytes(filePath, new byte[] { 1, 2, 3 });
        File.SetLastWriteTimeUtc(filePath, lastWrite);

        var sourceFiles = new[]
        {
            new ProjectDeliverer.DeliveryFile(filePath, "sample.mp4", 3, lastWrite)
        };

        var plan = ProjectDeliverer.DetermineVersion(root, sourceFiles);

        Assert.False(plan.IsNewVersion);
        Assert.True(plan.LegacyFinalFiles);
        Assert.Equal("v1", plan.VersionLabel);
        Assert.Equal(root, plan.StableRoot);
        Assert.Equal(root, plan.VersionRoot);
    }

    [Fact]
    public void DetermineVersion_LegacyFinal_Different_CreatesV1Folder()
    {
        var root = Path.Combine(Path.GetTempPath(), $"mgf_delivery_{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        var existingPath = Path.Combine(root, "old.mp4");
        File.WriteAllBytes(existingPath, new byte[] { 1, 2, 3 });

        var sourceRoot = Path.Combine(Path.GetTempPath(), $"mgf_delivery_src_{Guid.NewGuid():N}");
        Directory.CreateDirectory(sourceRoot);
        var filePath = Path.Combine(sourceRoot, "new.mp4");
        File.WriteAllBytes(filePath, new byte[] { 4, 5, 6 });
        var lastWrite = File.GetLastWriteTimeUtc(filePath);

        var sourceFiles = new[]
        {
            new ProjectDeliverer.DeliveryFile(filePath, "new.mp4", 3, lastWrite)
        };

        var plan = ProjectDeliverer.DetermineVersion(root, sourceFiles);

        Assert.True(plan.IsNewVersion);
        Assert.True(plan.LegacyFinalFiles);
        Assert.Equal("v1", plan.VersionLabel);
        Assert.Equal(root, plan.StableRoot);
        Assert.EndsWith(Path.Combine(root, "v1"), plan.VersionRoot, StringComparison.OrdinalIgnoreCase);
    }
}
