using MGF.Tools.LegacyAudit.Scanning;

namespace MGF.Tools.LegacyAudit.Tests;

public sealed class LegacyScannerTests
{
    [Fact]
    public void RootIsNotClassified()
    {
        var root = CreateTempRoot();
        try
        {
            var project = Path.Combine(root, "ProjectA");
            Directory.CreateDirectory(project);
            File.WriteAllText(Path.Combine(project, "ProjectA.prproj"), "");
            Directory.CreateDirectory(Path.Combine(project, "Adobe Premiere Pro Auto-Save"));

            var report = Scan(root);

            Assert.DoesNotContain(report.Classifications, item => item.Path == root);
        }
        finally
        {
            SafeDelete(root);
        }
    }

    [Fact]
    public void ContainerDetectedWhenMultipleProjectRootsExist()
    {
        var root = CreateTempRoot();
        try
        {
            var container = Path.Combine(root, "Container");
            Directory.CreateDirectory(container);

            CreateProjectRoot(Path.Combine(container, "ProjOne"));
            CreateProjectRoot(Path.Combine(container, "ProjTwo"));

            var report = Scan(root);

            Assert.Contains(report.Classifications, item => item.Path == container && item.Classification == "project_container");
            Assert.Contains(report.Classifications, item => item.Path.EndsWith("ProjOne") && item.Classification == "project_root");
            Assert.Contains(report.Classifications, item => item.Path.EndsWith("ProjTwo") && item.Classification == "project_root");
        }
        finally
        {
            SafeDelete(root);
        }
    }

    [Fact]
    public void MarkerOverridesClassificationForProjectAndContainer()
    {
        var root = CreateTempRoot();
        try
        {
            var project = Path.Combine(root, "MarkedProject");
            Directory.CreateDirectory(project);
            File.WriteAllText(Path.Combine(project, "_mgf_project.tag.json"), "{\"kind\":\"project\",\"confirmedBy\":\"Test\",\"confirmedAt\":\"2025-01-01\"}");

            var container = Path.Combine(root, "MarkedContainer");
            Directory.CreateDirectory(container);
            File.WriteAllText(Path.Combine(container, "_MGF_PROJECT.tag.json"), "{\"kind\":\"container\",\"confirmedBy\":\"Test\",\"confirmedAt\":\"2025-01-01\"}");

            var report = Scan(root);

            Assert.Contains(report.Classifications, item => item.Path == project && item.Classification == "project_confirmed");
            Assert.Contains(report.Classifications, item => item.Path == container && item.Classification == "container_confirmed");
        }
        finally
        {
            SafeDelete(root);
        }
    }

    [Fact]
    public void PrprojOnlyDoesNotBecomeProjectRoot()
    {
        var root = CreateTempRoot();
        try
        {
            var project = Path.Combine(root, "OnlyPrproj");
            Directory.CreateDirectory(project);
            File.WriteAllText(Path.Combine(project, "Only.prproj"), "");

            var report = Scan(root);

            Assert.Contains(report.Classifications, item => item.Path == project && item.Classification == "unknown_needs_review");
            Assert.DoesNotContain(report.Classifications, item => item.Path == project && item.Classification == "project_root");
        }
        finally
        {
            SafeDelete(root);
        }
    }

    private static void CreateProjectRoot(string path)
    {
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, "Project.prproj"), "");
        Directory.CreateDirectory(Path.Combine(path, "Adobe Premiere Pro Auto-Save"));
        Directory.CreateDirectory(Path.Combine(path, "Exports"));
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "MGF.LegacyAudit.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void SafeDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
        catch
        {
            // Best-effort cleanup for temp dirs.
        }
    }

    private static Models.ScanReport Scan(string root)
    {
        var scanner = new LegacyScanner();
        var options = new ScanOptions
        {
            RootPath = root,
            OutputPath = Path.Combine(root, "_out"),
            Profile = ScanProfile.Everything,
            MaxDepth = -1
        };

        return scanner.Scan(options, CancellationToken.None);
    }
}
