namespace MGF.Contracts.Abstractions.Email;

public sealed record EmailProfile(
    string Key,
    IReadOnlyList<string> AllowedFrom,
    string? DefaultFromName,
    string? DefaultReplyTo,
    string? LogoUrl);
