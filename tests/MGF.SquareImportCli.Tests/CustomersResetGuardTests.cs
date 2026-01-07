using MGF.Data.Configuration;
using MGF.SquareImportCli.Guards;
using Xunit;

namespace MGF.SquareImportCli.Tests;

public sealed class CustomersResetGuardTests
{
    [Fact]
    public void Evaluate_Blocks_WhenDestructiveFlagMissing()
    {
        var decision = CustomersResetGuard.Evaluate(
            env: MgfEnvironment.Dev,
            destructiveFlag: false,
            nonInteractive: false,
            inputRedirected: false
        );

        Assert.False(decision.Allowed);
        Assert.Contains("requires --i-understand-this-will-destroy-data", decision.ErrorMessage);
    }

    [Fact]
    public void Evaluate_Blocks_WhenEnvNotDev()
    {
        var decision = CustomersResetGuard.Evaluate(
            env: MgfEnvironment.Prod,
            destructiveFlag: true,
            nonInteractive: true,
            inputRedirected: false
        );

        Assert.False(decision.Allowed);
        Assert.Contains("DEV only", decision.ErrorMessage);
    }

    [Fact]
    public void Evaluate_Blocks_WhenInputRedirected_WithoutNonInteractive()
    {
        var decision = CustomersResetGuard.Evaluate(
            env: MgfEnvironment.Dev,
            destructiveFlag: true,
            nonInteractive: false,
            inputRedirected: true
        );

        Assert.False(decision.Allowed);
        Assert.Contains("non-interactive session", decision.ErrorMessage);
    }

    [Fact]
    public void Evaluate_Allows_WithNonInteractive()
    {
        var decision = CustomersResetGuard.Evaluate(
            env: MgfEnvironment.Dev,
            destructiveFlag: true,
            nonInteractive: true,
            inputRedirected: true
        );

        Assert.True(decision.Allowed);
        Assert.False(decision.RequiresConfirmation);
    }

    [Fact]
    public void Evaluate_RequiresConfirmation_WhenInteractive()
    {
        var decision = CustomersResetGuard.Evaluate(
            env: MgfEnvironment.Dev,
            destructiveFlag: true,
            nonInteractive: false,
            inputRedirected: false
        );

        Assert.True(decision.Allowed);
        Assert.True(decision.RequiresConfirmation);
    }
}


