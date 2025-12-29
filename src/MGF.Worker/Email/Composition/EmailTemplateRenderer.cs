namespace MGF.Worker.Email.Composition;

using System.Net;
using System.Text.Json;
using Scriban;
using Scriban.Parsing;
using Scriban.Runtime;

internal sealed class EmailTemplateRenderer
{
    private readonly EmailTheme theme;
    private readonly EmailTemplatePaths.EmailTemplateSet templates;

    private EmailTemplateRenderer(EmailTheme theme, EmailTemplatePaths.EmailTemplateSet templates)
    {
        this.theme = theme;
        this.templates = templates;
    }

    public static EmailTemplateRenderer CreateDefault()
    {
        var templatesRoot = EmailTemplatePaths.ResolveTemplatesRoot();
        var theme = EmailTemplatePaths.TryLoadTheme(templatesRoot) ?? EmailTheme.Default;
        var templates = EmailTemplatePaths.LoadTemplates(templatesRoot);
        return new EmailTemplateRenderer(theme, templates);
    }

    public string RenderHtml(string templateName, DeliveryReadyEmailContext context)
    {
        var model = BuildModel(context, theme, htmlEncode: true);
        return Render(templateName, model);
    }

    public string RenderText(string templateName, DeliveryReadyEmailContext context)
    {
        var model = BuildModel(context, theme, htmlEncode: false);
        return Render(templateName, model);
    }

    private string Render(string templateName, Dictionary<string, object?> model)
    {
        if (!templates.Templates.TryGetValue(templateName, out var template))
        {
            throw new InvalidOperationException($"Email template '{templateName}' not found.");
        }

        var scriptObject = new ScriptObject();
        scriptObject.Import(model);

        var context = new TemplateContext
        {
            StrictVariables = true,
            TemplateLoader = new InMemoryTemplateLoader(templates.Sources)
        };
        context.PushGlobal(scriptObject);

        return template.Render(context);
    }

    private static Dictionary<string, object?> BuildModel(
        DeliveryReadyEmailContext context,
        EmailTheme theme,
        bool htmlEncode)
    {
        var projectCode = context.Tokens.ProjectCode ?? "MGF";
        var projectName = context.Tokens.ProjectName ?? "Delivery";
        var projectLine = $"{projectCode} - {projectName}";
        var retention = context.RetentionUntilUtc.ToString("yyyy-MM-dd");
        var showCountNote = context.Files.Count > 50
            ? $"Showing 50 of {context.Files.Count} files."
            : string.Empty;
        var files = context.Files
            .Take(50)
            .Select(file => new Dictionary<string, object?>
            {
                ["relative_path"] = file.RelativePath
            })
            .ToArray();

        if (htmlEncode)
        {
            projectCode = WebUtility.HtmlEncode(projectCode);
            projectName = WebUtility.HtmlEncode(projectName);
            projectLine = WebUtility.HtmlEncode(projectLine);
            retention = WebUtility.HtmlEncode(retention);
            showCountNote = WebUtility.HtmlEncode(showCountNote);

            foreach (var file in files)
            {
                var value = file["relative_path"] as string ?? string.Empty;
                file["relative_path"] = WebUtility.HtmlEncode(value);
            }
        }

        var shareUrl = htmlEncode
            ? WebUtility.HtmlEncode(context.ShareUrl)
            : context.ShareUrl;
        var versionLabel = htmlEncode
            ? WebUtility.HtmlEncode(context.VersionLabel)
            : context.VersionLabel;
        var logoUrl = string.IsNullOrWhiteSpace(context.LogoUrl)
            ? string.Empty
            : (htmlEncode ? WebUtility.HtmlEncode(context.LogoUrl) : context.LogoUrl);

        return new Dictionary<string, object?>
        {
            ["project_code"] = projectCode,
            ["project_name"] = projectName,
            ["project_line"] = projectLine,
            ["share_url"] = shareUrl,
            ["version_label"] = versionLabel,
            ["retention_until"] = retention,
            ["files"] = files,
            ["file_count_note"] = showCountNote,
            ["logo_url"] = logoUrl,
            ["font_stack"] = htmlEncode ? WebUtility.HtmlEncode(theme.FontStack) : theme.FontStack,
            ["background_color"] = theme.BackgroundColor,
            ["card_background"] = theme.CardBackgroundColor,
            ["text_color"] = theme.TextColor,
            ["muted_text_color"] = theme.MutedTextColor,
            ["link_color"] = theme.LinkColor,
            ["button_background"] = theme.ButtonBackground,
            ["button_text_color"] = theme.ButtonTextColor
        };
    }

    private sealed class InMemoryTemplateLoader : ITemplateLoader
    {
        private readonly Dictionary<string, string> templates;

        public InMemoryTemplateLoader(Dictionary<string, string> templates)
        {
            this.templates = templates;
        }

        public string GetPath(TemplateContext context, SourceSpan callerSpan, string templateName)
        {
            return templateName;
        }

        public string Load(TemplateContext context, SourceSpan callerSpan, string templatePath)
        {
            if (!templates.TryGetValue(templatePath, out var template))
            {
                throw new InvalidOperationException($"Email template include '{templatePath}' not found.");
            }

            return template;
        }

        public ValueTask<string> LoadAsync(TemplateContext context, SourceSpan callerSpan, string templatePath)
        {
            return new ValueTask<string>(Load(context, callerSpan, templatePath));
        }
    }
}
