namespace MGF.Provisioning.Policy;

public interface IProvisioningPolicy
{
    void ValidateTopLevelFolderName(string name);

    void ValidateNodeName(string name, string topLevelName);

    string ManifestFolderRelativePath { get; }
}
