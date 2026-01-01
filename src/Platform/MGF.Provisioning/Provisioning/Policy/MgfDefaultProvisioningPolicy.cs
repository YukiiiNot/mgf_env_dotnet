using System.Text.RegularExpressions;

namespace MGF.Provisioning.Policy;

public sealed class MgfDefaultProvisioningPolicy : IProvisioningPolicy
{
    private static readonly Regex TopLevelPrefixRegex = new("^\\d{2}_.+", RegexOptions.Compiled);

    public string ManifestFolderRelativePath => Path.Combine("00_Admin", ".mgf", "manifest");

    public void ValidateTopLevelFolderName(string name)
    {
        if (!TopLevelPrefixRegex.IsMatch(name))
        {
            throw new InvalidOperationException($"Top-level folder '{name}' must match ^\\d{{2}}_.+.");
        }
    }

    public void ValidateNodeName(string name, string topLevelName)
    {
        if (string.Equals(name, ".mgf", StringComparison.Ordinal)
            && !string.Equals(topLevelName, "00_Admin", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(".mgf folder is only allowed under 00_Admin.");
        }
    }
}
