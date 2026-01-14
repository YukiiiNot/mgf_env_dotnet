using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using MGF.Hosting.Configuration;
using Xunit;

[CollectionDefinition("HostEnvironmentCoherence", DisableParallelization = true)]
public sealed class HostEnvironmentCoherenceCollection
{
}

[Collection("HostEnvironmentCoherence")]
public sealed class HostEnvironmentCoherenceTests
{
    [Fact]
    public void ResolveHostEnvironmentName_Throws_OnExplicitMismatch()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            MgfHostConfiguration.ResolveHostEnvironmentName("Dev", "Production", "Production"));

        Assert.Contains("MGF_ENV=Dev", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("expected host env=Development", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveHostEnvironmentName_Allows_ExplicitMatch()
    {
        var result = MgfHostConfiguration.ResolveHostEnvironmentName("Dev", "Development", "Development");
        Assert.Equal("Development", result);
    }

    [Fact]
    public void ResolveHostEnvironmentName_Throws_OnProdMismatch()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            MgfHostConfiguration.ResolveHostEnvironmentName("Prod", "Development", "Development"));

        Assert.Contains("MGF_ENV=Prod", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("expected host env=Production", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveHostEnvironmentName_Derives_WhenImplicit()
    {
        var result = MgfHostConfiguration.ResolveHostEnvironmentName("Dev", null, "Production");
        Assert.Equal("Development", result);
    }

    [Fact]
    public void ConfigureMgfConfiguration_UsesDerivedHostEnv_WhenHostEnvImplicit()
    {
        var originalDirectory = Directory.GetCurrentDirectory();
        var originalDotnetEnv = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        var originalAspnetEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        var originalMgfEnv = Environment.GetEnvironmentVariable("MGF_ENV");
        var originalConfigDir = Environment.GetEnvironmentVariable("MGF_CONFIG_DIR");

        var tempRoot = Path.Combine(Path.GetTempPath(), "mgf-config-" + Guid.NewGuid().ToString("N"));
        var configDir = Path.Combine(tempRoot, "config");
        Directory.CreateDirectory(configDir);

        File.WriteAllText(Path.Combine(configDir, "appsettings.json"), "{ \"Test\": { \"Value\": \"base\" } }");
        File.WriteAllText(Path.Combine(configDir, "appsettings.Development.json"), "{ \"Test\": { \"Value\": \"dev\" } }");

        try
        {
            Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", null);
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", null);
            Environment.SetEnvironmentVariable("MGF_ENV", "Dev");
            Environment.SetEnvironmentVariable("MGF_CONFIG_DIR", null);
            Directory.SetCurrentDirectory(tempRoot);

            var context = CreateContext(environmentName: "Production", contentRoot: tempRoot);
            var builder = new ConfigurationBuilder();
            MgfHostConfiguration.ConfigureMgfConfiguration(context, builder);
            var config = builder.Build();

            Assert.Equal("dev", config["Test:Value"]);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);
            Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", originalDotnetEnv);
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", originalAspnetEnv);
            Environment.SetEnvironmentVariable("MGF_ENV", originalMgfEnv);
            Environment.SetEnvironmentVariable("MGF_CONFIG_DIR", originalConfigDir);

            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static HostBuilderContext CreateContext(string environmentName, string contentRoot)
    {
        var context = new HostBuilderContext(new Dictionary<object, object>());
        context.HostingEnvironment = new TestHostEnvironment
        {
            EnvironmentName = environmentName,
            ApplicationName = "MGF.Tests",
            ContentRootPath = contentRoot,
            ContentRootFileProvider = new PhysicalFileProvider(contentRoot),
        };
        context.Configuration = new ConfigurationBuilder().Build();
        return context;
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = string.Empty;
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
