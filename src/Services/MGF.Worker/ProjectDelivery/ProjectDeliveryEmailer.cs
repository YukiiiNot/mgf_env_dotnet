namespace MGF.Worker.ProjectDelivery;

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MGF.Infrastructure.Data;
using MGF.Worker.Email;
using MGF.Worker.Email.Composition;
using MGF.Worker.Email.Models;
using MGF.Worker.Email.Registry;
using MGF.Worker.Email.Sending;
using MGF.Tools.Provisioner;

public sealed class ProjectDeliveryEmailer
{
    private const string DefaultReplyToAddress = "info@mgfilms.pro";
    private const string DeliveryEmailTemplateVersion = "v1-html";

    private readonly IConfiguration configuration;
    private readonly EmailService emailService;
    private readonly ILogger? logger;

    public ProjectDeliveryEmailer(
        IConfiguration configuration,
        IEmailSender? emailSender = null,
        ILogger? logger = null)
    {
        this.configuration = configuration;
        emailService = new EmailService(configuration, sender: emailSender, logger: logger);
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
        if (string.IsNullOrWhiteSpace(current.StablePath) && string.IsNullOrWhiteSpace(current.ApiStablePath))
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

        DeliveryEmailContext? context = null;
        if (!string.IsNullOrWhiteSpace(current.ApiStablePath))
        {
            context = TryBuildContextFromMetadata(project.Metadata);
        }
        else if (!string.IsNullOrWhiteSpace(current.StablePath))
        {
            if (!TryResolveContainerRoot(current.StablePath, out var containerRoot, out var resolveError))
            {
                logger?.LogWarning("MGF.Worker: delivery email manifest resolve failed: {Error}", resolveError);
            }
            else
            {
                var manifestPath = ProjectDeliverer.ResolveDeliveryManifestPath(containerRoot);
                context = await TryReadManifestContextAsync(manifestPath, cancellationToken);
            }
        }

        if (context is null)
        {
            context = TryBuildContextFromMetadata(project.Metadata);
            if (context is null)
            {
                var failed = BuildFailure(payload.ToEmails, "Delivery manifest is missing and metadata lacks delivery files.");
                await ProjectDeliverer.AppendDeliveryEmailAsync(db, project.ProjectId, project.Metadata, failed, cancellationToken);
                return failed;
            }
        }

        var clientName = await db.Clients.AsNoTracking()
            .Where(c => c.ClientId == project.ClientId)
            .Select(c => c.DisplayName)
            .SingleOrDefaultAsync(cancellationToken);

        var tokens = ProvisioningTokens.Create(project.ProjectCode, project.Name, clientName, payload.EditorInitials);
        var subject = ProjectDeliverer.BuildDeliverySubject(tokens);
        var profile = EmailProfileResolver.Resolve(configuration, EmailProfiles.Deliveries);
        var replyTo = !string.IsNullOrWhiteSpace(payload.ReplyToEmail)
            ? payload.ReplyToEmail
            : profile.DefaultReplyTo ?? DefaultReplyToAddress;

        var recipients = NormalizeEmailList(payload.ToEmails);
        if (recipients.Count == 0)
        {
            var failed = BuildFailure(recipients, "No delivery email recipients were provided.");
            await ProjectDeliverer.AppendDeliveryEmailAsync(db, project.ProjectId, project.Metadata, failed, cancellationToken);
            return failed;
        }

        var files = context.Files;
        var versionLabel = context.VersionLabel;
        var retentionUntil = context.RetentionUntilUtc;
        var logoUrl = profile.LogoUrl;
        var fromName = profile.DefaultFromName ?? "MG Films";
        DeliveryEmailResult result;
        try
        {
            var emailContext = new DeliveryReadyEmailContext(
                tokens,
                current.ShareUrl,
                versionLabel,
                retentionUntil,
                files,
                recipients,
                replyTo,
                logoUrl,
                fromName);
            result = await emailService.SendAsync(EmailKind.DeliveryReady, emailContext, cancellationToken);
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
                return new DeliveryCurrentState(null, null, null, null, null);
            }

            if (!delivery.TryGetProperty("current", out var current))
            {
                return new DeliveryCurrentState(null, null, null, null, null);
            }

            return new DeliveryCurrentState(
                StablePath: TryGetString(current, "stablePath"),
                ApiStablePath: TryGetString(current, "apiStablePath"),
                ShareUrl: TryGetString(current, "stableShareUrl") ?? TryGetString(current, "shareUrl"),
                CurrentVersion: TryGetString(current, "currentVersion"),
                RetentionUntilUtc: TryGetDateTimeOffset(current, "retentionUntilUtc")
            );
        }
        catch
        {
            return new DeliveryCurrentState(null, null, null, null, null);
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
            TemplateVersion: DeliveryEmailTemplateVersion,
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

    private sealed record DeliveryEmailContext(
        IReadOnlyList<DeliveryFileSummary> Files,
        string VersionLabel,
        DateTimeOffset RetentionUntilUtc);

    private sealed record DeliveryCurrentState(
        string? StablePath,
        string? ApiStablePath,
        string? ShareUrl,
        string? CurrentVersion,
        DateTimeOffset? RetentionUntilUtc);

    private static async Task<DeliveryEmailContext?> TryReadManifestContextAsync(
        string manifestPath,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(manifestPath, cancellationToken);
            var manifest = JsonSerializer.Deserialize<DeliveryManifest>(json, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            });

            if (manifest is null)
            {
                return null;
            }

            var files = manifest.Files ?? Array.Empty<DeliveryFileSummary>();
            var versionLabel = string.IsNullOrWhiteSpace(manifest.CurrentVersion) ? "v1" : manifest.CurrentVersion;
            var retentionUntil = manifest.RetentionUntilUtc == default
                ? DateTimeOffset.UtcNow.AddMonths(3)
                : manifest.RetentionUntilUtc;

            return new DeliveryEmailContext(files, versionLabel, retentionUntil);
        }
        catch
        {
            return null;
        }
    }

    private static DeliveryEmailContext? TryBuildContextFromMetadata(JsonElement metadata)
    {
        try
        {
            using var doc = JsonDocument.Parse(metadata.GetRawText());
            if (!doc.RootElement.TryGetProperty("delivery", out var delivery))
            {
                return null;
            }

            var currentVersion = string.Empty;
            var retentionUntil = DateTimeOffset.UtcNow.AddMonths(3);
            if (delivery.TryGetProperty("current", out var current))
            {
                currentVersion = TryGetString(current, "currentVersion") ?? string.Empty;
                retentionUntil = TryGetDateTimeOffset(current, "retentionUntilUtc") ?? retentionUntil;
            }

            if (!delivery.TryGetProperty("runs", out var runs) || runs.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            for (var index = runs.GetArrayLength() - 1; index >= 0; index--)
            {
                var run = runs[index];
                if (run.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var versionLabel = TryGetString(run, "versionLabel") ?? currentVersion;
                if (string.IsNullOrWhiteSpace(versionLabel))
                {
                    versionLabel = "v1";
                }

                var runRetention = TryGetDateTimeOffset(run, "retentionUntilUtc") ?? retentionUntil;

                if (run.TryGetProperty("files", out var filesElement) && filesElement.ValueKind == JsonValueKind.Array)
                {
                    var files = ReadDeliveryFiles(filesElement);
                    if (files.Count > 0)
                    {
                        return new DeliveryEmailContext(files, versionLabel, runRetention);
                    }
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static List<DeliveryFileSummary> ReadDeliveryFiles(JsonElement filesElement)
    {
        var list = new List<DeliveryFileSummary>();
        foreach (var element in filesElement.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var relative = TryGetString(element, "relativePath");
            if (string.IsNullOrWhiteSpace(relative))
            {
                continue;
            }

            var sizeRaw = TryGetString(element, "sizeBytes");
            if (!long.TryParse(sizeRaw, out var sizeBytes))
            {
                sizeBytes = 0;
            }

            var lastWrite = TryGetDateTimeOffset(element, "lastWriteTimeUtc") ?? DateTimeOffset.MinValue;
            list.Add(new DeliveryFileSummary(relative, sizeBytes, lastWrite));
        }

        return list;
    }
}
