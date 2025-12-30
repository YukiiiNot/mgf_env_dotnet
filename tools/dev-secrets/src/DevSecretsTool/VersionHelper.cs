namespace MGF.Tools.DevSecrets;

using System.Reflection;

internal static class VersionHelper
{
    public static string GetToolVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return version?.ToString() ?? "0.0.0";
    }
}
