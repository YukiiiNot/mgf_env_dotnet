using Microsoft.Extensions.Configuration;
using MGF.Data.Configuration;

public class DatabaseConnectionTests
{
    [Fact]
    public void ResolveConnectionString_ThrowsOnPlaceholderInDev()
    {
        var originalEnv = Environment.GetEnvironmentVariable("MGF_ENV");
        var originalMode = Environment.GetEnvironmentVariable("MGF_DB_MODE");

        Environment.SetEnvironmentVariable("MGF_ENV", "Dev");
        Environment.SetEnvironmentVariable("MGF_DB_MODE", "direct");

        try
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Database:Dev:DirectConnectionString"] = "Host=db.YOUR_REF.supabase.co;Password=YOUR_PASSWORD"
                })
                .Build();

            var ex = Assert.Throws<InvalidOperationException>(() => DatabaseConnection.ResolveConnectionString(config));
            Assert.Contains("placeholder", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MGF_ENV", originalEnv);
            Environment.SetEnvironmentVariable("MGF_DB_MODE", originalMode);
        }
    }
}
