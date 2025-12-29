namespace MGF.Worker.ProjectDelivery;

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MGF.Infrastructure.Data;
using MGF.Tools.Provisioner;
using MGF.Worker.Integrations.Email;

public sealed class ProjectDeliveryEmailer
{
    private const string DefaultReplyToAddress = "info@mgfilms.pro";

    private readonly IConfiguration configuration;
    private readonly IEmailSender emailSender;
    private readonly ILogger? logger;

    public ProjectDeliveryEmailer(
        IConfiguration configuration,
        IEmailSender? emailSender = null,
        ILogger? logger = null)
    {
        this.configuration = configuration;
        this.emailSender = emailSender ?? EmailSenderFactory.Create(configuration, logger);
        this.logger = logger;
    }

    public async Task<DeliveryEmailResult> RunAsync(
        AppDbContext db,
        ProjectDeliveryEmailPayload payload,
        string jobId,
        CancellationToken cancellationToken)
    {
        var project = await db.Projects.AsNoTracking()
            .SingleOrDefaultAsync(p => p.ProjectId == payload.ProjectId, cancellationToken);

        if (project is null)
        {
            throw new InvalidOperationException($"Project not found: {payload.ProjectId}");
        }

        var current = ReadDeliveryCurrent(project.Metadata);
        if (string.IsNullOrWhiteSpace(current.StablePath))
        {
            var failed = BuildFailure(payload.ToEmails, "Delivery stable path missing; run delivery first.");
            await ProjectDeliverer.AppendDeliveryEmailAsync(db, project.ProjectId, project.Metadata, failed, cancellationToken);
            return failed;
        }

        if (string.IsNullOrWhiteSpace(current.ShareUrl))
        {
            var failed = BuildFailure(payload.ToEmails, "Stable Dropbox share link missing; run delivery first.");
            await ProjectDeliverer.AppendDeliveryEmailAsync(db, project.ProjectId, project.Metadata, failed, cancellationToken);
            return failed;
        }

        if (!TryResolveContainerRoot(current.StablePath, out var containerRoot, out var resolveError))
        {
            var failed = BuildFailure(payload.ToEmails, resolveError);
            await ProjectDeliverer.AppendDeliveryEmailAsync(db, project.ProjectId, project.Metadata, failed, cancellationToken);
            return failed;
        }

        var manifestPath = ProjectDeliverer.ResolveDeliveryManifestPath(containerRoot);
        if (!File.Exists(manifestPath))
        {
            var failed = BuildFailure(payload.ToEmails, $"Delivery manifest not found: {manifestPath}");
            await ProjectDeliverer.AppendDeliveryEmailAsync(db, project.ProjectId, project.Metadata, failed, cancellationToken);
            return failed;
        }

        DeliveryManifest? manifest;
        try
        {
            var json = await File.ReadAllTextAsync(manifestPath, cancellationToken);
            manifest = JsonSerializer.Deserialize<DeliveryManifest>(json, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            var failed = BuildFailure(payload.ToEmails, $"Failed to read delivery manifest: {ex.Message}");
            await ProjectDeliverer.AppendDeliveryEmailAsync(db, project.ProjectId, project.Metadata, failed, cancellationToken);
            return failed;
        }

        if (manifest is null)
        {
            var failed = BuildFailure(payload.ToEmails, "Delivery manifest is empty or invalid.");
            await ProjectDeliverer.AppendDeliveryEmailAsync(db, project.ProjectId, project.Metadata, failed, cancellationToken);
            return failed;
        }

        var clientName = await db.Clients.AsNoTracking()
            .Where(c => c.ClientId == project.ClientId)
            .Select(c => c.DisplayName)
            .SingleOrDefaultAsync(cancellationToken);

        var tokens = ProvisioningTokens.Create(project.ProjectCode, project.Name, clientName, payload.EditorInitials);
        var subject = ProjectDeliverer.BuildDeliverySubject(tokens);
        var replyTo = !string.IsNullOrWhiteSpace(payload.ReplyToEmail)
            ? payload.ReplyToEmail
            : DefaultReplyToAddress;

        var recipients = NormalizeEmailList(payload.ToEmails);
        if (recipients.Count == 0)
        {
            var failed = BuildFailure(recipients, "No delivery email recipients were provided.");
            await ProjectDeliverer.AppendDeliveryEmailAsync(db, project.ProjectId, project.Metadata, failed, cancellationToken);
            return failed;
        }

        var files = manifest.Files ?? Array.Empty<DeliveryFileSummary>();
        var versionLabel = string.IsNullOrWhiteSpace(manifest.CurrentVersion) ? "v1" : manifest.CurrentVersion;
        var retentionUntil = manifest.RetentionUntilUtc == default
            ? DateTimeOffset.UtcNow.AddMonths(3)
            : manifest.RetentionUntilUtc;
        var request = ProjectDeliverer.BuildDeliveryEmailRequest(
            subject,
            current.ShareUrl,
            versionLabel,
            retentionUntil,
            files,
            recipients,
            replyTo);

        DeliveryEmailResult result;
        try
        {
            result = await emailSender.SendAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            result = BuildFailure(recipients, $"Delivery email failed: {ex.Message}");
        }

        await ProjectDeliverer.AppendDeliveryEmailAsync(db, project.ProjectId, project.Metadata, result, cancellationToken);
        logger?.LogInformation("MGF.Worker: project.delivery_email completed (job_id={JobId}, project_id={ProjectId}, status={Status})", jobId, payload.ProjectId, result.Status);
        return result;
    }

    public static ProjectDeliveryEmailPayload ParsePayload(string payloadJson)
    {
        using var doc = JsonDocument.Parse(payloadJson);
        var root = doc.RootElement;

        var projectId = TryGetString(root, "projectId");
        if (string.IsNullOrWhiteSpace(projectId))
        {
            throw new InvalidOperationException("projectId is required in project.delivery_email payload.");
        }

        var editorInitials = ReadEditorInitials(root);
        var toEmails = ReadEmailAddresses(root, "toEmails");
        var replyToEmail = TryGetString(root, "replyToEmail");

        return new ProjectDeliveryEmailPayload(
            ProjectId: projectId,
            EditorInitials: editorInitials,
            ToEmails: toEmails,
            ReplyToEmail: replyToEmail
        );
    }

    private static DeliveryCurrentState ReadDeliveryCurrent(JsonElement metadata)
    {
        try
        {
            using var doc = JsonDocument.Parse(metadata.GetRawText());
            if (!doc.RootElement.TryGetProperty("delivery", out var delivery))
            {
                return new DeliveryCurrentState(null, null, null, null);
            }

            if (!delivery.TryGetProperty("current", out var current))
            {
                return new DeliveryCurrentState(null, null, null, null);
            }

            return new DeliveryCurrentState(
                StablePath: TryGetString(current, "stablePath"),
                ShareUrl: TryGetString(current, "stableShareUrl") ?? TryGetString(current, "shareUrl"),
                CurrentVersion: TryGetString(current, "currentVersion"),
                RetentionUntilUtc: TryGetDateTimeOffset(current, "retentionUntilUtc")
            );
        }
        catch
        {
            return new DeliveryCurrentState(null, null, null, null);
        }
    }

    private static bool TryResolveContainerRoot(
        string stablePath,
        out string containerRoot,
        out string error)
    {
        containerRoot = string.Empty;
        error = string.Empty;

        var trimmed = stablePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!ProjectDeliverer.IsStableSharePath(trimmed))
        {
            error = "Stable path resolves to a version folder; cannot send delivery email.";
            return false;
        }

        var finalSegment = Path.GetFileName(trimmed);
        if (!string.Equals(finalSegment, "Final", StringComparison.OrdinalIgnoreCase))
        {
            error = $"Stable path does not point to Final folder: {stablePath}";
            return false;
        }

        var deliverablesDir = Path.GetDirectoryName(trimmed);
        if (string.IsNullOrWhiteSpace(deliverablesDir))
        {
            error = "Delivery path missing deliverables parent.";
            return false;
        }

        var deliverablesSegment = Path.GetFileName(deliverablesDir);
        if (!string.Equals(deliverablesSegment, "01_Deliverables", StringComparison.OrdinalIgnoreCase))
        {
            error = "Delivery path missing 01_Deliverables segment.";
            return false;
        }

        var container = Path.GetDirectoryName(deliverablesDir);
        if (string.IsNullOrWhiteSpace(container))
        {
            error = "Delivery container root could not be resolved.";
            return false;
        }

        containerRoot = container;
        return true;
    }

    private static IReadOnlyList<string> NormalizeEmailList(IEnumerable<string> values)
    {
        return values
            .SelectMany(value => (value ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static DeliveryEmailResult BuildFailure(IReadOnlyList<string> recipients, string error)
    {
        return new DeliveryEmailResult(
            Status: "failed",
            Provider: "email",
            FromAddress: "deliveries@mgfilms.pro",
            To: recipients,
            Subject: "MGF Delivery",
            SentAtUtc: null,
            ProviderMessageId: null,
            Error: error,
            ReplyTo: null
        );
    }

    private static string? TryGetString(JsonElement element, string name)
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

    private static DateTimeOffset? TryGetDateTimeOffset(JsonElement element, string name)
    {
        var raw = TryGetString(element, name);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return DateTimeOffset.TryParse(raw, out var parsed) ? parsed : null;
    }

    private static IReadOnlyList<string> ReadEditorInitials(JsonElement root)
    {
        if (!root.TryGetProperty("editorInitials", out var element))
        {
            return Array.Empty<string>();
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            return element
                .EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => e.GetString() ?? string.Empty)
                .SelectMany(value => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            var raw = element.GetString() ?? string.Empty;
            return raw
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        return Array.Empty<string>();
    }

    private static IReadOnlyList<string> ReadEmailAddresses(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var element))
        {
            return Array.Empty<string>();
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            var values = element
                .EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => e.GetString() ?? string.Empty);
            return NormalizeEmailList(values);
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            var raw = element.GetString() ?? string.Empty;
            return NormalizeEmailList(new[] { raw });
        }

        return Array.Empty<string>();
    }

    private sealed record DeliveryCurrentState(
        string? StablePath,
        string? ShareUrl,
        string? CurrentVersion,
        DateTimeOffset? RetentionUntilUtc);
}
