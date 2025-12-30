namespace MGF.Tools.Provisioner;

public sealed record ProvisioningResult(
    ProvisioningMode Mode,
    string TemplateKey,
    string TemplateHash,
    ProvisioningTokens Tokens,
    string TargetRoot,
    IReadOnlyList<PlanItem> ExpectedItems,
    IReadOnlyList<PlanItem> CreatedItems,
    IReadOnlyList<string> MissingRequired,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Errors,
    string ManifestPath
)
{
    public bool Success => Errors.Count == 0 && MissingRequired.Count == 0;

    public void WriteSummaryToConsole()
    {
        Console.WriteLine($"provisioner: mode={Mode} template={TemplateKey}");
        Console.WriteLine($"provisioner: target={TargetRoot}");
        Console.WriteLine($"provisioner: expected={ExpectedItems.Count} created={CreatedItems.Count} missing={MissingRequired.Count}");

        if (Warnings.Count > 0)
        {
            Console.WriteLine($"provisioner: warnings={Warnings.Count}");
        }

        if (Errors.Count > 0)
        {
            Console.WriteLine($"provisioner: errors={Errors.Count}");
        }

        Console.WriteLine($"provisioner: manifest={ManifestPath}");
    }
}
