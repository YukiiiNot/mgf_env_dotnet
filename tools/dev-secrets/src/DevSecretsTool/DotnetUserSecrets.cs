namespace MGF.DevSecretsCli;

internal sealed class DotnetUserSecrets
{
    public async Task<Dictionary<string, string>> ListAsync(string userSecretsId, CancellationToken cancellationToken)
    {
        var result = await ProcessRunner.RunAsync(
            "dotnet",
            new[] { "user-secrets", "list", "--id", userSecretsId },
            workingDirectory: null,
            cancellationToken);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"dotnet user-secrets list failed for {userSecretsId}: {result.StandardError}");
        }

        return ParseListOutput(result.StandardOutput);
    }

    public async Task SetAsync(string userSecretsId, string key, string value, CancellationToken cancellationToken)
    {
        var result = await ProcessRunner.RunAsync(
            "dotnet",
            new[] { "user-secrets", "set", "--id", userSecretsId, key, value },
            workingDirectory: null,
            cancellationToken);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"dotnet user-secrets set failed for {userSecretsId}: {result.StandardError}");
        }
    }

    internal static Dictionary<string, string> ParseListOutput(string output)
    {
        var secrets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using var reader = new StringReader(output);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                continue;
            }

            var separatorIndex = trimmed.IndexOf(" = ", StringComparison.Ordinal);
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = trimmed.Substring(0, separatorIndex).Trim();
            var value = trimmed.Substring(separatorIndex + 3).Trim();
            if (!string.IsNullOrWhiteSpace(key))
            {
                secrets[key] = value;
            }
        }

        return secrets;
    }
}

