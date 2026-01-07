namespace MGF.Contracts.Abstractions.ProjectWorkflows;

public static class StorageMutationScopes
{
    public static string ForProject(string projectId)
    {
        if (string.IsNullOrWhiteSpace(projectId))
        {
            throw new ArgumentException("ProjectId is required.", nameof(projectId));
        }

        return $"project:{projectId}";
    }

    public static string ForRoot(string providerKey, string rootKey)
    {
        if (string.IsNullOrWhiteSpace(providerKey))
        {
            throw new ArgumentException("ProviderKey is required.", nameof(providerKey));
        }

        if (string.IsNullOrWhiteSpace(rootKey))
        {
            throw new ArgumentException("RootKey is required.", nameof(rootKey));
        }

        return $"root:{providerKey}:{rootKey}";
    }
}
