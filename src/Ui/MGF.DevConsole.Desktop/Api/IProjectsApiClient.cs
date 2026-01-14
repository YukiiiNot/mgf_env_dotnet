namespace MGF.DevConsole.Desktop.Api;

public interface IProjectsApiClient
{
    Task<ProjectsApiClient.ProjectsListResponseDto> GetProjectsAsync(
        int limit,
        DateTimeOffset? since,
        DateTimeOffset? cursorCreatedAt,
        string? cursorProjectId,
        CancellationToken cancellationToken);

    Task<ProjectsApiClient.ProjectDetailDto> GetProjectAsync(string projectId, CancellationToken cancellationToken);
}
