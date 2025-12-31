namespace MGF.Tools.Provisioner;

public enum ProvisioningMode
{
    Plan,
    Apply,
    Verify,
    Repair
}

public sealed record ProvisioningRequest(
    ProvisioningMode Mode,
    string TemplatePath,
    string BasePath,
    string? SchemaPath,
    string? SeedsPath,
    ProvisioningTokens Tokens,
    bool ForceOverwriteSeededFiles
);

public sealed record ProvisioningTokens(
    string? ProjectCode,
    string? ProjectName,
    string? ClientName,
    IReadOnlyList<string> EditorInitials
)
{
    public static ProvisioningTokens Create(
        string? projectCode,
        string? projectName,
        string? clientName,
        IEnumerable<string> editors
    )
    {
        var parsedEditors = editors
            .SelectMany(e => e.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new ProvisioningTokens(projectCode, projectName, clientName, parsedEditors);
    }

    public Dictionary<string, object?> ToDictionary()
    {
        return new Dictionary<string, object?>
        {
            ["projectCode"] = ProjectCode,
            ["projectName"] = ProjectName,
            ["clientName"] = ClientName,
            ["editorInitials"] = EditorInitials
        };
    }
}
