namespace MGF.DevSecretsCli;

using System.Text.RegularExpressions;

internal sealed record SecretsFilterResult(
    SortedDictionary<string, string> Allowed,
    IReadOnlyList<string> MissingRequired,
    IReadOnlyList<string> SkippedKeys);

internal sealed record SecretsValidationResult(bool IsValid, string? Error);

internal static class SecretsFilter
{
    public static SecretsFilterResult Filter(
        IReadOnlyDictionary<string, string> source,
        ProjectSecretsConfig project,
        GlobalPolicy policy)
    {
        var allowed = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var missingRequired = new List<string>();
        var skipped = new List<string>();

        var lookup = new Dictionary<string, string>(source, StringComparer.OrdinalIgnoreCase);
        var allowedKeySet = new HashSet<string>(
            project.RequiredKeys.Concat(project.OptionalKeys),
            StringComparer.OrdinalIgnoreCase);

        foreach (var key in project.RequiredKeys)
        {
            if (TryGetValue(lookup, key, out var value))
            {
                if (SecretsPolicy.IsAllowedKey(key, policy))
                {
                    allowed[key] = value;
                }
                else
                {
                    skipped.Add(key);
                    missingRequired.Add(key);
                }
            }
            else
            {
                missingRequired.Add(key);
            }
        }

        foreach (var key in project.OptionalKeys)
        {
            if (TryGetValue(lookup, key, out var value))
            {
                if (SecretsPolicy.IsAllowedKey(key, policy))
                {
                    allowed[key] = value;
                }
                else
                {
                    skipped.Add(key);
                }
            }
        }

        foreach (var key in lookup.Keys)
        {
            if (!allowedKeySet.Contains(key) && SecretsPolicy.IsDisallowedKey(key, policy))
            {
                skipped.Add(key);
            }
        }

        return new SecretsFilterResult(allowed, missingRequired, skipped);
    }

    public static SecretsValidationResult ValidateExport(
        ProjectSecretsExport export,
        ProjectSecretsConfig project,
        GlobalPolicy policy)
    {
        var allowedKeys = new HashSet<string>(
            project.RequiredKeys.Concat(project.OptionalKeys),
            StringComparer.OrdinalIgnoreCase);

        var missingRequired = project.RequiredKeys
            .Where(required => !export.Secrets.ContainsKey(required))
            .ToArray();
        if (missingRequired.Length > 0)
        {
            return new SecretsValidationResult(false, $"Missing required keys: {string.Join(", ", missingRequired)}");
        }

        foreach (var key in export.Secrets.Keys)
        {
            if (!allowedKeys.Contains(key))
            {
                return new SecretsValidationResult(false, $"Key '{key}' is not allowed for project {project.Name}.");
            }

            if (!SecretsPolicy.IsAllowedKey(key, policy))
            {
                return new SecretsValidationResult(false, $"Key '{key}' violates policy filters.");
            }
        }

        return new SecretsValidationResult(true, null);
    }

    private static bool TryGetValue(IReadOnlyDictionary<string, string> source, string key, out string value)
    {
        if (source.TryGetValue(key, out value!))
        {
            return true;
        }

        return source.TryGetValue(key, out value!);
    }
}

internal static class SecretsPolicy
{
    private const string AllowedDbKey = "Database:Dev:DirectConnectionString";

    public static bool IsAllowedKey(string key, GlobalPolicy policy)
    {
        if (MatchesAny(key, policy.DisallowedKeyPatterns))
        {
            return false;
        }

        if (IsDbKey(key))
        {
            if (!string.Equals(key, AllowedDbKey, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return policy.AllowedDbConnectionKeyPatterns.Count == 0
                || MatchesAny(key, policy.AllowedDbConnectionKeyPatterns);
        }

        return true;
    }

    public static bool IsDisallowedKey(string key, GlobalPolicy policy)
        => MatchesAny(key, policy.DisallowedKeyPatterns);

    public static bool IsDbKey(string key)
        => key.Contains("ConnectionString", StringComparison.OrdinalIgnoreCase)
           || key.StartsWith("Database:", StringComparison.OrdinalIgnoreCase);

    private static bool MatchesAny(string key, IEnumerable<string> patterns)
    {
        foreach (var pattern in patterns)
        {
            if (WildcardMatch(key, pattern))
            {
                return true;
            }
        }

        return false;
    }

    private static bool WildcardMatch(string input, string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return false;
        }

        var regexPattern = "^" + Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";
        return Regex.IsMatch(input, regexPattern, RegexOptions.IgnoreCase);
    }
}

