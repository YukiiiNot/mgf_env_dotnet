using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using MGF.Tools.SquareImport.Commands;
using Xunit;

namespace MGF.Tools.SquareImport.Tests;

public sealed class CustomersResetCommandTests
{
    [Fact]
    public async Task Reset_ReturnsNonZero_WhenMissingDestructiveFlag()
    {
        var previousEnv = Environment.GetEnvironmentVariable("MGF_ENV");

        try
        {
            Environment.SetEnvironmentVariable("MGF_ENV", "Dev");

            var root = RootCommandFactory.Create();
            var parser = new CommandLineBuilder(root).UseDefaults().Build();

            var exitCode = await parser.InvokeAsync("customers --reset");

            Assert.NotEqual(0, exitCode);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MGF_ENV", previousEnv);
        }
    }

    [Fact]
    public async Task Reset_ReturnsNonZero_WhenEnvNotDev()
    {
        var previousEnv = Environment.GetEnvironmentVariable("MGF_ENV");

        try
        {
            Environment.SetEnvironmentVariable("MGF_ENV", "Prod");

            var root = RootCommandFactory.Create();
            var parser = new CommandLineBuilder(root).UseDefaults().Build();

            var exitCode = await parser.InvokeAsync(
                "customers --reset --i-understand-this-will-destroy-data --non-interactive"
            );

            Assert.NotEqual(0, exitCode);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MGF_ENV", previousEnv);
        }
    }

    [Fact]
    public async Task Reset_ReturnsNonZero_WhenConfirmationFails()
    {
        var previousEnv = Environment.GetEnvironmentVariable("MGF_ENV");
        var previousInput = Console.In;

        try
        {
            Environment.SetEnvironmentVariable("MGF_ENV", "Dev");
            Console.SetIn(new StringReader("NO"));

            var root = RootCommandFactory.Create();
            var parser = new CommandLineBuilder(root).UseDefaults().Build();

            var exitCode = await parser.InvokeAsync("customers --reset --i-understand-this-will-destroy-data");

            Assert.NotEqual(0, exitCode);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MGF_ENV", previousEnv);
            Console.SetIn(previousInput);
        }
    }

    [Fact]
    public async Task Reset_ReturnsNonZero_WhenConnectionStringLooksNonDev()
    {
        var previousEnv = Environment.GetEnvironmentVariable("MGF_ENV");
        var previousMode = Environment.GetEnvironmentVariable("MGF_DB_MODE");
        var previousConn = Environment.GetEnvironmentVariable("Database__Dev__DirectConnectionString");

        try
        {
            Environment.SetEnvironmentVariable("MGF_ENV", "Dev");
            Environment.SetEnvironmentVariable("MGF_DB_MODE", "direct");
            Environment.SetEnvironmentVariable(
                "Database__Dev__DirectConnectionString",
                "Host=prod.db.example;Database=production;Username=postgres;Password=ignored"
            );

            var root = RootCommandFactory.Create();
            var parser = new CommandLineBuilder(root).UseDefaults().Build();

            var exitCode = await parser.InvokeAsync(
                "customers --reset --i-understand-this-will-destroy-data --non-interactive"
            );

            Assert.NotEqual(0, exitCode);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MGF_ENV", previousEnv);
            Environment.SetEnvironmentVariable("MGF_DB_MODE", previousMode);
            Environment.SetEnvironmentVariable("Database__Dev__DirectConnectionString", previousConn);
        }
    }
}
