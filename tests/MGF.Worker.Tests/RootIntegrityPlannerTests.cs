using MGF.Contracts.Abstractions.RootIntegrity;
using MGF.Storage.RootIntegrity;

public sealed class RootIntegrityPlannerTests
{
    [Fact]
    public void ScanRoot_FindsMissingRequiredAndOptional()
    {
        var root = CreateTempRoot();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "00_Admin"));

            var contract = new RootIntegrityContract(
                ProviderKey: "dropbox",
                RootKey: "root",
                ContractKey: "dropbox_root",
                RequiredFolders: ["00_Admin", "01_Docs"],
                OptionalFolders: ["04_Staging"],
                AllowedExtras: Array.Empty<string>(),
                AllowedRootFiles: ["desktop.ini"],
                QuarantineRelpath: "99_Dump/_quarantine",
                MaxItems: 1000,
                MaxBytes: 1024,
                IsActive: true
            );

            var scan = RootIntegrityChecker.ScanRoot(root, contract, contract.AllowedExtras, contract.AllowedRootFiles);

            Assert.Contains("01_Docs", scan.MissingRequired);
            Assert.Contains("04_Staging", scan.MissingOptional);
            Assert.Empty(scan.UnknownEntries);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void ScanRoot_RespectsAllowedExtras()
    {
        var root = CreateTempRoot();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "00_Admin"));
            Directory.CreateDirectory(Path.Combine(root, "99_TestRuns"));
            Directory.CreateDirectory(Path.Combine(root, "99_Weird"));

            var contract = new RootIntegrityContract(
                ProviderKey: "dropbox",
                RootKey: "root",
                ContractKey: "dropbox_root",
                RequiredFolders: ["00_Admin"],
                OptionalFolders: Array.Empty<string>(),
                AllowedExtras: ["99_TestRuns"],
                AllowedRootFiles: ["desktop.ini"],
                QuarantineRelpath: null,
                MaxItems: 1000,
                MaxBytes: 1024,
                IsActive: true
            );

            var scan = RootIntegrityChecker.ScanRoot(root, contract, contract.AllowedExtras, contract.AllowedRootFiles);

            Assert.DoesNotContain(scan.UnknownEntries, entry => entry.Name == "99_TestRuns");
            Assert.Contains(scan.UnknownEntries, entry => entry.Name == "99_Weird");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void MatchesAllowedExtras_AllowsWildcard()
    {
        Assert.True(RootIntegrityChecker.MatchesAllowedExtras("99_TestRuns", ["99_*"]));
        Assert.False(RootIntegrityChecker.MatchesAllowedExtras("01_Docs", ["99_*"]));
    }

    [Fact]
    public void ScanRoot_FlagsRootFilesExceptAllowed()
    {
        var root = CreateTempRoot();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "00_Admin"));
            File.WriteAllText(Path.Combine(root, "desktop.ini"), "stub");
            File.WriteAllText(Path.Combine(root, "notes.txt"), "stub");

            var contract = new RootIntegrityContract(
                ProviderKey: "dropbox",
                RootKey: "root",
                ContractKey: "dropbox_root",
                RequiredFolders: ["00_Admin"],
                OptionalFolders: Array.Empty<string>(),
                AllowedExtras: Array.Empty<string>(),
                AllowedRootFiles: ["desktop.ini"],
                QuarantineRelpath: null,
                MaxItems: 1000,
                MaxBytes: 1024,
                IsActive: true
            );

            var scan = RootIntegrityChecker.ScanRoot(root, contract, contract.AllowedExtras, contract.AllowedRootFiles);

            Assert.Single(scan.RootFiles);
            Assert.Equal("notes.txt", scan.RootFiles[0].Name);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void ScanRoot_FlagsMissingDumpWhenRequired()
    {
        var root = CreateTempRoot();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "00_Admin"));

            var contract = new RootIntegrityContract(
                ProviderKey: "dropbox",
                RootKey: "root",
                ContractKey: "dropbox_root",
                RequiredFolders: ["00_Admin", "99_Dump"],
                OptionalFolders: Array.Empty<string>(),
                AllowedExtras: Array.Empty<string>(),
                AllowedRootFiles: ["desktop.ini"],
                QuarantineRelpath: "99_Dump/_quarantine",
                MaxItems: 1000,
                MaxBytes: 1024,
                IsActive: true
            );

            var scan = RootIntegrityChecker.ScanRoot(root, contract, contract.AllowedExtras, contract.AllowedRootFiles);

            Assert.Contains("99_Dump", scan.MissingRequired);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void BuildMovePlan_BlocksLargeFile()
    {
        var root = CreateTempRoot();
        try
        {
            var filePath = Path.Combine(root, "big.bin");
            File.WriteAllBytes(filePath, new byte[2048]);

            var entry = new RootIntegrityEntry(
                Name: "big.bin",
                Path: filePath,
                Kind: "file",
                IsReparsePoint: false,
                SizeBytes: 2048,
                ItemCount: 1,
                Note: null
            );

            var plan = RootIntegrityChecker.BuildMovePlan(entry, maxItems: 100, maxBytes: 1024, allowMeasure: true);

            Assert.False(plan.WillMove);
            Assert.Equal("too_large_to_quarantine", plan.BlockedReason);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"mgf_root_integrity_{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }
}
