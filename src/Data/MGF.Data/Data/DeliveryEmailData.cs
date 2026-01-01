namespace MGF.Data.Data;

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using MGF.Contracts.Abstractions;

public sealed class DeliveryEmailData : IDeliveryEmailData
{
    private readonly AppDbContext db;

    public DeliveryEmailData(AppDbContext db)
    {
        this.db = db;
    }

    public async Task<DeliveryEmailProject?> GetProjectAsync(
        string projectId,
        CancellationToken cancellationToken = default)
    {
        var project = await db.Projects.AsNoTracking()
            .SingleOrDefaultAsync(p => p.ProjectId == projectId, cancellationToken);

        if (project is null)
        {
            return null;
        }

        var client = await db.Clients.AsNoTracking()
            .SingleOrDefaultAsync(c => c.ClientId == project.ClientId, cancellationToken);

        var recipients = await ResolveCanonicalRecipientsAsync(
            project.ClientId,
            client?.PrimaryContactPersonId,
            cancellationToken);

        return new DeliveryEmailProject(
            project.ProjectId,
            project.ProjectCode,
            project.Name,
            project.ClientId,
            client?.DisplayName,
            project.Metadata,
            recipients);
    }

    public async Task RecordDeliveryEmailSentAsync(
        string projectId,
        JsonElement metadata,
        DeliveryEmailAudit emailResult,
        CancellationToken cancellationToken = default)
    {
        var root = JsonNode.Parse(metadata.GetRawText()) as JsonObject ?? new JsonObject();
        var delivery = root["delivery"] as JsonObject ?? new JsonObject();
        var current = delivery["current"] as JsonObject ?? new JsonObject();

        ApplyLastEmail(current, emailResult);

        delivery["current"] = current;
        root["delivery"] = delivery;

        var updatedJson = root.ToJsonString(new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE public.projects
            SET metadata = {updatedJson}::jsonb,
                updated_at = now()
            WHERE project_id = {projectId};
            """,
            cancellationToken);
    }

    private static void ApplyLastEmail(JsonObject current, DeliveryEmailAudit emailResult)
    {
        var emailNode = JsonSerializer.SerializeToNode(emailResult, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        if (emailNode is not null)
        {
            current["lastEmail"] = emailNode;
        }
    }

    private async Task<IReadOnlyList<string>> ResolveCanonicalRecipientsAsync(
        string clientId,
        string? primaryContactPersonId,
        CancellationToken cancellationToken)
    {
        var recipients = new List<string>();
        var personContacts = db.Set<Dictionary<string, object>>("person_contacts").AsNoTracking();

        if (!string.IsNullOrWhiteSpace(primaryContactPersonId))
        {
            var primaryEmail = await personContacts
                .Where(row => EF.Property<string>(row, "person_id") == primaryContactPersonId)
                .Select(row => EF.Property<string?>(row, "email"))
                .SingleOrDefaultAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(primaryEmail))
            {
                recipients.Add(primaryEmail);
                return recipients;
            }
        }

        var clientContacts = db.Set<Dictionary<string, object>>("client_contacts").AsNoTracking();
        var primaryContactIds = await clientContacts
            .Where(row => EF.Property<string>(row, "client_id") == clientId
                && EF.Property<bool>(row, "is_primary"))
            .Select(row => EF.Property<string>(row, "person_id"))
            .ToListAsync(cancellationToken);

        if (primaryContactIds.Count == 0)
        {
            return recipients;
        }

        var emails = await personContacts
            .Where(row => primaryContactIds.Contains(EF.Property<string>(row, "person_id")))
            .Select(row => EF.Property<string?>(row, "email"))
            .ToListAsync(cancellationToken);

        recipients.AddRange(emails
            .Where(email => !string.IsNullOrWhiteSpace(email))
            .Select(email => email!));
        return recipients;
    }
}
