namespace MGF.FolderProvisioning;

public static class PathSafety
{
    private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();

    public static void EnsureSafeSegment(string segment, string context)
    {
        if (string.IsNullOrWhiteSpace(segment))
        {
            throw new InvalidOperationException($"Empty path segment in {context}.");
        }

        if (segment.IndexOfAny(InvalidFileNameChars) >= 0)
        {
            throw new InvalidOperationException($"Invalid characters in path segment '{segment}' ({context}).");
        }

        if (segment.Contains(Path.DirectorySeparatorChar) || segment.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new InvalidOperationException($"Path segment '{segment}' contains a separator ({context}).");
        }

        if (segment.Contains("..", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Path segment '{segment}' contains '..' ({context}).");
        }
    }

    public static void EnsureSafeRelativePath(string relativePath, string context)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new InvalidOperationException($"Empty relative path in {context}.");
        }

        if (Path.IsPathRooted(relativePath))
        {
            throw new InvalidOperationException($"Relative path '{relativePath}' is rooted ({context}).");
        }

        var parts = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        foreach (var part in parts)
        {
            if (string.Equals(part, "..", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Relative path '{relativePath}' contains '..' ({context}).");
            }
        }
    }
}


