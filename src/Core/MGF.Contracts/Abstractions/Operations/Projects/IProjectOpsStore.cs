namespace MGF.Contracts.Abstractions.Operations.Projects;

public interface IProjectOpsStore
{
    Task<ProjectInfo?> GetProjectAsync(
        string projectId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProjectStorageRootInfo>> GetProjectStorageRootsAsync(
        string projectId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProjectListItem>> ListProjectsAsync(
        int limit,
        CancellationToken cancellationToken = default);

    Task<int> UpdateProjectStatusAsync(
        string projectId,
        string statusKey,
        CancellationToken cancellationToken = default);

    Task<string?> GetClientNameAsync(
        string clientId,
        CancellationToken cancellationToken = default);

    Task<TestProjectInfo?> FindTestProjectAsync(
        string testKey,
        CancellationToken cancellationToken = default);

    Task<CreatedTestProject> CreateTestProjectAsync(
        CreateTestProjectRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ProjectInfo(
    string ProjectId,
    string ProjectCode,
    string ProjectName,
    string StatusKey,
    string DataProfile,
    string MetadataJson,
    string ClientId);

public sealed record ProjectStorageRootInfo(
    string ProjectStorageRootId,
    string StorageProviderKey,
    string RootKey,
    string FolderRelpath,
    bool IsPrimary,
    DateTimeOffset CreatedAtUtc);

public sealed record ProjectListItem(
    string ProjectId,
    string ProjectCode,
    string ProjectName);

public sealed record TestProjectInfo(
    string ProjectId,
    string ProjectCode,
    string ProjectName,
    string ClientId);

public sealed record CreateTestProjectRequest(
    string TestKey,
    string ClientName,
    string ProjectName,
    string EditorFirstName,
    string EditorLastName,
    string EditorInitials,
    string MetadataJson,
    string PersonId,
    string ClientId,
    string ProjectId,
    string ProjectMemberId);

public sealed record CreatedTestProject(
    string ProjectId,
    string ProjectCode,
    string ProjectName,
    string ClientId,
    string PersonId,
    string EditorInitials);
