namespace MGF.Contracts.Abstractions.ProjectArchive;

using System.Text.Json;

public interface IProjectArchiveStore
{
    Task AppendArchiveRunAsync(
        string projectId,
        JsonElement metadata,
        JsonElement runResult,
        CancellationToken cancellationToken = default);

    Task UpdateProjectStatusAsync(
        string projectId,
        string statusKey,
        CancellationToken cancellationToken = default);
}
