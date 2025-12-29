namespace MGF.Worker.Email.Composition;

internal sealed record EmailTheme(
    string FontStack,
    string BackgroundColor,
    string CardBackgroundColor,
    string TextColor,
    string MutedTextColor,
    string LinkColor,
    string ButtonBackground,
    string ButtonTextColor)
{
    public static EmailTheme Default => new(
        FontStack: "'Glacial Indifference','Garet','Segoe UI',Arial,sans-serif",
        BackgroundColor: "#2b2b2b",
        CardBackgroundColor: "#111111",
        TextColor: "#f5f5f5",
        MutedTextColor: "#b3b3b3",
        LinkColor: "#3d7bff",
        ButtonBackground: "#3d7bff",
        ButtonTextColor: "#ffffff");
}
