namespace MGF.Contracts.Abstractions.ProjectArchive;

public interface IProjectArchiveExecutor
{
    Task<string> ResolveProjectFolderNameAsync(
        ProjectArchiveTokens tokens,
        CancellationToken cancellationToken = default);

    Task<ProjectArchiveDomainResult> ProcessDropboxAsync(
        ProjectArchivePayload payload,
        string projectFolderName,
        ProjectArchivePathTemplates pathTemplates,
        CancellationToken cancellationToken = default);

    Task<ProjectArchiveDomainResult> FinalizeDropboxArchiveAsync(
        ProjectArchiveDomainResult dropboxResult,
        ProjectArchivePayload payload,
        string projectFolderName,
        ProjectArchivePathTemplates pathTemplates,
        CancellationToken cancellationToken = default);

    Task<ProjectArchiveDomainResult> ProcessLucidlinkAsync(
        ProjectArchivePayload payload,
        string projectFolderName);

    Task<ProjectArchiveDomainResult> ProcessNasAsync(
        ProjectArchivePayload payload,
        string projectFolderName,
        ProjectArchiveDomainResult lucidlinkResult,
        ProjectArchiveTokens tokens,
        ProjectArchivePathTemplates pathTemplates,
        CancellationToken cancellationToken = default);
}
