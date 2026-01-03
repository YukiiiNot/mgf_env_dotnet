namespace MGF.Email.Composition;

using System.Net;
using System.Text.Json;
using Scriban;
using Scriban.Parsing;
using Scriban.Runtime;

public sealed class EmailTemplateRenderer
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
        var theme = EmailTheme.ApplyDefaults(EmailTemplatePaths.TryLoadTheme(templatesRoot));
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
        var preheaderText = "Your deliverables are ready.";
        var headlineLine1 = "Deliverables";
        var headlineLine2 = "Ready";
        var ctaLabel = "Download deliverables";
        var supportEmail = "info@mgfilms.pro";
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
            preheaderText = WebUtility.HtmlEncode(preheaderText);
            headlineLine1 = WebUtility.HtmlEncode(headlineLine1);
            headlineLine2 = WebUtility.HtmlEncode(headlineLine2);
            ctaLabel = WebUtility.HtmlEncode(ctaLabel);
            supportEmail = WebUtility.HtmlEncode(supportEmail);
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
            ["button_text_color"] = theme.ButtonTextColor,
            ["headline_size"] = theme.HeadlineSize,
            ["headline_line_height"] = theme.HeadlineLineHeight,
            ["headline_tracking"] = theme.HeadlineTracking,
            ["section_label_size"] = theme.SectionLabelSize,
            ["section_label_tracking"] = theme.SectionLabelTracking,
            ["body_size"] = theme.BodySize,
            ["body_line_height"] = theme.BodyLineHeight,
            ["strong_weight"] = theme.StrongWeight,
            ["normal_weight"] = theme.NormalWeight,
            ["outer_padding_y"] = theme.OuterPaddingY,
            ["outer_padding_x"] = theme.OuterPaddingX,
            ["content_padding_y"] = theme.ContentPaddingY,
            ["content_padding_x"] = theme.ContentPaddingX,
            ["section_gap_sm"] = theme.SectionGapSm,
            ["section_gap_md"] = theme.SectionGapMd,
            ["section_gap_lg"] = theme.SectionGapLg,
            ["rule_color"] = theme.RuleColor,
            ["rule_thickness"] = theme.RuleThickness,
            ["button_padding_y"] = theme.ButtonPaddingY,
            ["button_padding_x"] = theme.ButtonPaddingX,
            ["button_font_size"] = theme.ButtonFontSize,
            ["button_weight"] = theme.ButtonWeight,
            ["button_border_radius"] = theme.ButtonBorderRadius,
            ["max_width"] = theme.MaxWidth,
            ["logo_width"] = theme.LogoWidth,
            ["preheader_text"] = preheaderText,
            ["headline_line1"] = headlineLine1,
            ["headline_line2"] = headlineLine2,
            ["cta_label"] = ctaLabel,
            ["support_email"] = supportEmail
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
