namespace MGF.Tools.Provisioner;

public static class TokenExpander
{
    private const string EditorToken = "{EDITOR_INITIALS}";

    public static string ExpandRootName(string name, ProvisioningTokens tokens)
    {
        if (name.Contains(EditorToken, StringComparison.Ordinal))
        {
            if (tokens.EditorInitials.Count > 1)
            {
                throw new InvalidOperationException("Root name cannot contain {EDITOR_INITIALS} with multiple editors.");
            }

            var editorValue = tokens.EditorInitials.Count == 1
                ? tokens.EditorInitials[0]
                : "_EDITOR_INITIALS_HERE";

            return ReplaceTokens(name, tokens, editorValue);
        }

        return ReplaceTokens(name, tokens, editorInitialsOverride: null);
    }

    public static IReadOnlyList<string> ExpandNodeName(string name, ProvisioningTokens tokens, bool optional)
    {
        if (!name.Contains(EditorToken, StringComparison.Ordinal))
        {
            return new[] { ReplaceTokens(name, tokens, editorInitialsOverride: null) };
        }

        if (tokens.EditorInitials.Count > 0)
        {
            return tokens.EditorInitials
                .Select(editor => ReplaceTokens(name, tokens, editor))
                .ToArray();
        }

        if (optional)
        {
            return Array.Empty<string>();
        }

        return new[] { ReplaceTokens(name, tokens, "_EDITOR_INITIALS_HERE") };
    }

    private static string ReplaceTokens(string input, ProvisioningTokens tokens, string? editorInitialsOverride)
    {
        var output = input;

        output = ReplaceToken(output, "{PROJECT_CODE}", tokens.ProjectCode);
        output = ReplaceToken(output, "{PROJECT_NAME}", tokens.ProjectName);
        output = ReplaceToken(output, "{CLIENT_NAME}", tokens.ClientName);

        if (output.Contains(EditorToken, StringComparison.Ordinal))
        {
            var editorValue = editorInitialsOverride;
            if (string.IsNullOrWhiteSpace(editorValue))
            {
                throw new InvalidOperationException("Missing editor initials for {EDITOR_INITIALS} token.");
            }

            output = output.Replace(EditorToken, editorValue, StringComparison.Ordinal);
        }

        return output;
    }

    private static string ReplaceToken(string input, string token, string? value, bool allowMissing = false)
    {
        if (!input.Contains(token, StringComparison.Ordinal))
        {
            return input;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            if (allowMissing)
            {
                return input.Replace(token, string.Empty, StringComparison.Ordinal);
            }

            throw new InvalidOperationException($"Missing value for token {token}.");
        }

        return input.Replace(token, value, StringComparison.Ordinal);
    }
}
