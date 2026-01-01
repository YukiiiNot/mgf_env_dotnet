namespace MGF.Data.Stores.ProjectBootstrap;

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using MGF.Data.Data;
using MGF.Domain.Entities;

public sealed class ProjectBootstrapStore : IProjectBootstrapStore
{
    private readonly AppDbContext db;

    public ProjectBootstrapStore(AppDbContext db)
    {
        this.db = db;
    }

    public Task AppendProvisioningRunAsync(
        string projectId,
        JsonElement metadata,
        JsonElement runResult,
        CancellationToken cancellationToken = default)
    {
        var updatedJson = BootstrapMetadataUpdater.AppendProvisioningRun(metadata, runResult);
        return db.Database.ExecuteSqlInterpolatedAsync(
            ProjectBootstrapSql.BuildUpdateMetadataCommand(projectId, updatedJson),
            cancellationToken);
    }

    public Task UpdateProjectStatusAsync(
        string projectId,
        string statusKey,
        CancellationToken cancellationToken = default)
    {
        return db.Database.ExecuteSqlInterpolatedAsync(
            ProjectBootstrapSql.BuildUpdateStatusCommand(projectId, statusKey),
            cancellationToken);
    }

    public async Task<string?> UpsertProjectStorageRootAsync(
        string projectId,
        string storageProviderKey,
        string rootKey,
        string folderRelpath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

            var storageRootId = EntityIds.NewWithPrefix("psr");
            var parameters = new[]
            {
                new NpgsqlParameter("project_storage_root_id", storageRootId),
                new NpgsqlParameter("project_id", projectId),
                new NpgsqlParameter("storage_provider_key", storageProviderKey),
                new NpgsqlParameter("root_key", rootKey),
                new NpgsqlParameter("folder_relpath", folderRelpath),
            };

            await db.Database.ExecuteSqlRawAsync(
                ProjectBootstrapSql.UpsertStorageRoot,
                parameters,
                cancellationToken
            );

            await db.Database.ExecuteSqlRawAsync(
                ProjectBootstrapSql.UpdateIsPrimaryForProvider,
                parameters,
                cancellationToken
            );

            await transaction.CommitAsync(cancellationToken);
            return null;
        }
        catch (Exception ex)
        {
            return $"Storage root upsert failed: {ex.Message}";
        }
    }
}

internal static class BootstrapMetadataUpdater
{
    private const int MaxRunsToKeep = 10;

    private static readonly JsonSerializerOptions CamelCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    internal static string AppendProvisioningRun(JsonElement metadata, JsonElement runResult)
    {
        var root = JsonNode.Parse(metadata.GetRawText()) as JsonObject ?? new JsonObject();
        var provisioning = root["provisioning"] as JsonObject ?? new JsonObject();
        var runs = provisioning["runs"] as JsonArray ?? new JsonArray();

        var runNode = JsonNode.Parse(runResult.GetRawText());
        if (runNode is not null)
        {
            runs.Add(runNode);
        }

        while (runs.Count > MaxRunsToKeep)
        {
            runs.RemoveAt(0);
        }

        provisioning["runs"] = runs;
        root["provisioning"] = provisioning;

        return root.ToJsonString(CamelCaseOptions);
    }
}

internal static class ProjectBootstrapSql
{
    internal const string UpsertStorageRoot =
        """
        INSERT INTO public.project_storage_roots (
            project_storage_root_id,
            project_id,
            storage_provider_key,
            root_key,
            folder_relpath,
            share_url,
            is_primary
        )
        VALUES (
            @project_storage_root_id,
            @project_id,
            @storage_provider_key,
            @root_key,
            @folder_relpath,
            NULL,
            true
        )
        ON CONFLICT (project_id, storage_provider_key, root_key)
        DO UPDATE SET
            folder_relpath = EXCLUDED.folder_relpath,
            share_url = NULL,
            is_primary = true;
        """;

    internal const string UpdateIsPrimaryForProvider =
        """
        UPDATE public.project_storage_roots
        SET is_primary = CASE WHEN root_key = @root_key THEN true ELSE false END
        WHERE project_id = @project_id
          AND storage_provider_key = @storage_provider_key;
        """;

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
