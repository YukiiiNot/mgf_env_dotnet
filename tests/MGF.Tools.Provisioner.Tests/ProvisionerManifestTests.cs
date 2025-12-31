namespace MGF.Provisioning.Tests;

public sealed class ProvisionerManifestTests
{
    [Fact]
    public async Task Execute_WritesManifestUnderMgfWhenPresent()
    {
        var tempBase = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempBase);

        try
        {
            var repoRoot = FindRepoRoot();
            var templatePath = Path.Combine(repoRoot, "docs", "templates", "dropbox_project_container.json");
            var schemaPath = Path.Combine(repoRoot, "docs", "schemas", "mgf.folderTemplate.schema.json");

            var tokens = ProvisioningTokens.Create("MGF25-TEST", "ManifestTest", "Client", Array.Empty<string>());
            var provisioner = new FolderProvisioner(new LocalFileStore());

            var result = await provisioner.ExecuteAsync(
                new ProvisioningRequest(
                    TemplatePath: templatePath,
                    SchemaPath: schemaPath,
                    BasePath: tempBase,
                    SeedsPath: Path.Combine(repoRoot, "docs", "templates", "seeds"),
                    Tokens: tokens,
                    Mode: ProvisioningMode.Apply,
                    ForceOverwriteSeededFiles: false
                ),
                CancellationToken.None
            );

            var expectedSuffix = Path.Combine("00_Admin", ".mgf", "manifest", "folder_manifest.json");
            Assert.EndsWith(expectedSuffix, result.ManifestPath, StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(result.ManifestPath));
        }
        finally
        {
            if (Directory.Exists(tempBase))
            {
                Directory.Delete(tempBase, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Execute_WritesManifestUnderMgfForNasArchiveContainer()
    {
        var tempBase = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempBase);

        try
        {
            var repoRoot = FindRepoRoot();
            var templatePath = Path.Combine(repoRoot, "docs", "templates", "nas_archive_container.json");
            var schemaPath = Path.Combine(repoRoot, "docs", "schemas", "mgf.folderTemplate.schema.json");

            var tokens = ProvisioningTokens.Create("MGF25-TEST", "ManifestTest", "Client", Array.Empty<string>());
            var provisioner = new FolderProvisioner(new LocalFileStore());

            var result = await provisioner.ExecuteAsync(
                new ProvisioningRequest(
                    TemplatePath: templatePath,
                    SchemaPath: schemaPath,
                    BasePath: tempBase,
                    SeedsPath: Path.Combine(repoRoot, "docs", "templates", "seeds"),
                    Tokens: tokens,
                    Mode: ProvisioningMode.Apply,
                    ForceOverwriteSeededFiles: false
                ),
                CancellationToken.None
            );

            var expectedSuffix = Path.Combine("00_Admin", ".mgf", "manifest", "folder_manifest.json");
            Assert.EndsWith(expectedSuffix, result.ManifestPath, StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(result.ManifestPath));
        }
        finally
        {
            if (Directory.Exists(tempBase))
            {
                Directory.Delete(tempBase, recursive: true);
            }
        }
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "MGF.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate repo root for schema path.");
    }
}
