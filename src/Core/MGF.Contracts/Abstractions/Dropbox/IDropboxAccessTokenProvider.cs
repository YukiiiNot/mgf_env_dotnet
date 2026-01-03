namespace MGF.Contracts.Abstractions.Dropbox;

public interface IDropboxAccessTokenProvider
{
    Task<DropboxAccessTokenResult> GetAccessTokenAsync(CancellationToken cancellationToken);
}

public sealed record DropboxAccessTokenResult(
    string? AccessToken,
    string AuthMode,
    string Source,
    string? Error);
