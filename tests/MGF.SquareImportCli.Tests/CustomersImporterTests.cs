using Microsoft.EntityFrameworkCore;
using MGF.Infrastructure.Data;
using MGF.Tools.SquareImport.Importers;
using Xunit;

namespace MGF.Tools.SquareImport.Tests;

public sealed class CustomersImporterTests
{
    [Fact]
    public async Task Reset_Blocks_WhenConnectionStringLooksNonDev()
    {
        var previousEnv = Environment.GetEnvironmentVariable("MGF_ENV");

        try
        {
            Environment.SetEnvironmentVariable("MGF_ENV", "Dev");

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql("Host=prod.db.example;Database=prod;Username=postgres;Password=ignored")
                .Options;

            await using var db = new AppDbContext(options);
            var importer = new CustomersImporter(db);

            var summary = await importer.ResetAsync(dryRun: true, cancellationToken: CancellationToken.None);

            Assert.True(summary.Errors > 0);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MGF_ENV", previousEnv);
        }
    }
}
