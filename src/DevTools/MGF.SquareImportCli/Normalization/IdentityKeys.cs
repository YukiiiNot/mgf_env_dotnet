namespace MGF.SquareImportCli.Normalization;

public static class IdentityKeys
{
    public static string? NormalizeEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var value = email.Trim().ToLowerInvariant();
        return value.Length == 0 ? null : value;
    }

    public static string? NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return null;
        }

        Span<char> buffer = stackalloc char[phone.Length];
        var idx = 0;
        foreach (var c in phone)
        {
            if (char.IsDigit(c))
            {
                buffer[idx++] = c;
            }
        }

        if (idx == 0)
        {
            return null;
        }

        return new string(buffer[..idx]);
    }

    public static string? NormalizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var span = name.AsSpan().Trim();
        if (span.Length == 0)
        {
            return null;
        }

        Span<char> buffer = stackalloc char[span.Length];
        var idx = 0;
        var inWhitespace = false;

        foreach (var c in span)
        {
            if (char.IsWhiteSpace(c))
            {
                if (idx == 0 || inWhitespace)
                {
                    continue;
                }

                buffer[idx++] = ' ';
                inWhitespace = true;
                continue;
            }

            buffer[idx++] = c;
            inWhitespace = false;
        }

        if (idx == 0)
        {
            return null;
        }

        if (buffer[idx - 1] == ' ')
        {
            idx--;
        }

        return idx == 0 ? null : new string(buffer[..idx]);
    }
}


