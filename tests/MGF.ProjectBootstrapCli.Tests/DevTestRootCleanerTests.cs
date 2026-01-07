using MGF.ProjectBootstrapCli;

public sealed class DevTestRootCleanerTests
{
    [Fact]
    public void Plan_DetectsRootManifestAsLegacy()
    {
        var root = CreateTempRoot();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "00_Admin", ".mgf", "manifest"));
            Directory.CreateDirectory(Path.Combine(root, "99_Dump"));
            File.WriteAllText(Path.Combine(root, "folder_manifest.json"), "legacy");

            var contract = new DevTestRootContract(
                RequiredFolders: new[] { "00_Admin", "99_Dump" },
                OptionalFolders: Array.Empty<string>(),
                AllowedExtras: Array.Empty<string>(),
                AllowedRootFiles: new[] { "desktop.ini" },
                QuarantineRelpath: "99_Dump/_quarantine"
            );

            var plan = DevTestRootCleaner.Plan(
                root,
                contract,
                new DevTestRootOptions(
                    DryRun: true,
                    MaxItems: 10,
                    MaxBytes: 1024,
                    ForceUnknownSize: false,
                    AllowMeasure: true,
                    TimestampUtc: DateTimeOffset.UtcNow
                )
            );

            Assert.Single(plan.LegacyManifestFiles);
            Assert.Equal(Path.Combine(root, "folder_manifest.json"), plan.LegacyManifestFiles[0].SourcePath);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Apply_CreatesAdminManifestAndDump()
    {
        var root = CreateTempRoot();
        try
        {
            var contract = new DevTestRootContract(
                RequiredFolders: new[] { "00_Admin", "99_Dump" },
                OptionalFolders: Array.Empty<string>(),
                AllowedExtras: Array.Empty<string>(),
                AllowedRootFiles: new[] { "desktop.ini" },
                QuarantineRelpath: "99_Dump/_quarantine"
            );

            var plan = DevTestRootCleaner.Plan(
                root,
                contract,
                new DevTestRootOptions(
                    DryRun: false,
                    MaxItems: 10,
                    MaxBytes: 1024,
                    ForceUnknownSize: false,
                    AllowMeasure: true,
                    TimestampUtc: DateTimeOffset.UtcNow
                )
            );

            var result = DevTestRootCleaner.Apply(
                plan,
                contract,
                new DevTestRootOptions(
                    DryRun: false,
                    MaxItems: 10,
                    MaxBytes: 1024,
                    ForceUnknownSize: false,
                    AllowMeasure: true,
                    TimestampUtc: DateTimeOffset.UtcNow
                )
            );

            Assert.Empty(result.Errors);
            Assert.True(Directory.Exists(Path.Combine(root, "00_Admin")));
            Assert.True(Directory.Exists(Path.Combine(root, "99_Dump")));
            Assert.True(Directory.Exists(Path.Combine(root, "00_Admin", ".mgf", "manifest")));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void BuildMovePlan_RespectsUnknownSizeForce()
    {
        var entry = new RootEntry(
            Name: "unknown",
            Path: "C:\\fake",
            Kind: "folder",
            IsReparsePoint: true,
            SizeBytes: null,
            ItemCount: null
        );

        var blocked = DevTestRootCleaner.BuildMovePlan(entry, 10, 1024, forceUnknownSize: false, allowMeasure: false);
        Assert.False(blocked.WillMove);
        Assert.Equal("unknown_size", blocked.BlockedReason);

        var forced = DevTestRootCleaner.BuildMovePlan(entry, 10, 1024, forceUnknownSize: true, allowMeasure: false);
        Assert.True(forced.WillMove);
        Assert.Equal("unknown_size", forced.BlockedReason);
    }

    [Fact]
    public void Apply_DoesNotOverwriteCanonicalManifest()
    {
        var root = CreateTempRoot();
        try
        {
            var manifestDir = Path.Combine(root, "00_Admin", ".mgf", "manifest");
            Directory.CreateDirectory(manifestDir);
            var canonicalPath = Path.Combine(manifestDir, "folder_manifest.json");
            File.WriteAllText(canonicalPath, "canonical");

            Directory.CreateDirectory(Path.Combine(root, "99_Dump"));
            File.WriteAllText(Path.Combine(root, "folder_manifest.json"), "legacy");

            var contract = new DevTestRootContract(
                RequiredFolders: new[] { "00_Admin", "99_Dump" },
                OptionalFolders: Array.Empty<string>(),
                AllowedExtras: Array.Empty<string>(),
                AllowedRootFiles: new[] { "desktop.ini" },
                QuarantineRelpath: "99_Dump/_quarantine"
            );

            var options = new DevTestRootOptions(
                DryRun: false,
                MaxItems: 10,
                MaxBytes: 1024,
                ForceUnknownSize: false,
                AllowMeasure: true,
                TimestampUtc: DateTimeOffset.UtcNow
            );

            var plan = DevTestRootCleaner.Plan(root, contract, options);
            var result = DevTestRootCleaner.Apply(plan, contract, options);

            Assert.Empty(result.Errors);
            Assert.True(File.Exists(canonicalPath));
            Assert.Equal("canonical", File.ReadAllText(canonicalPath));
            Assert.False(File.Exists(Path.Combine(root, "folder_manifest.json")));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"mgf_devtest_clean_{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }
}

