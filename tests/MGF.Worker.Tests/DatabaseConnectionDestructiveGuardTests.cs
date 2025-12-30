using MGF.Infrastructure.Configuration;
using Xunit;

namespace MGF.Worker.Tests;

public sealed class DatabaseConnectionDestructiveGuardTests
{
    [Fact]
    public void EnsureDestructiveAllowedOrThrow_Blocks_WhenEnvNotDev()
    {
        var previousEnv = Environment.GetEnvironmentVariable("MGF_ENV");
        var previousAllow = Environment.GetEnvironmentVariable("MGF_ALLOW_DESTRUCTIVE");
        var previousAck = Environment.GetEnvironmentVariable("MGF_DESTRUCTIVE_ACK");

        try
        {
            Environment.SetEnvironmentVariable("MGF_ENV", "Staging");
            Environment.SetEnvironmentVariable("MGF_ALLOW_DESTRUCTIVE", "true");
            Environment.SetEnvironmentVariable("MGF_DESTRUCTIVE_ACK", "I_UNDERSTAND");

            Assert.Throws<InvalidOperationException>(
                () => DatabaseConnection.EnsureDestructiveAllowedOrThrow("test", "Host=dev")
            );
        }
        finally
        {
            Environment.SetEnvironmentVariable("MGF_ENV", previousEnv);
            Environment.SetEnvironmentVariable("MGF_ALLOW_DESTRUCTIVE", previousAllow);
            Environment.SetEnvironmentVariable("MGF_DESTRUCTIVE_ACK", previousAck);
        }
    }

    [Fact]
    public void EnsureDestructiveAllowedOrThrow_Blocks_WhenConnectionStringLooksNonDev()
    {
        var previousEnv = Environment.GetEnvironmentVariable("MGF_ENV");
        var previousAllow = Environment.GetEnvironmentVariable("MGF_ALLOW_DESTRUCTIVE");
        var previousAck = Environment.GetEnvironmentVariable("MGF_DESTRUCTIVE_ACK");

        try
        {
            Environment.SetEnvironmentVariable("MGF_ENV", "Dev");
            Environment.SetEnvironmentVariable("MGF_ALLOW_DESTRUCTIVE", "true");
            Environment.SetEnvironmentVariable("MGF_DESTRUCTIVE_ACK", "I_UNDERSTAND");

            Assert.Throws<InvalidOperationException>(
                () => DatabaseConnection.EnsureDestructiveAllowedOrThrow("test", "Host=prod-db")
            );
        }
        finally
        {
            Environment.SetEnvironmentVariable("MGF_ENV", previousEnv);
            Environment.SetEnvironmentVariable("MGF_ALLOW_DESTRUCTIVE", previousAllow);
            Environment.SetEnvironmentVariable("MGF_DESTRUCTIVE_ACK", previousAck);
        }
    }

    [Fact]
    public void EnsureDestructiveAllowedOrThrow_Allows_WhenDevAndAcknowledged()
    {
        var previousEnv = Environment.GetEnvironmentVariable("MGF_ENV");
        var previousAllow = Environment.GetEnvironmentVariable("MGF_ALLOW_DESTRUCTIVE");
        var previousAck = Environment.GetEnvironmentVariable("MGF_DESTRUCTIVE_ACK");

        try
        {
            Environment.SetEnvironmentVariable("MGF_ENV", "Dev");
            Environment.SetEnvironmentVariable("MGF_ALLOW_DESTRUCTIVE", "true");
            Environment.SetEnvironmentVariable("MGF_DESTRUCTIVE_ACK", "I_UNDERSTAND");

            DatabaseConnection.EnsureDestructiveAllowedOrThrow("test", "Host=dev-db");
        }
        finally
        {
            Environment.SetEnvironmentVariable("MGF_ENV", previousEnv);
            Environment.SetEnvironmentVariable("MGF_ALLOW_DESTRUCTIVE", previousAllow);
            Environment.SetEnvironmentVariable("MGF_DESTRUCTIVE_ACK", previousAck);
        }
    }
}
