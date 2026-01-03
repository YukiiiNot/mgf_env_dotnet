namespace MGF.Contracts.Abstractions.Dropbox;

public interface IDropboxShareLinkClient
{
    Task ValidateAccessTokenAsync(string accessToken, CancellationToken cancellationToken);

    Task<DropboxShareLinkResult> GetOrCreateSharedLinkAsync(
        string accessToken,
        string dropboxPath,
        CancellationToken cancellationToken);
}

public sealed record DropboxShareLinkResult(string Url, string? Id, bool IsNew);
