namespace MGF.UseCases.DeliveryEmail;

using System.Text.Json;
using MGF.Contracts.Abstractions;
using MGF.Provisioning;

public sealed class SendDeliveryEmailUseCase : ISendDeliveryEmailUseCase
{
    private const string DeliveryEmailTemplateVersion = "v1-html";
    private const string DefaultFromAddress = "deliveries@mgfilms.pro";
    private const string DefaultSubject = "MGF Delivery";

    private readonly IDeliveryEmailData data;
    private readonly IWorkerEmailGateway emailGateway;

    public SendDeliveryEmailUseCase(IDeliveryEmailData data, IWorkerEmailGateway emailGateway)
    {
        this.data = data;
        this.emailGateway = emailGateway;
    }

    public async Task<SendDeliveryEmailResult> ExecuteAsync(
        SendDeliveryEmailRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ProjectId))
        {
            throw new InvalidOperationException("ProjectId is required.");
        }

        var project = await data.GetProjectAsync(request.ProjectId, cancellationToken);
        if (project is null)
        {
            throw new InvalidOperationException($"Project not found: {request.ProjectId}");
        }

        var current = ReadDeliveryCurrent(project.Metadata);
        if (string.IsNullOrWhiteSpace(current.StablePath) && string.IsNullOrWhiteSpace(current.ApiStablePath))
        {
            return await RecordFailureAsync(
                project,
                NormalizeEmailList(project.CanonicalRecipients),
                current.ShareUrl,
                "Delivery stable path missing; run delivery first.",
                cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(current.ShareUrl))
        {
            return await RecordFailureAsync(
                project,
                NormalizeEmailList(project.CanonicalRecipients),
                current.ShareUrl,
                "Stable Dropbox share link missing; run delivery first.",
                cancellationToken);
        }

        DeliveryEmailContext? context = null;
        if (!string.IsNullOrWhiteSpace(current.ApiStablePath))
        {
            context = TryBuildContextFromMetadata(project.Metadata);
        }
        else if (!string.IsNullOrWhiteSpace(current.StablePath))
        {
            if (TryResolveContainerRoot(current.StablePath, out var containerRoot, out _))
            {
                var manifestPath = ResolveDeliveryManifestPath(containerRoot);
                context = await TryReadManifestContextAsync(manifestPath, cancellationToken);
            }
        }

        if (context is null)
        {
            context = TryBuildContextFromMetadata(project.Metadata);
            if (context is null)
            {
                return await RecordFailureAsync(
                    project,
                    NormalizeEmailList(project.CanonicalRecipients),
                    current.ShareUrl,
                    "Delivery manifest is missing and metadata lacks delivery files.",
                    cancellationToken);
            }
        }

        var recipients = NormalizeEmailList(project.CanonicalRecipients);
        if (recipients.Count == 0)
        {
            return await RecordFailureAsync(
                project,
                recipients,
                current.ShareUrl,
                "No delivery email recipients were provided.",
                cancellationToken);
        }

        if (request.ObservedRecipients is not null && request.ObservedRecipients.To.Count > 0)
        {
            var observed = NormalizeEmailList(request.ObservedRecipients.To);
            if (!AreEquivalentRecipients(observed, recipients))
            {
                return await RecordFailureAsync(
                    project,
                    recipients,
                    current.ShareUrl,
                    "Delivery email recipients do not match canonical recipients.",
                    cancellationToken);
            }
        }

        var tokens = ProvisioningTokens.Create(
            project.ProjectCode,
            project.ProjectName,
            project.ClientName,
            request.EditorInitials);

        var sendRequest = new WorkerDeliveryEmailRequest(
            Tokens: tokens,
            ShareUrl: current.ShareUrl,
            VersionLabel: context.VersionLabel,
            RetentionUntilUtc: context.RetentionUntilUtc,
            Files: context.Files,
            Recipients: recipients,
            ObservedReplyTo: request.ObservedRecipients?.ReplyTo,
            Mode: request.Mode);

        DeliveryEmailAudit emailResult;
        try
        {
            emailResult = await emailGateway.SendDeliveryReadyAsync(sendRequest, cancellationToken);
        }
        catch (Exception ex)
        {
            emailResult = BuildFailure(recipients, $"Delivery email failed: {ex.Message}");
        }

        if (request.Mode == DeliveryEmailMode.PreviewOnly)
        {
            return new SendDeliveryEmailResult(
                emailResult.Status,
                emailResult.Provider,
                emailResult.ProviderMessageId,
                emailResult.To,
                emailResult.Subject,
                current.ShareUrl,
                AuditRecorded: false,
                emailResult.Error);
        }

        await data.RecordDeliveryEmailSentAsync(project.ProjectId, project.Metadata, emailResult, cancellationToken);

        return new SendDeliveryEmailResult(
            emailResult.Status,
            emailResult.Provider,
            emailResult.ProviderMessageId,
            emailResult.To,
            emailResult.Subject,
            current.ShareUrl,
            AuditRecorded: true,
            emailResult.Error);
    }

    private static DeliveryEmailAudit BuildFailure(IReadOnlyList<string> recipients, string error)
    {
        return new DeliveryEmailAudit(
            Status: "failed",
            Provider: "email",
            FromAddress: DefaultFromAddress,
            To: recipients,
            Subject: DefaultSubject,
            SentAtUtc: null,
            ProviderMessageId: null,
            Error: error,
            TemplateVersion: DeliveryEmailTemplateVersion,
            ReplyTo: null);
    }

    private async Task<SendDeliveryEmailResult> RecordFailureAsync(
        DeliveryEmailProject project,
        IReadOnlyList<string> recipients,
        string? shareUrl,
        string error,
        CancellationToken cancellationToken)
    {
        var result = BuildFailure(recipients, error);
        await data.RecordDeliveryEmailSentAsync(project.ProjectId, project.Metadata, result, cancellationToken);
        return new SendDeliveryEmailResult(
            result.Status,
            result.Provider,
            result.ProviderMessageId,
            result.To,
            result.Subject,
            shareUrl,
            AuditRecorded: true,
            result.Error);
    }

    private static bool AreEquivalentRecipients(IReadOnlyList<string> observed, IReadOnlyList<string> canonical)
    {
        if (observed.Count != canonical.Count)
        {
            return false;
        }

        var observedSet = new HashSet<string>(observed, StringComparer.OrdinalIgnoreCase);
        return canonical.All(recipient => observedSet.Contains(recipient));
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
                RetentionUntilUtc: TryGetDateTimeOffset(current, "retentionUntilUtc"));
        }
        catch
        {
            return new DeliveryCurrentState(null, null, null, null, null);
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
            var manifest = JsonSerializer.Deserialize<DeliveryManifestSnapshot>(json, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            });

            if (manifest is null)
            {
                return null;
            }

            var files = manifest.Files ?? Array.Empty<DeliveryEmailFile>();
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

    private static bool TryResolveContainerRoot(
        string stablePath,
        out string containerRoot,
        out string error)
    {
        containerRoot = string.Empty;
        error = string.Empty;

        var trimmed = stablePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!IsStableSharePath(trimmed))
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

    private static string ResolveDeliveryManifestPath(string containerRoot)
    {
        var manifestDir = Path.Combine(containerRoot, "00_Admin", ".mgf", "manifest");
        return Path.Combine(manifestDir, "delivery_manifest.json");
    }

    private static bool IsStableSharePath(string path)
    {
        var trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var lastSegment = Path.GetFileName(trimmed);
        return !IsVersionFolderName(lastSegment);
    }

    private static bool IsVersionFolderName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        if (name.Length <= 1 || (name[0] != 'v' && name[0] != 'V'))
        {
            return false;
        }

        for (var i = 1; i < name.Length; i++)
        {
            if (!char.IsDigit(name[i]))
            {
                return false;
            }
        }

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

    private static List<DeliveryEmailFile> ReadDeliveryFiles(JsonElement filesElement)
    {
        var list = new List<DeliveryEmailFile>();
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
            list.Add(new DeliveryEmailFile(relative, sizeBytes, lastWrite));
        }

        return list;
    }

    private sealed record DeliveryEmailContext(
        IReadOnlyList<DeliveryEmailFile> Files,
        string VersionLabel,
        DateTimeOffset RetentionUntilUtc);

    private sealed record DeliveryCurrentState(
        string? StablePath,
        string? ApiStablePath,
        string? ShareUrl,
        string? CurrentVersion,
        DateTimeOffset? RetentionUntilUtc);

    private sealed record DeliveryManifestSnapshot
    {
        public string CurrentVersion { get; init; } = string.Empty;
        public DateTimeOffset RetentionUntilUtc { get; init; }
        public IReadOnlyList<DeliveryEmailFile> Files { get; init; } = Array.Empty<DeliveryEmailFile>();
    }
}
