namespace MGF.Worker.Email.Composition;

internal sealed record EmailTheme(
    string FontStack,
    string BackgroundColor,
    string CardBackgroundColor,
    string TextColor,
    string MutedTextColor,
    string LinkColor,
    string ButtonBackground,
    string ButtonTextColor,
    string HeadlineSize,
    string HeadlineLineHeight,
    string HeadlineTracking,
    string SectionLabelSize,
    string SectionLabelTracking,
    string BodySize,
    string BodyLineHeight,
    string StrongWeight,
    string NormalWeight,
    string OuterPaddingY,
    string OuterPaddingX,
    string ContentPaddingY,
    string ContentPaddingX,
    string SectionGapSm,
    string SectionGapMd,
    string SectionGapLg,
    string RuleColor,
    string RuleThickness,
    string ButtonPaddingY,
    string ButtonPaddingX,
    string ButtonFontSize,
    string ButtonWeight,
    string ButtonBorderRadius,
    string MaxWidth,
    string LogoWidth)
{
    public static EmailTheme Default => new(
        FontStack: "'Glacial Indifference','Garet','Segoe UI',Arial,sans-serif",
        BackgroundColor: "#0B0B0B",
        CardBackgroundColor: "#0B0B0B",
        TextColor: "#FAF9F7",
        MutedTextColor: "#A6A6A6",
        LinkColor: "#FAF9F7",
        ButtonBackground: "#FAF9F7",
        ButtonTextColor: "#0B0B0B",
        HeadlineSize: "48px",
        HeadlineLineHeight: "0.95",
        HeadlineTracking: "2px",
        SectionLabelSize: "12px",
        SectionLabelTracking: "1.6px",
        BodySize: "13px",
        BodyLineHeight: "1.6",
        StrongWeight: "800",
        NormalWeight: "600",
        OuterPaddingY: "40px",
        OuterPaddingX: "16px",
        ContentPaddingY: "40px",
        ContentPaddingX: "40px",
        SectionGapSm: "10px",
        SectionGapMd: "18px",
        SectionGapLg: "26px",
        RuleColor: "#2A2A2A",
        RuleThickness: "1px",
        ButtonPaddingY: "12px",
        ButtonPaddingX: "20px",
        ButtonFontSize: "14px",
        ButtonWeight: "800",
        ButtonBorderRadius: "0px",
        MaxWidth: "600",
        LogoWidth: "180");

    public static EmailTheme ApplyDefaults(EmailTheme? theme)
    {
        var fallback = Default;
        if (theme is null)
        {
            return fallback;
        }

        return new EmailTheme(
            FontStack: Coalesce(theme.FontStack, fallback.FontStack),
            BackgroundColor: Coalesce(theme.BackgroundColor, fallback.BackgroundColor),
            CardBackgroundColor: Coalesce(theme.CardBackgroundColor, fallback.CardBackgroundColor),
            TextColor: Coalesce(theme.TextColor, fallback.TextColor),
            MutedTextColor: Coalesce(theme.MutedTextColor, fallback.MutedTextColor),
            LinkColor: Coalesce(theme.LinkColor, fallback.LinkColor),
            ButtonBackground: Coalesce(theme.ButtonBackground, fallback.ButtonBackground),
            ButtonTextColor: Coalesce(theme.ButtonTextColor, fallback.ButtonTextColor),
            HeadlineSize: Coalesce(theme.HeadlineSize, fallback.HeadlineSize),
            HeadlineLineHeight: Coalesce(theme.HeadlineLineHeight, fallback.HeadlineLineHeight),
            HeadlineTracking: Coalesce(theme.HeadlineTracking, fallback.HeadlineTracking),
            SectionLabelSize: Coalesce(theme.SectionLabelSize, fallback.SectionLabelSize),
            SectionLabelTracking: Coalesce(theme.SectionLabelTracking, fallback.SectionLabelTracking),
            BodySize: Coalesce(theme.BodySize, fallback.BodySize),
            BodyLineHeight: Coalesce(theme.BodyLineHeight, fallback.BodyLineHeight),
            StrongWeight: Coalesce(theme.StrongWeight, fallback.StrongWeight),
            NormalWeight: Coalesce(theme.NormalWeight, fallback.NormalWeight),
            OuterPaddingY: Coalesce(theme.OuterPaddingY, fallback.OuterPaddingY),
            OuterPaddingX: Coalesce(theme.OuterPaddingX, fallback.OuterPaddingX),
            ContentPaddingY: Coalesce(theme.ContentPaddingY, fallback.ContentPaddingY),
            ContentPaddingX: Coalesce(theme.ContentPaddingX, fallback.ContentPaddingX),
            SectionGapSm: Coalesce(theme.SectionGapSm, fallback.SectionGapSm),
            SectionGapMd: Coalesce(theme.SectionGapMd, fallback.SectionGapMd),
            SectionGapLg: Coalesce(theme.SectionGapLg, fallback.SectionGapLg),
            RuleColor: Coalesce(theme.RuleColor, fallback.RuleColor),
            RuleThickness: Coalesce(theme.RuleThickness, fallback.RuleThickness),
            ButtonPaddingY: Coalesce(theme.ButtonPaddingY, fallback.ButtonPaddingY),
            ButtonPaddingX: Coalesce(theme.ButtonPaddingX, fallback.ButtonPaddingX),
            ButtonFontSize: Coalesce(theme.ButtonFontSize, fallback.ButtonFontSize),
            ButtonWeight: Coalesce(theme.ButtonWeight, fallback.ButtonWeight),
            ButtonBorderRadius: Coalesce(theme.ButtonBorderRadius, fallback.ButtonBorderRadius),
            MaxWidth: Coalesce(theme.MaxWidth, fallback.MaxWidth),
            LogoWidth: Coalesce(theme.LogoWidth, fallback.LogoWidth)
        );
    }

    private static string Coalesce(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value;
}
