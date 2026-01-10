namespace MGF.DevConsole.Desktop.Api;

using System.Net;
using System.Net.Http;
using System.Net.Http.Json;

public sealed class ProjectsApiClient
{
    private readonly HttpClient httpClient;

    public ProjectsApiClient(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    public sealed record ProjectListItemDto(
        string ProjectId,
        string ProjectCode,
        string ClientId,
        string Name,
        string StatusKey,
        string PhaseKey,
        string? PriorityKey,
        DateOnly? DueDate,
        DateTimeOffset? ArchivedAt,
        DateTimeOffset CreatedAt
    );

    public sealed record ProjectsListCursorDto(
        DateTimeOffset CreatedAt,
        string ProjectId
    );

    public sealed record ProjectsListResponseDto(
        IReadOnlyList<ProjectListItemDto> Items,
        ProjectsListCursorDto? NextCursor
    );

    public sealed record ProjectDetailDto(
        string ProjectId,
        string ProjectCode,
        string ClientId,
        string Name,
        string StatusKey,
        string PhaseKey,
        string? PriorityKey,
        DateOnly? DueDate,
        DateTimeOffset? ArchivedAt,
        string DataProfile,
        string? CurrentInvoiceId,
        DateTimeOffset CreatedAt,
        DateTimeOffset? UpdatedAt
    );

    public async Task<ProjectsListResponseDto> GetProjectsAsync(
        int limit,
        ProjectsListCursorDto? cursor,
        CancellationToken cancellationToken)
    {
        var url = BuildProjectsListUrl(limit, cursor);
        using var response = await httpClient.GetAsync(url, cancellationToken);
        return await ReadResponseAsync<ProjectsListResponseDto>(response, cancellationToken, "projects list");
    }

    public async Task<ProjectDetailDto> GetProjectAsync(string projectId, CancellationToken cancellationToken)
    {
        var encodedId = Uri.EscapeDataString(projectId);
        using var response = await httpClient.GetAsync($"api/projects/{encodedId}", cancellationToken);
        return await ReadResponseAsync<ProjectDetailDto>(response, cancellationToken, "project detail");
    }

    private static string BuildProjectsListUrl(int limit, ProjectsListCursorDto? cursor)
    {
        var query = new List<string> { $"limit={limit}" };

        if (cursor is not null)
        {
            query.Add($"cursorCreatedAt={Uri.EscapeDataString(cursor.CreatedAt.ToString("O"))}");
            query.Add($"cursorProjectId={Uri.EscapeDataString(cursor.ProjectId)}");
        }

        return "api/projects?" + string.Join("&", query);
    }

    private static async Task<T> ReadResponseAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken,
        string operation)
    {
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new ProjectsApiException(ProjectsApiFailure.Unauthorized, "Unauthorized (X-MGF-API-KEY rejected).");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new ProjectsApiException(
                ProjectsApiFailure.HttpError,
                $"Unexpected status code {(int)response.StatusCode} during {operation}.");
        }

        var result = await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
        if (result is null)
        {
            throw new ProjectsApiException(ProjectsApiFailure.InvalidResponse, $"Missing or invalid {operation} response.");
        }

        return result;
    }
}

public sealed class ProjectsApiException : Exception
{
    public ProjectsApiException(ProjectsApiFailure failure, string message)
        : base(message)
    {
        Failure = failure;
    }

    public ProjectsApiFailure Failure { get; }
}

public enum ProjectsApiFailure
{
    Unauthorized,
    HttpError,
    InvalidResponse
}
