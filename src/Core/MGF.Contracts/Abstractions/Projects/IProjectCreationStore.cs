namespace MGF.Contracts.Abstractions.Projects;

public interface IProjectCreationStore
{
    Task<bool> ClientExistsAsync(string clientId, CancellationToken cancellationToken = default);

    Task<bool> PersonExistsAsync(string personId, CancellationToken cancellationToken = default);

    Task<bool> PersonHasRoleAsync(
        string personId,
        string roleKey,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetPersonRolesAsync(
        string personId,
        CancellationToken cancellationToken = default);

    Task CreateProjectAsync(
        CreateProjectCommand command,
        CancellationToken cancellationToken = default);
}

public sealed record CreateProjectCommand(
    string ProjectId,
    string ClientId,
    string ProjectName,
    string EditorPersonId,
    string JobId,
    string PayloadJson,
    IReadOnlyList<string> AdditionalRoleKeys);
