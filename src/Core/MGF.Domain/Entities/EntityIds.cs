namespace MGF.Domain.Entities;

public static class EntityIds
{
    public static string NewClientId() => NewWithPrefix("cli");
    public static string NewPersonId() => NewWithPrefix("per");
    public static string NewProjectId() => NewWithPrefix("prj");

    public static string NewWithPrefix(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            throw new ArgumentException("Prefix must be non-empty.", nameof(prefix));
        }

        var normalizedPrefix = prefix.Trim();
        if (normalizedPrefix.EndsWith('_'))
        {
            normalizedPrefix = normalizedPrefix[..^1];
        }

        return $"{normalizedPrefix}_{UlidString.New()}";
    }
}
