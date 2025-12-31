namespace MGF.Provisioning.Tests;

public sealed class TokenExpanderTests
{
    [Fact]
    public void ExpandNodeName_DuplicatesForEditors()
    {
        var tokens = ProvisioningTokens.Create("MGF25-0001", "Test", "Client", new[] { "EC", "MM" });

        var names = TokenExpander.ExpandNodeName("WORKING_{EDITOR_INITIALS}", tokens, optional: false);

        Assert.Equal(2, names.Count);
        Assert.Contains("WORKING_EC", names);
        Assert.Contains("WORKING_MM", names);
    }

    [Fact]
    public void ExpandNodeName_UsesPlaceholderWhenMissingEditors()
    {
        var tokens = ProvisioningTokens.Create("MGF25-0001", "Test", null, Array.Empty<string>());

        var names = TokenExpander.ExpandNodeName("WORKING_{EDITOR_INITIALS}", tokens, optional: false);

        Assert.Single(names);
        Assert.Contains("_EDITOR_INITIALS_HERE", names[0]);
    }

    [Fact]
    public void ExpandNodeName_SkipsOptionalWhenMissingEditors()
    {
        var tokens = ProvisioningTokens.Create("MGF25-0001", "Test", null, Array.Empty<string>());

        var names = TokenExpander.ExpandNodeName("WORKING_{EDITOR_INITIALS}", tokens, optional: true);

        Assert.Empty(names);
    }
}

