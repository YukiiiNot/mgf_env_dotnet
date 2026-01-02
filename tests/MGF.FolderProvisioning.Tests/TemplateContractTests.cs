namespace MGF.FolderProvisioning.Tests;

public sealed class TemplateContractTests
{
    [Fact]
    public async Task AllTemplates_IncludeDumpAndManifestFolder()
    {
        var repoRoot = FindRepoRoot();
        var templatesDir = Path.Combine(repoRoot, "artifacts", "templates");
        var schemaPath = Path.Combine(repoRoot, "artifacts", "schemas", "mgf.folderTemplate.schema.json");

        var loader = new FolderTemplateLoader();
        var templateFiles = Directory.GetFiles(templatesDir, "*.json", SearchOption.TopDirectoryOnly);

        foreach (var templatePath in templateFiles)
        {
            var loaded = await loader.LoadAsync(templatePath, schemaPath, CancellationToken.None);
            var root = loaded.Template.Root ?? throw new InvalidOperationException("Template root missing.");

            Assert.NotNull(root.Children);
            Assert.Contains(root.Children!, child => NameEquals(child.Name, "99_Dump"));

            var admin = root.Children!.FirstOrDefault(child => NameEquals(child.Name, "00_Admin"));
            Assert.NotNull(admin);

            var mgf = admin!.Children?.FirstOrDefault(child => NameEquals(child.Name, ".mgf"));
            Assert.NotNull(mgf);

            var manifest = mgf!.Children?.FirstOrDefault(child => NameEquals(child.Name, "manifest"));
            Assert.NotNull(manifest);
        }
    }

    private static bool NameEquals(string? left, string right)
        => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

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


