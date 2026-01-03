namespace MGF.Contracts.Abstractions.Dropbox;

public interface IDropboxFilesClient
{
    Task EnsureFolderAsync(string accessToken, string dropboxPath, CancellationToken cancellationToken);
    Task UploadFileAsync(string accessToken, string dropboxPath, string localFilePath, CancellationToken cancellationToken);
    Task UploadBytesAsync(string accessToken, string dropboxPath, byte[] content, CancellationToken cancellationToken);
}
