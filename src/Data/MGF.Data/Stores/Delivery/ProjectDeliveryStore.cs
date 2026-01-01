namespace MGF.Data.Stores.Delivery;

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using MGF.Data.Data;

public sealed class ProjectDeliveryStore : IProjectDeliveryStore
{
    private readonly AppDbContext db;

    public ProjectDeliveryStore(AppDbContext db)
    {
        this.db = db;
    }

    public Task AppendDeliveryRunAsync(
        string projectId,
        JsonElement metadata,
        JsonElement runResult,
        CancellationToken cancellationToken = default)
    {
        var updatedJson = DeliveryMetadataUpdater.AppendDeliveryRun(metadata, runResult);
        return db.Database.ExecuteSqlInterpolatedAsync(
            DeliverySql.BuildUpdateMetadataCommand(projectId, updatedJson),
            cancellationToken);
    }

    public Task AppendDeliveryEmailAsync(
        string projectId,
        JsonElement metadata,
        JsonElement emailResult,
        CancellationToken cancellationToken = default)
    {
        var updatedJson = DeliveryMetadataUpdater.AppendDeliveryEmail(metadata, emailResult);
        return db.Database.ExecuteSqlInterpolatedAsync(
            DeliverySql.BuildUpdateMetadataCommand(projectId, updatedJson),
            cancellationToken);
    }

    public Task UpdateProjectStatusAsync(
        string projectId,
        string statusKey,
        CancellationToken cancellationToken = default)
    {
        return db.Database.ExecuteSqlInterpolatedAsync(
            DeliverySql.BuildUpdateStatusCommand(projectId, statusKey),
            cancellationToken);
    }
}

internal static class DeliveryMetadataUpdater
{
    private const int MaxRunsToKeep = 10;

    private static readonly JsonSerializerOptions CamelCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    internal static string AppendDeliveryRun(JsonElement metadata, JsonElement runResult)
    {
        var root = JsonNode.Parse(metadata.GetRawText()) as JsonObject ?? new JsonObject();
        var delivery = root["delivery"] as JsonObject ?? new JsonObject();
        var runs = delivery["runs"] as JsonArray ?? new JsonArray();

        var runNode = ToNode(runResult);
        if (runNode is not null)
        {
            runs.Add(runNode);
        }

        while (runs.Count > MaxRunsToKeep)
        {
            runs.RemoveAt(0);
        }

        delivery["runs"] = runs;
        UpdateDeliveryCurrent(delivery, runResult);
        root["delivery"] = delivery;

        return root.ToJsonString(CamelCaseOptions);
    }

    internal static string AppendDeliveryEmail(JsonElement metadata, JsonElement emailResult)
    {
        var root = JsonNode.Parse(metadata.GetRawText()) as JsonObject ?? new JsonObject();
        var delivery = root["delivery"] as JsonObject ?? new JsonObject();
        var current = delivery["current"] as JsonObject ?? new JsonObject();

        ApplyLastEmail(current, emailResult);

        delivery["current"] = current;
        root["delivery"] = delivery;

        return root.ToJsonString(CamelCaseOptions);
    }

    private static void UpdateDeliveryCurrent(JsonObject delivery, JsonElement runResult)
    {
        var current = delivery["current"] as JsonObject ?? new JsonObject();

        var existingShareUrl = current["stableShareUrl"]?.GetValue<string>();
        var existingShareId = current["stableShareId"]?.GetValue<string>();

        var destinationPath = GetString(runResult, "destinationPath");
        if (!string.IsNullOrWhiteSpace(destinationPath))
        {
            current["stablePath"] = destinationPath;
        }

        var apiStablePath = GetString(runResult, "apiStablePath");
        if (!string.IsNullOrWhiteSpace(apiStablePath))
        {
            current["apiStablePath"] = apiStablePath;
        }

        var apiVersionPath = GetString(runResult, "apiVersionPath");
        if (!string.IsNullOrWhiteSpace(apiVersionPath))
        {
            current["apiVersionPath"] = apiVersionPath;
        }

        var versionLabel = GetString(runResult, "versionLabel");
        if (!string.IsNullOrWhiteSpace(versionLabel))
        {
            current["currentVersion"] = versionLabel;
        }

        var retentionUntilUtc = GetString(runResult, "retentionUntilUtc");
        if (!string.IsNullOrWhiteSpace(retentionUntilUtc))
        {
            current["retentionUntilUtc"] = retentionUntilUtc;
        }

        var shareUrl = GetString(runResult, "shareUrl");
        var shareId = GetString(runResult, "shareId");
        var resolvedShareUrl = !string.IsNullOrWhiteSpace(shareUrl) ? shareUrl : existingShareUrl;
        var resolvedShareId = !string.IsNullOrWhiteSpace(shareId) ? shareId : existingShareId;

        if (!string.IsNullOrWhiteSpace(resolvedShareUrl))
        {
            current["stableShareUrl"] = resolvedShareUrl;
            current["shareProviderKey"] = "dropbox";
        }

        if (!string.IsNullOrWhiteSpace(resolvedShareId))
        {
            current["stableShareId"] = resolvedShareId;
        }

        var shareStatus = GetString(runResult, "shareStatus");
        if (!string.IsNullOrWhiteSpace(shareStatus))
        {
            current["shareStatus"] = shareStatus;
        }

        var shareError = GetString(runResult, "shareError");
        if (!string.IsNullOrWhiteSpace(shareError))
        {
            current["shareError"] = shareError;
        }

        if (shareStatus is "created" or "reused")
        {
            current.Remove("shareError");
            current["lastShareVerifiedAtUtc"] = DateTimeOffset.UtcNow;
        }

        if (runResult.TryGetProperty("email", out var emailResult)
            && emailResult.ValueKind != JsonValueKind.Null
            && emailResult.ValueKind != JsonValueKind.Undefined)
        {
            ApplyLastEmail(current, emailResult);
        }

        delivery["current"] = current;
    }

    private static void ApplyLastEmail(JsonObject current, JsonElement emailResult)
    {
        var emailNode = ToNode(emailResult);
        if (emailNode is not null)
        {
            current["lastEmail"] = emailNode;
        }
    }

    private static JsonNode? ToNode(JsonElement element)
    {
        return element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
            ? null
            : JsonNode.Parse(element.GetRawText());
    }

    private static string? GetString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var prop))
        {
            return null;
        }

        return prop.ValueKind switch
        {
            JsonValueKind.String => prop.GetString(),
            JsonValueKind.Number => prop.GetRawText(),
            _ => null
        };
    }
}

internal static class DeliverySql
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
