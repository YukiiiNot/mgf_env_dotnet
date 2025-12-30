namespace MGF.Tools.DevSecrets;

using System.Diagnostics;

internal static class GitHelper
{
    public static async Task<string?> TryGetCommitAsync(string repoRoot, CancellationToken cancellationToken)
    {
        try
        {
            var result = await ProcessRunner.RunAsync(
                "git",
                new[] { "rev-parse", "--short", "HEAD" },
                repoRoot,
                cancellationToken);

            if (result.ExitCode != 0)
            {
                return null;
            }

            return result.StandardOutput.Trim();
        }
        catch
        {
            return null;
        }
    }
}
