using System.Xml.Linq;

namespace MGF.Architecture.Tests;

public sealed class ArchitectureRulesTests
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string SrcRoot = Path.Combine(RepoRoot, "src");
    private static readonly string DocsRoot = Path.Combine(RepoRoot, "docs");

    [Fact]
    public void UseCases_DoNotReference_Data_Or_Ef_Npgsql()
    {
        var projectPath = Path.Combine(RepoRoot, "src", "Application", "MGF.UseCases", "MGF.UseCases.csproj");
        var projectRefs = GetProjectReferences(projectPath);
        var packages = GetPackageReferences(projectPath);

        Assert.DoesNotContain(projectRefs, reference => IsProject(reference, "MGF.Data.csproj"));
        Assert.DoesNotContain(packages, IsEfCorePackage);
        Assert.DoesNotContain(packages, IsNpgsqlPackage);
    }

    [Fact]
    public void Operations_DoNotReference_Worker_Data_Or_Ef_Npgsql()
    {
        var operationsRoot = Path.Combine(SrcRoot, "Operations");
        var projects = Directory.GetFiles(operationsRoot, "*.csproj", SearchOption.AllDirectories);

        var waivers = new Dictionary<string, OperationWaiver>(StringComparer.OrdinalIgnoreCase)
        {
            ["MGF.ProjectBootstrapCli.csproj"] = new OperationWaiver(AllowDataReference: true, AllowNpgsql: true)
        };

        foreach (var projectPath in projects)
        {
            var projectName = Path.GetFileName(projectPath);
            var waiver = waivers.TryGetValue(projectName, out var configured) ? configured : new OperationWaiver();

            var projectRefs = GetProjectReferences(projectPath);
            var packages = GetPackageReferences(projectPath);

            Assert.DoesNotContain(projectRefs, reference => IsProject(reference, "MGF.Worker.csproj"));

            if (!waiver.AllowDataReference)
            {
                Assert.DoesNotContain(projectRefs, reference => IsProject(reference, "MGF.Data.csproj"));
            }

            if (!waiver.AllowEfCore)
            {
                Assert.DoesNotContain(packages, IsEfCorePackage);
            }

            if (!waiver.AllowNpgsql)
            {
                Assert.DoesNotContain(packages, IsNpgsqlPackage);
            }
        }
    }

    [Fact]
    public void Contracts_AreHostAgnostic()
    {
        var projectPath = Path.Combine(RepoRoot, "src", "Core", "MGF.Contracts", "MGF.Contracts.csproj");
        var packages = GetPackageReferences(projectPath);

        Assert.DoesNotContain(packages, IsHostCoupledPackage);
        Assert.DoesNotContain(packages, IsEfCorePackage);
        Assert.DoesNotContain(packages, IsNpgsqlPackage);
    }

    [Fact]
    public void PurePlatform_Projects_DoNotReference_Core_Application_Or_Data()
    {
        var purePlatformProjects = Array.Empty<string>();

        foreach (var projectName in purePlatformProjects)
        {
            var projectPath = Path.Combine(SrcRoot, "Platform", projectName, $"{projectName}.csproj");
            var projectRefs = GetProjectReferences(projectPath);

            Assert.DoesNotContain(projectRefs, reference => IsProject(reference, "MGF.Contracts.csproj"));
            Assert.DoesNotContain(projectRefs, reference => IsProject(reference, "MGF.Domain.csproj"));
            Assert.DoesNotContain(projectRefs, reference => IsProject(reference, "MGF.UseCases.csproj"));
            Assert.DoesNotContain(projectRefs, reference => IsProject(reference, "MGF.Data.csproj"));
        }
    }

    [Fact]
    public void DevTools_AreNotReferenced_From_Production_Projects()
    {
        var devToolsRoot = Path.Combine(SrcRoot, "DevTools");
        var projects = Directory.GetFiles(SrcRoot, "*.csproj", SearchOption.AllDirectories);

        foreach (var projectPath in projects)
        {
            if (IsUnder(projectPath, devToolsRoot))
            {
                continue;
            }

            var projectRefs = GetProjectReferences(projectPath);
            Assert.DoesNotContain(projectRefs, reference => IsUnder(reference, devToolsRoot));
        }
    }

    [Fact]
    public void Readmes_Outside_Docs_Are_Signposts_Only()
    {
        var readmes = Directory.GetFiles(RepoRoot, "README.md", SearchOption.AllDirectories)
            .Where(path => !IsUnder(path, DocsRoot))
            .ToArray();

        foreach (var readme in readmes)
        {
            var lines = File.ReadAllLines(readme);
            Assert.True(lines.Length <= 10, $"README exceeds 10 lines: {readme}");

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                Assert.Contains("docs/", line, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "MGF.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Repo root not found (MGF.sln missing).");
    }

    private static IReadOnlyList<string> GetProjectReferences(string projectPath)
    {
        var doc = XDocument.Load(projectPath);
        var ns = doc.Root?.Name.Namespace ?? XNamespace.None;
        var projectDir = Path.GetDirectoryName(projectPath) ?? RepoRoot;

        return doc.Descendants(ns + "ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => Path.GetFullPath(Path.Combine(projectDir, value!)))
            .ToList();
    }

    private static IReadOnlyList<string> GetPackageReferences(string projectPath)
    {
        var doc = XDocument.Load(projectPath);
        var ns = doc.Root?.Name.Namespace ?? XNamespace.None;

        return doc.Descendants(ns + "PackageReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToList();
    }

    private static bool IsProject(string referencePath, string projectFileName)
        => referencePath.EndsWith(projectFileName, StringComparison.OrdinalIgnoreCase);

    private static bool IsEfCorePackage(string package)
        => package.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.OrdinalIgnoreCase);

    private static bool IsNpgsqlPackage(string package)
        => package.Equals("Npgsql", StringComparison.OrdinalIgnoreCase)
           || package.StartsWith("Npgsql.", StringComparison.OrdinalIgnoreCase);

    private static bool IsHostCoupledPackage(string package)
        => package.StartsWith("Microsoft.Extensions.Configuration", StringComparison.OrdinalIgnoreCase)
           || package.StartsWith("Microsoft.Extensions.Options", StringComparison.OrdinalIgnoreCase)
           || package.StartsWith("Microsoft.Extensions.DependencyInjection", StringComparison.OrdinalIgnoreCase)
           || package.StartsWith("Microsoft.Extensions.Hosting", StringComparison.OrdinalIgnoreCase);

    private static bool IsUnder(string path, string root)
    {
        var fullPath = Path.GetFullPath(path);
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record OperationWaiver(
        bool AllowDataReference = false,
        bool AllowEfCore = false,
        bool AllowNpgsql = false);
}
