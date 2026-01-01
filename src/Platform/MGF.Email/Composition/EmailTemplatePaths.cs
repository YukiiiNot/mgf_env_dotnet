namespace MGF.Email.Composition;

using System.Text.Json;
using Scriban;

internal static class EmailTemplatePaths
{
    private const string TemplatesFolderName = "Templates";

    public static string ResolveTemplatesRoot()
    {
        var baseDir = AppContext.BaseDirectory;
        var runtimePath = Path.Combine(baseDir, "Email", TemplatesFolderName);
        if (Directory.Exists(runtimePath) && HasTemplates(runtimePath))
        {
            return runtimePath;
        }

        throw new DirectoryNotFoundException($"Email templates folder not found at {runtimePath}.");
    }

    private static bool HasTemplates(string templatesRoot)
    {
        return Directory.EnumerateFiles(templatesRoot, "*.html", SearchOption.AllDirectories).Any()
            || Directory.EnumerateFiles(templatesRoot, "*.txt", SearchOption.AllDirectories).Any();
    }

    public static EmailTheme? TryLoadTheme(string templatesRoot)
    {
        var themePath = Path.Combine(templatesRoot, "theme.json");
        if (!File.Exists(themePath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(themePath);
            return JsonSerializer.Deserialize<EmailTheme>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch
        {
            return null;
        }
    }

    public static EmailTemplateSet LoadTemplates(string templatesRoot)
    {
        var templates = new Dictionary<string, Template>(StringComparer.OrdinalIgnoreCase);
        var sources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in Directory.GetFiles(templatesRoot, "*.html", SearchOption.AllDirectories)
                     .Concat(Directory.GetFiles(templatesRoot, "*.txt", SearchOption.AllDirectories)))
        {
            var relative = Path.GetRelativePath(templatesRoot, path).Replace('\\', '/');
            var content = File.ReadAllText(path);
            var template = Template.Parse(content);
            if (template.HasErrors)
            {
                var errors = string.Join("; ", template.Messages.Select(m => m.Message));
                throw new InvalidOperationException($"Email template parse error ({relative}): {errors}");
            }

            templates[relative] = template;
            sources[relative] = content;
        }

        return new EmailTemplateSet(sources, templates);
    }

    internal sealed record EmailTemplateSet(
        Dictionary<string, string> Sources,
        Dictionary<string, Template> Templates);
}
