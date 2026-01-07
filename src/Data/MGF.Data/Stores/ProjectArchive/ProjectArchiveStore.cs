namespace MGF.Data.Stores.ProjectArchive;

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using MGF.Contracts.Abstractions.ProjectArchive;
using MGF.Data.Data;

public sealed class ProjectArchiveStore : IProjectArchiveStore
{
    private readonly AppDbContext db;

    public ProjectArchiveStore(AppDbContext db)
    {
        this.db = db;
    }

    public Task AppendArchiveRunAsync(
        string projectId,
        JsonElement metadata,
        JsonElement runResult,
        CancellationToken cancellationToken = default)
    {
        var updatedJson = ProjectArchiveMetadataUpdater.AppendArchiveRun(metadata, runResult);
        return db.Database.ExecuteSqlInterpolatedAsync(
            ProjectArchiveSql.BuildUpdateMetadataCommand(projectId, updatedJson),
            cancellationToken);
    }

    public Task UpdateProjectStatusAsync(
        string projectId,
        string statusKey,
        CancellationToken cancellationToken = default)
    {
        return db.Database.ExecuteSqlInterpolatedAsync(
            ProjectArchiveSql.BuildUpdateStatusCommand(projectId, statusKey),
            cancellationToken);
    }
}

internal static class ProjectArchiveMetadataUpdater
{
    private const int MaxRunsToKeep = 10;

    private static readonly JsonSerializerOptions CamelCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    internal static string AppendArchiveRun(JsonElement metadata, JsonElement runResult)
    {
        var root = JsonNode.Parse(metadata.GetRawText()) as JsonObject ?? new JsonObject();
        var archiving = root["archiving"] as JsonObject ?? new JsonObject();
        var runs = archiving["runs"] as JsonArray ?? new JsonArray();

        var runNode = JsonSerializer.SerializeToNode(runResult, CamelCaseOptions);
        if (runNode is not null)
        {
            runs.Add(runNode);
        }

        while (runs.Count > MaxRunsToKeep)
        {
            runs.RemoveAt(0);
        }

        archiving["runs"] = runs;
        root["archiving"] = archiving;

        return root.ToJsonString(CamelCaseOptions);
    }
}

internal static class ProjectArchiveSql
{
    internal static FormattableString BuildUpdateMetadataCommand(string projectId, string updatedJson)
    {
        return $"""
        UPDATE public.projects
        SET metadata = {updatedJson}::jsonb,
            updated_at = now()
        WHERE project_id = {projectId};
        """;
    }

    internal static FormattableString BuildUpdateStatusCommand(string projectId, string statusKey)
    {
        return $"""
        UPDATE public.projects
        SET status_key = {statusKey},
            updated_at = now()
        WHERE project_id = {projectId};
        """;
    }
}
