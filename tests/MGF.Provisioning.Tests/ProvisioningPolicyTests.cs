using MGF.Provisioning.Policy;

namespace MGF.Provisioning.Tests;

public sealed class ProvisioningPolicyTests
{
    [Fact]
    public void DefaultPolicy_RejectsTopLevelWithoutNumericPrefix()
    {
        var policy = new MgfDefaultProvisioningPolicy();

        var ex = Assert.Throws<InvalidOperationException>(() => policy.ValidateTopLevelFolderName("Admin"));
        Assert.Contains("Top-level", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DefaultPolicy_RestrictsMgfFolderToAdmin()
    {
        var policy = new MgfDefaultProvisioningPolicy();

        var ex = Assert.Throws<InvalidOperationException>(() => policy.ValidateNodeName(".mgf", "01_PreProduction"));
        Assert.Contains(".mgf", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DefaultPolicy_UsesAdminManifestFolder()
    {
        var policy = new MgfDefaultProvisioningPolicy();

        var expected = Path.Combine("00_Admin", ".mgf", "manifest");
        Assert.Equal(expected, policy.ManifestFolderRelativePath);
    }
}
