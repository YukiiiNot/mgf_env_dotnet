namespace MGF.Worker.Email.Models;

public sealed record EmailProfile(
    string Key,
    IReadOnlyList<string> AllowedFrom,
    string? DefaultFromName,
    string? DefaultReplyTo,
    string? LogoUrl);
