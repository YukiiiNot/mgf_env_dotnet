namespace MGF.Worker.Adapters.Storage.ProjectDelivery;

using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MGF.Contracts.Abstractions.Dropbox;
using MGF.Contracts.Abstractions.Email;
using MGF.Contracts.Abstractions.ProjectDelivery;
using MGF.Email.Composition;
using MGF.Email.Models;
using MGF.Email.Registry;
using MGF.FolderProvisioning;
using MGF.Worker.Adapters.Storage.ProjectBootstrap;

public sealed class ProjectDeliveryExecutor : IProjectDeliveryExecutor
{
    private const int DefaultRetentionMonths = 3;
    private const int ShareLinkTtlDays = 7;
    private const string DeliveryFromAddress = "deliveries@mgfilms.pro";
    private const string DefaultReplyToAddress = "info@mgfilms.pro";
    private const string DeliveryEmailTemplateVersion = "v1-html";

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mov", ".mxf", ".wav", ".mp3", ".m4a", ".aif", ".aiff", ".srt", ".vtt", ".xml", ".pdf"
    };
    private readonly IConfiguration configuration;
    private readonly LocalFileStore fileStore = new();
    private readonly FolderProvisioner provisioner;
    private readonly IDropboxShareLinkClient shareLinkClient;
    private readonly IDropboxAccessTokenProvider accessTokenProvider;
    private readonly IDropboxFilesClient dropboxFilesClient;
    private readonly EmailService emailService;
    private readonly ILogger? logger;

    public ProjectDeliveryExecutor(
        IConfiguration configuration,
        IDropboxShareLinkClient shareLinkClient,
        IDropboxAccessTokenProvider accessTokenProvider,
        IDropboxFilesClient dropboxFilesClient,
        IEmailSender emailSender,
        ILogger? logger = null)
    {
        this.configuration = configuration;
        provisioner = new FolderProvisioner(fileStore);
        this.shareLinkClient = shareLinkClient;
        this.accessTokenProvider = accessTokenProvider;
        this.dropboxFilesClient = dropboxFilesClient;
        emailService = new EmailService(configuration, emailSender, logger: logger);
        this.logger = logger;
    }

    internal sealed record DeliveryShareState(
        string? ShareUrl,
        string? ShareId,
        string? ShareStatus,
        DateTimeOffset? LastVerifiedAtUtc
    );

    internal sealed record DeliveryHistory(
        string? CurrentVersion,
        IReadOnlyList<DeliveryFileSummary> LastFiles
    );

    private sealed record DeliveryContainerManifest(
        string TemplateKey,
        string TemplateHash,
        IReadOnlyList<string> FolderPaths,
        byte[] ManifestBytes
    );

    internal sealed record DeliveryShareOutcome(
        string? ShareStatus,
        string? ShareUrl,
        string? ShareId,
        string? ShareError,
        DateTimeOffset? VerifiedAtUtc
    );

    internal sealed record DeliveryVersionPlan(
        string VersionLabel,
        string StableRoot,
        string VersionRoot,
        bool IsNewVersion,
        bool LegacyFinalFiles
    );

    internal static string BuildDeliverySubject(ProvisioningTokens tokens)
    {
        var code = tokens.ProjectCode ?? "MGF";
        var name = tokens.ProjectName ?? "Delivery";
        return $"Your deliverables are ready \u2014 {code} {name}";
    }

    private static ProvisioningTokens ToProvisioningTokens(DeliveryTokens tokens)
    {
        return ProvisioningTokens.Create(
            tokens.ProjectCode,
            tokens.ProjectName,
            tokens.ClientName,
            tokens.EditorInitials);
    }

    internal static EmailMessage BuildDeliveryEmailRequest(
        string subject,
        string shareUrl,
        string versionLabel,
        DateTimeOffset retentionUntilUtc,
        IReadOnlyList<DeliveryFileSummary> files,
        IReadOnlyList<string> recipients,
        string? replyTo,
        ProvisioningTokens tokens,
        string? logoUrl,
        string? fromName)
    {
        var body = BuildDeliveryEmailBody(shareUrl, versionLabel, retentionUntilUtc, files);
        var htmlBody = BuildDeliveryEmailBodyHtml(shareUrl, versionLabel, retentionUntilUtc, files, tokens, logoUrl);
        return new EmailMessage(
            FromAddress: DeliveryFromAddress,
            FromName: fromName,
            To: recipients,
            Subject: subject,
            BodyText: body,
            HtmlBody: htmlBody,
            TemplateVersion: DeliveryEmailTemplateVersion,
            ReplyTo: replyTo);
    }

    internal static string BuildDeliveryEmailBody(
        string shareUrl,
        string versionLabel,
        DateTimeOffset retentionUntilUtc,
        IReadOnlyList<DeliveryFileSummary> files)
    {
        var lines = new List<string>
        {
            "Your deliverables are ready.",
            string.Empty,
            $"Download link: {shareUrl}",
            $"Current delivery version: {versionLabel}",
            $"This link remains active for 3 months (until {retentionUntilUtc:yyyy-MM-dd}).",
            string.Empty,
            "Files:"
        };

        foreach (var file in files)
        {
            lines.Add($"- {file.RelativePath}");
        }

        lines.Add(string.Empty);
        lines.Add("If you have any questions, contact info@mgfilms.pro.");
        lines.Add(string.Empty);
        lines.Add("Thank you,");
        lines.Add("MGF");

        return string.Join(Environment.NewLine, lines);
    }

    internal static string BuildDeliveryEmailBodyHtml(
        string shareUrl,
        string versionLabel,
        DateTimeOffset retentionUntilUtc,
        IReadOnlyList<DeliveryFileSummary> files,
        ProvisioningTokens tokens,
        string? logoUrl)
    {
        var context = new DeliveryReadyEmailContext(
            tokens,
            shareUrl,
            versionLabel,
            retentionUntilUtc,
            files.Select(ToEmailSummary).ToArray(),
            Array.Empty<string>(),
            null,
            logoUrl,
            "MG Films");

        var renderer = EmailTemplateRenderer.CreateDefault();
        return renderer.RenderHtml("delivery_ready.html", context);
    }

    public Task<ProjectDeliverySourceResult> ResolveLucidlinkSourceAsync(
        ProjectDeliveryPayload payload,
        string? storageRelpath,
        CancellationToken cancellationToken = default)
    {
        _ = payload;
        _ = cancellationToken;
        var rootPath = ResolveDomainRootPath("lucidlink");
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            return Task.FromResult(new ProjectDeliverySourceResult(
                SourcePath: null,
                Files: Array.Empty<DeliveryFile>(),
                DomainResult: new ProjectDeliveryDomainResult(
                    DomainKey: "lucidlink",
                    RootPath: string.Empty,
                    RootState: "skipped_unconfigured",
                    DeliveryContainerProvisioning: null,
                    Deliverables: Array.Empty<DeliveryFileSummary>(),
                    VersionLabel: null,
                    DestinationPath: null,
                    Notes: new[] { "LucidLink root not configured." }
                )
            ));
        }

        if (!Directory.Exists(rootPath))
        {
            return Task.FromResult(new ProjectDeliverySourceResult(
                SourcePath: null,
                Files: Array.Empty<DeliveryFile>(),
                DomainResult: new ProjectDeliveryDomainResult(
                    DomainKey: "lucidlink",
                    RootPath: rootPath,
                    RootState: "blocked_missing_root",
                    DeliveryContainerProvisioning: null,
                    Deliverables: Array.Empty<DeliveryFileSummary>(),
                    VersionLabel: null,
                    DestinationPath: null,
                    Notes: new[] { "LucidLink root missing." }
                )
            ));
        }

        var relpath = storageRelpath;
        if (string.IsNullOrWhiteSpace(relpath))
        {
            return Task.FromResult(new ProjectDeliverySourceResult(
                SourcePath: null,
                Files: Array.Empty<DeliveryFile>(),
                DomainResult: new ProjectDeliveryDomainResult(
                    DomainKey: "lucidlink",
                    RootPath: rootPath,
                    RootState: "container_missing",
                    DeliveryContainerProvisioning: null,
                    Deliverables: Array.Empty<DeliveryFileSummary>(),
                    VersionLabel: null,
                    DestinationPath: null,
                    Notes: new[] { "LucidLink storage root not found; run bootstrap first." }
                )
            ));
        }

        var normalizedRelpath = relpath.Trim().TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (Path.IsPathRooted(normalizedRelpath) || normalizedRelpath.Contains("..", StringComparison.Ordinal))
        {
            return Task.FromResult(new ProjectDeliverySourceResult(
                SourcePath: null,
                Files: Array.Empty<DeliveryFile>(),
                DomainResult: new ProjectDeliveryDomainResult(
                    DomainKey: "lucidlink",
                    RootPath: rootPath,
                    RootState: "container_missing",
                    DeliveryContainerProvisioning: null,
                    Deliverables: Array.Empty<DeliveryFileSummary>(),
                    VersionLabel: null,
                    DestinationPath: null,
                    Notes: new[] { $"LucidLink storage relpath is invalid: {relpath}" }
                )
            ));
        }

        var containerPath = Path.Combine(rootPath, normalizedRelpath);
        var sourcePath = Path.Combine(containerPath, "02_Renders", "Final_Masters");

        if (!Directory.Exists(sourcePath))
        {
            return Task.FromResult(new ProjectDeliverySourceResult(
                SourcePath: sourcePath,
                Files: Array.Empty<DeliveryFile>(),
                DomainResult: new ProjectDeliveryDomainResult(
                    DomainKey: "lucidlink",
                    RootPath: rootPath,
                    RootState: "no_source_folder",
                    DeliveryContainerProvisioning: null,
                    Deliverables: Array.Empty<DeliveryFileSummary>(),
                    VersionLabel: null,
                    DestinationPath: null,
                    Notes: new[] { "LucidLink Final_Masters folder not found." }
                )
            ));
        }

        var files = FindDeliverableFiles(sourcePath);
        if (files.Count == 0)
        {
            return Task.FromResult(new ProjectDeliverySourceResult(
                SourcePath: sourcePath,
                Files: Array.Empty<DeliveryFile>(),
                DomainResult: new ProjectDeliveryDomainResult(
                    DomainKey: "lucidlink",
                    RootPath: rootPath,
                    RootState: "no_deliverables_found",
                    DeliveryContainerProvisioning: null,
                    Deliverables: Array.Empty<DeliveryFileSummary>(),
                    VersionLabel: null,
                    DestinationPath: null,
                    Notes: new[] { "No deliverable files found in Final_Masters." }
                )
            ));
        }

        return Task.FromResult(new ProjectDeliverySourceResult(
            SourcePath: sourcePath,
            Files: files,
            DomainResult: new ProjectDeliveryDomainResult(
                DomainKey: "lucidlink",
                RootPath: rootPath,
                RootState: "source_ready",
                DeliveryContainerProvisioning: null,
                Deliverables: files.Select(ToSummary).ToArray(),
                VersionLabel: null,
                DestinationPath: null,
                Notes: Array.Empty<string>()
            )
        ));
    }

    public async Task<ProjectDeliveryTargetResult> ProcessDropboxAsync(
        ProjectDeliveryPayload payload,
        DeliveryTokens tokens,
        ProjectDeliverySourceResult source,
        string dropboxDeliveryRelpath,
        JsonElement projectMetadata,
        CancellationToken cancellationToken = default)
    {
        var provisioningTokens = ToProvisioningTokens(tokens);
        var deliveryTemplatePath = ResolveDeliveryTemplatePath();
        var projectFolderName = await ResolveProjectFolderNameAsync(deliveryTemplatePath, provisioningTokens, cancellationToken);
        var shareState = ReadShareState(projectMetadata);
        var history = ReadDeliveryHistory(projectMetadata);

        return await ProcessDropboxAsyncCore(
            payload,
            projectFolderName,
            provisioningTokens,
            source,
            dropboxDeliveryRelpath,
            shareState,
            history,
            deliveryTemplatePath,
            cancellationToken);
    }

    private async Task<ProjectDeliveryTargetResult> ProcessDropboxAsyncCore(
        ProjectDeliveryPayload payload,
        string projectFolderName,
        ProvisioningTokens tokens,
        ProjectDeliverySourceResult source,
        string dropboxDeliveryRelpath,
        DeliveryShareState shareState,
        DeliveryHistory history,
        string deliveryTemplatePath,
        CancellationToken cancellationToken)
    {
        var notes = new List<string>();
        var useApiRoot = configuration.GetValue("Integrations:Dropbox:UseApiRootFolder", false)
            || configuration.GetValue("Dropbox:UseApiRootFolder", false);

        if (useApiRoot)
        {
            return await ProcessDropboxApiAsync(
                payload,
                projectFolderName,
                tokens,
                source,
                dropboxDeliveryRelpath,
                shareState,
                history,
                deliveryTemplatePath,
                cancellationToken);
        }
        var rootPath = ResolveDomainRootPath("dropbox");
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            return new ProjectDeliveryTargetResult(
                DestinationPath: null,
                ApiStablePath: null,
                ApiVersionPath: null,
                VersionLabel: null,
                RetentionUntilUtc: null,
                DomainResult: new ProjectDeliveryDomainResult(
                    DomainKey: "dropbox",
                    RootPath: string.Empty,
                    RootState: "skipped_unconfigured",
                    DeliveryContainerProvisioning: null,
                    Deliverables: source.Files.Select(ToSummary).ToArray(),
                    VersionLabel: null,
                    DestinationPath: null,
                    Notes: new[] { "Dropbox root not configured." }
                ),
                ShareStatus: null,
                ShareUrl: null,
                ShareId: null,
                ShareError: null
            );
        }

        if (!Directory.Exists(rootPath))
        {
            return new ProjectDeliveryTargetResult(
                DestinationPath: null,
                ApiStablePath: null,
                ApiVersionPath: null,
                VersionLabel: null,
                RetentionUntilUtc: null,
                DomainResult: new ProjectDeliveryDomainResult(
                    DomainKey: "dropbox",
                    RootPath: rootPath,
                    RootState: "blocked_missing_root",
                    DeliveryContainerProvisioning: null,
                    Deliverables: source.Files.Select(ToSummary).ToArray(),
                    VersionLabel: null,
                    DestinationPath: null,
                    Notes: new[] { "Dropbox root missing." }
                ),
                ShareStatus: null,
                ShareUrl: null,
                ShareId: null,
                ShareError: null
            );
        }

        var clientFolder = tokens.ClientName ?? "Client";
        PathSafety.EnsureSafeSegment(clientFolder, "client folder name");

        var baseRoot = payload.TestMode
            ? Path.Combine(rootPath, "99_TestRuns")
            : rootPath;

        var basePath = Path.Combine(baseRoot, dropboxDeliveryRelpath, clientFolder);
        var targetPath = Path.Combine(basePath, projectFolderName);

        if (payload.TestMode && payload.AllowTestCleanup && Directory.Exists(targetPath))
        {
            if (!ProjectBootstrapGuards.TryValidateTestCleanup(rootPath, targetPath, payload.AllowTestCleanup, out var cleanupError))
            {
                notes.Add(cleanupError ?? "Test cleanup blocked.");
                return new ProjectDeliveryTargetResult(
                    DestinationPath: targetPath,
                    ApiStablePath: null,
                    ApiVersionPath: null,
                    VersionLabel: null,
                    RetentionUntilUtc: null,
                    DomainResult: new ProjectDeliveryDomainResult(
                        DomainKey: "dropbox",
                        RootPath: rootPath,
                        RootState: "blocked_test_cleanup",
                        DeliveryContainerProvisioning: null,
                        Deliverables: source.Files.Select(ToSummary).ToArray(),
                        VersionLabel: null,
                        DestinationPath: targetPath,
                        Notes: notes
                    ),
                    ShareStatus: null,
                    ShareUrl: null,
                    ShareId: null,
                    ShareError: null
                );
            }

            var cleanup = await ProjectBootstrapCleanupHelper.TryDeleteWithRetryAsync(targetPath, cancellationToken);
            if (!cleanup.Success)
            {
                notes.Add(cleanup.Error ?? "Test cleanup failed (locked; cleanup skipped).");
                return new ProjectDeliveryTargetResult(
                    DestinationPath: targetPath,
                    ApiStablePath: null,
                    ApiVersionPath: null,
                    VersionLabel: null,
                    RetentionUntilUtc: null,
                    DomainResult: new ProjectDeliveryDomainResult(
                        DomainKey: "dropbox",
                        RootPath: rootPath,
                        RootState: "cleanup_locked",
                        DeliveryContainerProvisioning: null,
                        Deliverables: source.Files.Select(ToSummary).ToArray(),
                        VersionLabel: null,
                        DestinationPath: targetPath,
                        Notes: notes
                    ),
                    ShareStatus: null,
                    ShareUrl: null,
                    ShareId: null,
                    ShareError: null
                );
            }
        }

        var applySummary = await ExecuteTemplateAsync(deliveryTemplatePath, basePath, tokens, ProvisioningMode.Apply, cancellationToken);
        var verifySummary = await ExecuteTemplateAsync(deliveryTemplatePath, basePath, tokens, ProvisioningMode.Verify, cancellationToken);

        if (!verifySummary.Success)
        {
            notes.Add("Delivery container verify failed.");
            return new ProjectDeliveryTargetResult(
                DestinationPath: targetPath,
                ApiStablePath: null,
                ApiVersionPath: null,
                VersionLabel: null,
                RetentionUntilUtc: null,
                DomainResult: new ProjectDeliveryDomainResult(
                    DomainKey: "dropbox",
                    RootPath: rootPath,
                    RootState: "container_verify_failed",
                    DeliveryContainerProvisioning: verifySummary,
                    Deliverables: source.Files.Select(ToSummary).ToArray(),
                    VersionLabel: null,
                    DestinationPath: targetPath,
                    Notes: notes
                ),
                ShareStatus: null,
                ShareUrl: null,
                ShareId: null,
                ShareError: null
            );
        }

        var finalRoot = Path.Combine(targetPath, "01_Deliverables", "Final");
        Directory.CreateDirectory(finalRoot);
        var versionPlan = DetermineVersion(finalRoot, source.Files);

        notes.Add(versionPlan.IsNewVersion
            ? $"Created new delivery version {versionPlan.VersionLabel}."
            : $"Reusing existing delivery version {versionPlan.VersionLabel}.");
        if (versionPlan.LegacyFinalFiles)
        {
            notes.Add("Legacy files detected directly under Final; no automatic normalization performed.");
        }

        if (versionPlan.IsNewVersion)
        {
            foreach (var file in source.Files)
            {
                var destPath = Path.Combine(versionPlan.VersionRoot, file.RelativePath);
                var destDir = Path.GetDirectoryName(destPath);
                if (!string.IsNullOrWhiteSpace(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }

                if (!File.Exists(destPath))
                {
                    File.Copy(file.SourcePath, destPath, overwrite: false);
                }
            }
        }

        var retentionUntil = DateTimeOffset.UtcNow.AddMonths(DefaultRetentionMonths);
        var shareOutcome = await EnsureDropboxShareLinkAsync(
            rootPath,
            finalRoot,
            dropboxDeliveryRelpath,
            shareState,
            payload.RefreshShareLink,
            payload.TestMode,
            cancellationToken,
            apiStablePathOverride: null
        );
        if (!string.IsNullOrWhiteSpace(shareOutcome.ShareError))
        {
            notes.Add(shareOutcome.ShareError);
        }
        var manifestPath = ResolveDeliveryManifestPath(targetPath);
        var manifest = BuildDeliveryManifest(
            payload.ProjectId,
            tokens,
            source.SourcePath ?? string.Empty,
            versionPlan.StableRoot,
            versionPlan.VersionRoot,
            versionPlan.VersionLabel,
            retentionUntil,
            source.Files,
            shareOutcome.ShareStatus is "created" or "reused" ? shareOutcome.ShareUrl : null,
            apiStablePath: null,
            apiVersionPath: null
        );

        await WriteDeliveryManifestAsync(manifestPath, manifest, cancellationToken);

        return new ProjectDeliveryTargetResult(
            DestinationPath: versionPlan.StableRoot,
            ApiStablePath: null,
            ApiVersionPath: null,
            VersionLabel: versionPlan.VersionLabel,
            RetentionUntilUtc: retentionUntil,
            DomainResult: new ProjectDeliveryDomainResult(
                DomainKey: "dropbox",
                RootPath: rootPath,
                RootState: versionPlan.IsNewVersion ? "delivered" : "delivered_noop",
                DeliveryContainerProvisioning: verifySummary,
                Deliverables: source.Files.Select(ToSummary).ToArray(),
                VersionLabel: versionPlan.VersionLabel,
                DestinationPath: versionPlan.StableRoot,
                Notes: notes
            ),
            ShareStatus: shareOutcome.ShareStatus,
            ShareUrl: shareOutcome.ShareUrl,
            ShareId: shareOutcome.ShareId,
            ShareError: shareOutcome.ShareError
        );
    }

    private async Task<ProjectDeliveryTargetResult> ProcessDropboxApiAsync(
        ProjectDeliveryPayload payload,
        string projectFolderName,
        ProvisioningTokens tokens,
        ProjectDeliverySourceResult source,
        string dropboxDeliveryRelpath,
        DeliveryShareState shareState,
        DeliveryHistory history,
        string deliveryTemplatePath,
        CancellationToken cancellationToken)
    {
        var notes = new List<string>();
        try
        {
            var apiRoot = ResolveDropboxApiRootFolder();
            if (string.IsNullOrWhiteSpace(apiRoot))
            {
                return new ProjectDeliveryTargetResult(
                    DestinationPath: null,
                    ApiStablePath: null,
                    ApiVersionPath: null,
                    VersionLabel: null,
                    RetentionUntilUtc: null,
                    DomainResult: new ProjectDeliveryDomainResult(
                        DomainKey: "dropbox",
                        RootPath: string.Empty,
                        RootState: "blocked_api_root_missing",
                        DeliveryContainerProvisioning: null,
                        Deliverables: source.Files.Select(ToSummary).ToArray(),
                        VersionLabel: null,
                        DestinationPath: null,
                        Notes: new[] { "Dropbox ApiRootFolder not configured." }
                    ),
                    ShareStatus: null,
                    ShareUrl: null,
                    ShareId: null,
                    ShareError: null
                );
            }

            var tokenResult = await accessTokenProvider.GetAccessTokenAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(tokenResult.AccessToken))
            {
                return new ProjectDeliveryTargetResult(
                    DestinationPath: null,
                    ApiStablePath: null,
                    ApiVersionPath: null,
                    VersionLabel: null,
                    RetentionUntilUtc: null,
                    DomainResult: new ProjectDeliveryDomainResult(
                        DomainKey: "dropbox",
                        RootPath: "/" + apiRoot.Trim().Trim('/'),
                        RootState: "blocked_dropbox_auth",
                        DeliveryContainerProvisioning: null,
                        Deliverables: source.Files.Select(ToSummary).ToArray(),
                        VersionLabel: null,
                        DestinationPath: null,
                        Notes: new[] { tokenResult.Error ?? "Dropbox access token not configured." }
                    ),
                    ShareStatus: null,
                    ShareUrl: null,
                    ShareId: null,
                    ShareError: tokenResult.Error ?? "Dropbox access token not configured."
                );
            }

            var clientFolder = tokens.ClientName ?? "Client";
            PathSafety.EnsureSafeSegment(clientFolder, "client folder name");

            var rootPath = ResolveDomainRootPath("dropbox");
            var destinationPath = TryBuildLocalStablePath(rootPath, dropboxDeliveryRelpath, clientFolder, projectFolderName, payload.TestMode);

            var apiContainerRoot = BuildDropboxApiContainerRoot(apiRoot, dropboxDeliveryRelpath, clientFolder, projectFolderName);
            var apiStablePath = CombineDropboxPath(apiContainerRoot, "01_Deliverables", "Final");
            var versionPlan = DetermineVersionFromHistory(apiStablePath, history, source.Files);
            var apiVersionPath = CombineDropboxPath(apiStablePath, versionPlan.VersionLabel);

            notes.Add(versionPlan.IsNewVersion
                ? $"Created new delivery version {versionPlan.VersionLabel}."
                : $"Reusing existing delivery version {versionPlan.VersionLabel}.");

            var manifestInfo = await BuildDeliveryContainerManifestAsync(deliveryTemplatePath, tokens, apiContainerRoot, cancellationToken);
            var folderPaths = BuildDeliveryFolderList(apiContainerRoot, manifestInfo.FolderPaths, apiVersionPath);

            foreach (var folderPath in folderPaths.OrderBy(path => path.Length))
            {
                await dropboxFilesClient.EnsureFolderAsync(tokenResult.AccessToken, folderPath, cancellationToken);
            }

            await EnsureDropboxParentFoldersAsync(tokenResult.AccessToken, apiVersionPath, source.Files, cancellationToken);

            if (versionPlan.IsNewVersion)
            {
                var uploadPaths = BuildDropboxUploadPaths(apiVersionPath, source.Files);
                for (var index = 0; index < source.Files.Count; index++)
                {
                    var file = source.Files[index];
                    var dropboxPath = uploadPaths[index];
                    await dropboxFilesClient.UploadFileAsync(tokenResult.AccessToken, dropboxPath, file.SourcePath, cancellationToken);
                }
            }

            var folderManifestPath = CombineDropboxPath(apiContainerRoot, "00_Admin", ".mgf", "manifest", "folder_manifest.json");
            await dropboxFilesClient.UploadBytesAsync(tokenResult.AccessToken, folderManifestPath, manifestInfo.ManifestBytes, cancellationToken);

            var retentionUntil = DateTimeOffset.UtcNow.AddMonths(DefaultRetentionMonths);
            var shareOutcome = await EnsureDropboxShareLinkAsync(
                rootPath,
                destinationPath ?? apiStablePath,
                dropboxDeliveryRelpath,
                shareState,
                payload.RefreshShareLink,
                payload.TestMode,
                cancellationToken,
                apiStablePathOverride: apiStablePath
            );

            if (!string.IsNullOrWhiteSpace(shareOutcome.ShareError))
            {
                notes.Add(shareOutcome.ShareError);
            }

            var deliveryManifest = BuildDeliveryManifest(
                payload.ProjectId,
                tokens,
                source.SourcePath ?? string.Empty,
                destinationPath ?? apiStablePath,
                apiVersionPath,
                versionPlan.VersionLabel,
                retentionUntil,
                source.Files,
                shareOutcome.ShareStatus is "created" or "reused" ? shareOutcome.ShareUrl : null,
                apiStablePath: apiStablePath,
                apiVersionPath: apiVersionPath
            );

            var deliveryManifestBytes = SerializeDeliveryManifest(deliveryManifest);
            var deliveryManifestPath = CombineDropboxPath(apiContainerRoot, "00_Admin", ".mgf", "manifest", "delivery_manifest.json");
            await dropboxFilesClient.UploadBytesAsync(tokenResult.AccessToken, deliveryManifestPath, deliveryManifestBytes, cancellationToken);

            var provisioningSummary = new ProvisioningSummary(
                Mode: "api",
                TemplateKey: manifestInfo.TemplateKey,
                TargetRoot: apiContainerRoot,
                ManifestPath: folderManifestPath,
                Success: true,
                MissingRequired: Array.Empty<string>(),
                Errors: Array.Empty<string>(),
                Warnings: Array.Empty<string>()
            );

            return new ProjectDeliveryTargetResult(
                DestinationPath: destinationPath ?? apiStablePath,
                ApiStablePath: apiStablePath,
                ApiVersionPath: apiVersionPath,
                VersionLabel: versionPlan.VersionLabel,
                RetentionUntilUtc: retentionUntil,
                DomainResult: new ProjectDeliveryDomainResult(
                    DomainKey: "dropbox",
                    RootPath: string.IsNullOrWhiteSpace(rootPath) ? "/" + apiRoot.Trim().Trim('/') : rootPath,
                    RootState: versionPlan.IsNewVersion ? "delivered" : "delivered_noop",
                    DeliveryContainerProvisioning: provisioningSummary,
                    Deliverables: source.Files.Select(ToSummary).ToArray(),
                    VersionLabel: versionPlan.VersionLabel,
                    DestinationPath: destinationPath ?? apiStablePath,
                    Notes: notes
                ),
                ShareStatus: shareOutcome.ShareStatus,
                ShareUrl: shareOutcome.ShareUrl,
                ShareId: shareOutcome.ShareId,
                ShareError: shareOutcome.ShareError
            );
        }
        catch (Exception ex)
        {
            notes.Insert(0, ex.Message);
            return new ProjectDeliveryTargetResult(
                DestinationPath: null,
                ApiStablePath: null,
                ApiVersionPath: null,
                VersionLabel: null,
                RetentionUntilUtc: null,
                DomainResult: new ProjectDeliveryDomainResult(
                    DomainKey: "dropbox",
                    RootPath: string.Empty,
                    RootState: "delivery_failed",
                    DeliveryContainerProvisioning: null,
                    Deliverables: source.Files.Select(ToSummary).ToArray(),
                    VersionLabel: null,
                    DestinationPath: null,
                    Notes: notes
                ),
                ShareStatus: "failed",
                ShareUrl: null,
                ShareId: null,
                ShareError: ex.Message
            );
        }
    }

    public async Task<EmailSendResult> SendDeliveryEmailAsync(
        ProjectDeliveryPayload payload,
        DeliveryTokens tokens,
        ProjectDeliverySourceResult source,
        ProjectDeliveryTargetResult target,
        CancellationToken cancellationToken = default)
    {
        var provisioningTokens = ToProvisioningTokens(tokens);
        var recipients = NormalizeEmailList(payload.ToEmails);
        if (!IsShareSuccess(target.ShareStatus) || string.IsNullOrWhiteSpace(target.ShareUrl))
        {
            return SkippedEmailResult(
                recipients,
                BuildDeliverySubject(provisioningTokens),
                "Stable Dropbox share link is missing; delivery email skipped.",
                payload.ReplyToEmail);
        }

        var versionLabel = target.VersionLabel ?? "v1";
        var retentionUntil = target.RetentionUntilUtc ?? DateTimeOffset.UtcNow.AddMonths(DefaultRetentionMonths);
        var subject = BuildDeliverySubject(provisioningTokens);
        var profile = EmailProfileResolver.Resolve(configuration, EmailProfiles.Deliveries);
        var replyTo = !string.IsNullOrWhiteSpace(payload.ReplyToEmail)
            ? payload.ReplyToEmail
            : profile.DefaultReplyTo ?? DefaultReplyToAddress;
        if (recipients.Count == 0)
        {
            return SkippedEmailResult(
                recipients,
                subject,
                "No delivery email recipients were provided.",
                replyTo);
        }

        var shareUrl = target.ShareUrl;
        if (string.IsNullOrWhiteSpace(shareUrl))
        {
            return SkippedEmailResult(
                recipients,
                subject,
                "Stable Dropbox share link is missing; delivery email skipped.",
                replyTo);
        }

        var logoUrl = profile.LogoUrl;
        var fromName = profile.DefaultFromName ?? "MG Films";
        var context = new DeliveryReadyEmailContext(
            provisioningTokens,
            shareUrl,
            versionLabel,
            retentionUntil,
            source.Files.Select(ToEmailSummary).ToArray(),
            recipients,
            replyTo,
            logoUrl,
            fromName);

        try
        {
            return await emailService.SendAsync(EmailKind.DeliveryReady, context, cancellationToken);
        }
        catch (Exception ex)
        {
            return FailedEmailResult(recipients, subject, $"Delivery email failed: {ex.Message}", replyTo);
        }
    }

    private static bool IsShareSuccess(string? status)
    {
        return string.Equals(status, "created", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "reused", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<ProvisioningSummary> ExecuteTemplateAsync(
        string templatePath,
        string basePath,
        ProvisioningTokens tokens,
        ProvisioningMode mode,
        CancellationToken cancellationToken)
    {
        var request = new ProvisioningRequest(
            Mode: mode,
            TemplatePath: templatePath,
            BasePath: basePath,
            SchemaPath: null,
            SeedsPath: null,
            Tokens: tokens,
            ForceOverwriteSeededFiles: false
        );

        var result = await provisioner.ExecuteAsync(request, cancellationToken);

        return new ProvisioningSummary(
            Mode: result.Mode.ToString().ToLowerInvariant(),
            TemplateKey: result.TemplateKey,
            TargetRoot: result.TargetRoot,
            ManifestPath: result.ManifestPath,
            Success: result.Success,
            MissingRequired: result.MissingRequired.ToArray(),
            Errors: result.Errors.ToArray(),
            Warnings: result.Warnings.ToArray()
        );
    }

    private static async Task<string> ResolveProjectFolderNameAsync(
        string templatePath,
        ProvisioningTokens tokens,
        CancellationToken cancellationToken)
    {
        var loader = new FolderTemplateLoader();
        var loaded = await loader.LoadAsync(templatePath, schemaPathOverride: null, cancellationToken);
        var expanded = TokenExpander.ExpandRootName(loaded.Template.Root.Name, tokens);
        PathSafety.EnsureSafeSegment(expanded, "project folder name");
        return expanded;
    }

    private static async Task<DeliveryContainerManifest> BuildDeliveryContainerManifestAsync(
        string templatePath,
        ProvisioningTokens tokens,
        string targetRoot,
        CancellationToken cancellationToken)
    {
        var loader = new FolderTemplateLoader();
        var loaded = await loader.LoadAsync(templatePath, schemaPathOverride: null, cancellationToken);
        var templateHash = Hashing.Sha256Hex(loaded.TemplateBytes);
        var planner = new FolderTemplatePlanner();
        var repoRoot = FindRepoRoot();
        var planBase = Path.Combine(repoRoot, "runtime", "delivery_plan");
        var plan = planner.Plan(loaded.Template, tokens, planBase);

        var folderPaths = plan.Items
            .Where(item => item.Kind == PlanItemKind.Folder)
            .Select(item => item.RelativePath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var manifest = new ProvisioningManifest
        {
            TemplateKey = loaded.Template.TemplateKey,
            TemplateHash = templateHash,
            RunMode = "api",
            TimestampUtc = DateTimeOffset.UtcNow,
            Tokens = tokens.ToDictionary(),
            TargetRoot = targetRoot,
            ExpectedItems = plan.Items.Select(ToManifestItem).ToList(),
            CreatedItems = new List<ManifestItem>(),
            MissingRequired = new List<string>(),
            Warnings = new List<string>(),
            Errors = new List<string>()
        };

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        var manifestBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(manifest, options));

        return new DeliveryContainerManifest(
            TemplateKey: loaded.Template.TemplateKey,
            TemplateHash: templateHash,
            FolderPaths: folderPaths,
            ManifestBytes: manifestBytes
        );
    }

    private static ManifestItem ToManifestItem(PlanItem item)
    {
        return new ManifestItem
        {
            Path = item.RelativePath,
            Kind = item.Kind.ToString().ToLowerInvariant(),
            Optional = item.Optional
        };
    }

    private static IReadOnlyList<string> BuildDeliveryFolderList(
        string containerRoot,
        IReadOnlyList<string> folderRelpaths,
        string versionPath)
    {
        var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            containerRoot,
            versionPath
        };

        foreach (var relpath in folderRelpaths)
        {
            if (string.IsNullOrWhiteSpace(relpath))
            {
                continue;
            }

            folders.Add(CombineDropboxPath(containerRoot, relpath));
        }

        return folders.ToArray();
    }

    private static byte[] SerializeDeliveryManifest(DeliveryManifest manifest)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(manifest, options));
    }

    private async Task EnsureDropboxParentFoldersAsync(
        string accessToken,
        string versionPath,
        IReadOnlyList<DeliveryFile> files,
        CancellationToken cancellationToken)
    {
        var folders = BuildDropboxUploadFolders(versionPath, files);
        foreach (var folder in folders)
        {
            await dropboxFilesClient.EnsureFolderAsync(accessToken, folder, cancellationToken);
        }
    }

    internal static IReadOnlyList<string> BuildDropboxUploadFolders(
        string versionPath,
        IReadOnlyList<DeliveryFile> files)
    {
        var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            var relativeDir = Path.GetDirectoryName(file.RelativePath);
            if (string.IsNullOrWhiteSpace(relativeDir))
            {
                continue;
            }

            folders.Add(CombineDropboxPath(versionPath, relativeDir));
        }

        return folders.ToArray();
    }

    internal static IReadOnlyList<string> BuildDropboxUploadPaths(
        string versionPath,
        IReadOnlyList<DeliveryFile> files)
    {
        return files
            .Select(file => CombineDropboxPath(versionPath, file.RelativePath))
            .ToArray();
    }

    private static IReadOnlyList<DeliveryFile> FindDeliverableFiles(string sourcePath)
    {
        var list = new List<DeliveryFile>();
        foreach (var filePath in Directory.EnumerateFiles(sourcePath, "*", SearchOption.AllDirectories))
        {
            var fileName = Path.GetFileName(filePath);
            if (IsIgnoredFileName(fileName))
            {
                continue;
            }

            var extension = Path.GetExtension(fileName);
            if (!AllowedExtensions.Contains(extension))
            {
                continue;
            }

            var info = new FileInfo(filePath);
            var relative = Path.GetRelativePath(sourcePath, filePath);
            list.Add(new DeliveryFile(filePath, relative, info.Length, info.LastWriteTimeUtc));
        }

        return list;
    }

    private static bool IsIgnoredFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return true;
        }

        if (fileName.StartsWith("._", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(fileName, ".ds_store", StringComparison.OrdinalIgnoreCase)
               || fileName.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase);
    }

    internal static DeliveryVersionPlan DetermineVersion(string finalRoot, IReadOnlyList<DeliveryFile> sourceFiles)
    {
        var sourceSet = BuildFileSet(sourceFiles);
        var versionDirs = Directory.GetDirectories(finalRoot)
            .Where(dir => IsVersionFolderName(Path.GetFileName(dir)))
            .ToArray();

        var legacySet = BuildFileSet(finalRoot, versionDirs);
        var legacyHasFiles = legacySet.Count > 0;

        if (versionDirs.Length > 0)
        {
            var latestVersion = versionDirs
                .Select(dir => new { Dir = dir, Version = ParseVersionNumber(Path.GetFileName(dir)) })
                .Where(entry => entry.Version > 0)
                .OrderByDescending(entry => entry.Version)
                .FirstOrDefault();

            if (latestVersion is not null)
            {
                var latestSet = BuildFileSet(latestVersion.Dir, Array.Empty<string>());
                if (sourceSet.Count > 0 && latestSet.Count > 0 && SetsMatch(sourceSet, latestSet))
                {
                    var label = Path.GetFileName(latestVersion.Dir);
                    return new DeliveryVersionPlan(label, finalRoot, latestVersion.Dir, false, legacyHasFiles);
                }

                var nextVersion = latestVersion.Version + 1;
                var nextLabel = $"v{nextVersion}";
                var destination = Path.Combine(finalRoot, nextLabel);
                return new DeliveryVersionPlan(nextLabel, finalRoot, destination, true, legacyHasFiles);
            }
        }

        if (legacyHasFiles)
        {
            if (sourceSet.Count > 0 && SetsMatch(sourceSet, legacySet))
            {
                return new DeliveryVersionPlan("v1", finalRoot, finalRoot, false, true);
            }

            var versionRoot = Path.Combine(finalRoot, "v1");
            return new DeliveryVersionPlan("v1", finalRoot, versionRoot, true, true);
        }

        var initialVersionRoot = Path.Combine(finalRoot, "v1");
        return new DeliveryVersionPlan("v1", finalRoot, initialVersionRoot, true, false);
    }

    private static Dictionary<string, DeliveryFingerprint> BuildFileSet(
        IReadOnlyList<DeliveryFile> files)
    {
        var map = new Dictionary<string, DeliveryFingerprint>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            map[file.RelativePath] = new DeliveryFingerprint(file.SizeBytes, file.LastWriteTimeUtc);
        }

        return map;
    }

    private static Dictionary<string, DeliveryFingerprint> BuildFileSet(
        IReadOnlyList<DeliveryFileSummary> files)
    {
        var map = new Dictionary<string, DeliveryFingerprint>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            map[file.RelativePath] = new DeliveryFingerprint(file.SizeBytes, file.LastWriteTimeUtc);
        }

        return map;
    }

    internal static DeliveryVersionPlan DetermineVersionFromHistory(
        string stablePath,
        DeliveryHistory history,
        IReadOnlyList<DeliveryFile> sourceFiles)
    {
        var sourceSet = BuildFileSet(sourceFiles);
        var historySet = BuildFileSet(history.LastFiles);
        var currentVersion = NormalizeVersionLabel(history.CurrentVersion);
        if (historySet.Count > 0 && sourceSet.Count > 0 && SetsMatch(sourceSet, historySet))
        {
            var label = currentVersion ?? "v1";
            return new DeliveryVersionPlan(label, stablePath, CombineDropboxPath(stablePath, label), false, false);
        }

        if (string.IsNullOrWhiteSpace(currentVersion))
        {
            return new DeliveryVersionPlan("v1", stablePath, CombineDropboxPath(stablePath, "v1"), true, false);
        }

        var currentNumber = ParseVersionNumber(currentVersion);
        var nextNumber = currentNumber > 0 ? currentNumber + 1 : 2;
        var nextLabel = $"v{nextNumber}";
        return new DeliveryVersionPlan(nextLabel, stablePath, CombineDropboxPath(stablePath, nextLabel), true, false);
    }

    private static Dictionary<string, DeliveryFingerprint> BuildFileSet(
        string rootPath,
        IReadOnlyList<string> excludeDirs)
    {
        var exclusions = excludeDirs
            .Select(NormalizePath)
            .ToArray();

        var map = new Dictionary<string, DeliveryFingerprint>(StringComparer.OrdinalIgnoreCase);
        foreach (var filePath in Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories))
        {
            if (IsUnderAny(filePath, exclusions))
            {
                continue;
            }

            var name = Path.GetFileName(filePath);
            if (IsIgnoredFileName(name))
            {
                continue;
            }

            var extension = Path.GetExtension(name);
            if (!AllowedExtensions.Contains(extension))
            {
                continue;
            }

            var info = new FileInfo(filePath);
            var relative = Path.GetRelativePath(rootPath, filePath);
            map[relative] = new DeliveryFingerprint(info.Length, info.LastWriteTimeUtc);
        }

        return map;
    }

    private static bool IsUnderAny(string filePath, IReadOnlyList<string> prefixes)
    {
        var normalized = NormalizePath(filePath);
        foreach (var prefix in prefixes)
        {
            if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool SetsMatch(
        IReadOnlyDictionary<string, DeliveryFingerprint> left,
        IReadOnlyDictionary<string, DeliveryFingerprint> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        foreach (var pair in left)
        {
            if (!right.TryGetValue(pair.Key, out var rightValue))
            {
                return false;
            }

            if (pair.Value.SizeBytes != rightValue.SizeBytes)
            {
                return false;
            }

            if (pair.Value.LastWriteTimeUtc != rightValue.LastWriteTimeUtc)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsVersionFolderName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        return name.Length > 1
               && (name[0] == 'v' || name[0] == 'V')
               && int.TryParse(name[1..], out _);
    }

    internal sealed record ShareLinkDecision(bool ShouldCreate, bool ReuseExisting, string Reason);

    private static int ParseVersionNumber(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return 0;
        }

        if (name.Length <= 1 || (name[0] != 'v' && name[0] != 'V'))
        {
            return 0;
        }

        return int.TryParse(name[1..], out var number) ? number : 0;
    }

    private static string? NormalizeVersionLabel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (IsVersionFolderName(trimmed))
        {
            return trimmed.StartsWith("v", StringComparison.Ordinal) ? trimmed : $"v{trimmed[1..]}";
        }

        return null;
    }

    internal static string CombineDropboxPath(params string[] segments)
    {
        var parts = new List<string>();
        foreach (var segment in segments)
        {
            if (string.IsNullOrWhiteSpace(segment))
            {
                continue;
            }

            var split = segment.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in split)
            {
                var trimmed = part.Trim();
                if (!string.IsNullOrWhiteSpace(trimmed))
                {
                    parts.Add(trimmed);
                }
            }
        }

        return "/" + string.Join("/", parts);
    }

    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    internal static string ResolveDeliveryManifestPath(string containerRoot)
    {
        var manifestDir = Path.Combine(containerRoot, "00_Admin", ".mgf", "manifest");
        return Path.Combine(manifestDir, "delivery_manifest.json");
    }

    internal async Task<DeliveryShareOutcome> EnsureDropboxShareLinkAsync(
        string dropboxRootPath,
        string stablePath,
        string dropboxDeliveryRelpath,
        DeliveryShareState existing,
        bool refreshRequested,
        bool testMode,
        CancellationToken cancellationToken,
        string? apiStablePathOverride = null)
    {
        var stablePathForCheck = apiStablePathOverride ?? stablePath;
        if (!IsStableSharePath(stablePathForCheck))
        {
            return new DeliveryShareOutcome(
                ShareStatus: "failed",
                ShareUrl: existing.ShareUrl,
                ShareId: existing.ShareId,
                ShareError: "Stable share path resolved to a version folder; refusing to create a share link.",
                VerifiedAtUtc: null
            );
        }

        var tokenResult = await accessTokenProvider.GetAccessTokenAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(tokenResult.AccessToken))
        {
            return new DeliveryShareOutcome(
                ShareStatus: "failed",
                ShareUrl: existing.ShareUrl,
                ShareId: existing.ShareId,
                ShareError: tokenResult.Error ?? "Dropbox access token not configured; share link not created.",
                VerifiedAtUtc: null
            );
        }

        var decision = DetermineShareLinkDecision(existing, refreshRequested, testMode, DateTimeOffset.UtcNow);
        if (decision.ReuseExisting && !string.IsNullOrWhiteSpace(existing.ShareUrl))
        {
            return new DeliveryShareOutcome(
                ShareStatus: "reused",
                ShareUrl: existing.ShareUrl,
                ShareId: existing.ShareId,
                ShareError: null,
                VerifiedAtUtc: DateTimeOffset.UtcNow
            );
        }

        try
        {
            var useApiRoot = configuration.GetValue("Integrations:Dropbox:UseApiRootFolder", false)
                || configuration.GetValue("Dropbox:UseApiRootFolder", false);
            var apiRoot = ResolveDropboxApiRootFolder();
            if (useApiRoot && string.IsNullOrWhiteSpace(apiRoot))
            {
                return new DeliveryShareOutcome(
                    ShareStatus: "failed",
                    ShareUrl: existing.ShareUrl,
                    ShareId: existing.ShareId,
                    ShareError: "Dropbox ApiRootFolder not configured; cannot create share link.",
                    VerifiedAtUtc: null
                );
            }

            if (apiStablePathOverride is null && !stablePath.StartsWith("/", StringComparison.Ordinal))
            {
                WarnIfLocalSyncRootMismatch(stablePath);
            }

            var dropboxPath = apiStablePathOverride ?? (useApiRoot
                ? BuildDropboxApiPath(apiRoot, stablePath, dropboxDeliveryRelpath)
                : BuildDropboxApiPathFromLocalRoot(dropboxRootPath, stablePath));
            logger?.LogInformation("MGF.Worker: Dropbox share link path={DropboxPath}", dropboxPath);

            await shareLinkClient.ValidateAccessTokenAsync(tokenResult.AccessToken, cancellationToken);
            var shared = await shareLinkClient.GetOrCreateSharedLinkAsync(tokenResult.AccessToken, dropboxPath, cancellationToken);
            return new DeliveryShareOutcome(
                ShareStatus: shared.IsNew ? "created" : "reused",
                ShareUrl: shared.Url,
                ShareId: shared.Id,
                ShareError: null,
                VerifiedAtUtc: DateTimeOffset.UtcNow
            );
        }
        catch (Exception ex)
        {
            return new DeliveryShareOutcome(
                ShareStatus: "failed",
                ShareUrl: existing.ShareUrl,
                ShareId: existing.ShareId,
                ShareError: $"Dropbox share link failed: {ex.Message}",
                VerifiedAtUtc: null
            );
        }
    }

    internal static ShareLinkDecision DetermineShareLinkDecision(
        DeliveryShareState existing,
        bool refreshRequested,
        bool testMode,
        DateTimeOffset nowUtc)
    {
        var hasShareUrl = !string.IsNullOrWhiteSpace(existing.ShareUrl);
        var statusFailed = string.Equals(existing.ShareStatus, "failed", StringComparison.OrdinalIgnoreCase);

        if (hasShareUrl && !refreshRequested && !statusFailed)
        {
            if (!testMode && existing.LastVerifiedAtUtc.HasValue)
            {
                var age = nowUtc - existing.LastVerifiedAtUtc.Value;
                if (age > TimeSpan.FromDays(ShareLinkTtlDays))
                {
                    return new ShareLinkDecision(true, false, "ttl_expired");
                }
            }

            return new ShareLinkDecision(false, true, "reuse_existing");
        }

        if (!hasShareUrl)
        {
            return new ShareLinkDecision(true, false, "missing_share");
        }

        if (refreshRequested)
        {
            return new ShareLinkDecision(true, false, "refresh_requested");
        }

        if (statusFailed)
        {
            return new ShareLinkDecision(true, false, "previous_failed");
        }

        return new ShareLinkDecision(true, false, "refresh_fallback");
    }

    internal static string BuildDropboxApiPath(string apiRootFolder, string stablePath, string dropboxDeliveryRelpath)
    {
        if (string.IsNullOrWhiteSpace(apiRootFolder))
        {
            throw new InvalidOperationException("Dropbox ApiRootFolder is required for share links.");
        }

        var apiRoot = apiRootFolder.Trim().Trim('/');
        if (string.IsNullOrWhiteSpace(apiRoot))
        {
            throw new InvalidOperationException("Dropbox ApiRootFolder is empty after normalization.");
        }

        var stableSegments = SplitPathSegments(stablePath);
        var deliverySegments = SplitPathSegments(dropboxDeliveryRelpath);
        if (deliverySegments.Length == 0)
        {
            throw new InvalidOperationException("Dropbox delivery root relpath is empty.");
        }

        var startIndex = FindSegmentSequence(stableSegments, deliverySegments);
        if (startIndex < 0)
        {
            throw new InvalidOperationException($"Stable path is outside Dropbox delivery root: {stablePath}");
        }

        var relativeSegments = stableSegments[startIndex..];
        var relativePath = string.Join("/", relativeSegments);
        return "/" + apiRoot + "/" + relativePath;
    }

    internal static string BuildDropboxApiContainerRoot(
        string apiRootFolder,
        string dropboxDeliveryRelpath,
        string clientFolder,
        string projectFolderName)
    {
        if (string.IsNullOrWhiteSpace(apiRootFolder))
        {
            throw new InvalidOperationException("Dropbox ApiRootFolder is required for delivery.");
        }

        if (string.IsNullOrWhiteSpace(dropboxDeliveryRelpath))
        {
            throw new InvalidOperationException("Dropbox delivery root relpath is required.");
        }

        return CombineDropboxPath(apiRootFolder, dropboxDeliveryRelpath, clientFolder, projectFolderName);
    }

    private string ResolveDropboxApiRootFolder()
    {
        var configured = configuration["Integrations:Dropbox:ApiRootFolder"]
            ?? configuration["Dropbox:ApiRootFolder"];
        if (string.IsNullOrWhiteSpace(configured))
        {
            return string.Empty;
        }

        return configured;
    }

    private static string BuildDropboxApiPathFromLocalRoot(string dropboxRootPath, string stablePath)
    {
        var root = dropboxRootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var relative = Path.GetRelativePath(root, stablePath);
        if (string.IsNullOrWhiteSpace(relative) || relative.StartsWith("..", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Stable path is outside Dropbox root: {stablePath}");
        }

        var trimmed = relative.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return "/" + trimmed.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
    }

    private void WarnIfLocalSyncRootMismatch(string stablePath)
    {
        var hint = configuration["Integrations:Dropbox:LocalSyncRootHint"]
            ?? configuration["Dropbox:LocalSyncRootHint"];
        if (string.IsNullOrWhiteSpace(hint))
        {
            return;
        }

        var normalizedHint = hint.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalizedPath = stablePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!normalizedPath.StartsWith(normalizedHint + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(normalizedPath, normalizedHint, StringComparison.OrdinalIgnoreCase))
        {
            logger?.LogWarning(
                "MGF.Worker: Dropbox stable path is outside LocalSyncRootHint (stablePath={StablePath}, hint={Hint})",
                stablePath,
                hint);
        }
    }

    private static string[] SplitPathSegments(string path)
    {
        return path
            .Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => segment.Trim())
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .ToArray();
    }

    private static int FindSegmentSequence(string[] haystack, string[] needle)
    {
        if (needle.Length == 0 || haystack.Length < needle.Length)
        {
            return -1;
        }

        for (var i = 0; i <= haystack.Length - needle.Length; i++)
        {
            var match = true;
            for (var j = 0; j < needle.Length; j++)
            {
                if (!string.Equals(haystack[i + j], needle[j], StringComparison.OrdinalIgnoreCase))
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                return i;
            }
        }

        return -1;
    }

    internal static bool IsStableSharePath(string path)
    {
        var trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var lastSegment = Path.GetFileName(trimmed);
        return !IsVersionFolderName(lastSegment);
    }

    private static async Task WriteDeliveryManifestAsync(
        string manifestPath,
        DeliveryManifest manifest,
        CancellationToken cancellationToken)
    {
        var manifestDir = Path.GetDirectoryName(manifestPath);
        if (!string.IsNullOrWhiteSpace(manifestDir))
        {
            Directory.CreateDirectory(manifestDir);
        }

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        var json = JsonSerializer.Serialize(manifest, options);
        await File.WriteAllTextAsync(manifestPath, json, cancellationToken);
    }

    internal static DeliveryManifest BuildDeliveryManifest(
        string projectId,
        ProvisioningTokens tokens,
        string sourcePath,
        string stablePath,
        string versionPath,
        string versionLabel,
        DateTimeOffset retentionUntilUtc,
        IReadOnlyList<DeliveryFile> files,
        string? stableShareUrl,
        string? apiStablePath,
        string? apiVersionPath)
    {
        return new DeliveryManifest
        {
            SchemaVersion = 2,
            DeliveryRunId = Guid.NewGuid().ToString("N"),
            CreatedAtUtc = DateTimeOffset.UtcNow,
            ProjectId = projectId,
            ProjectCode = tokens.ProjectCode ?? string.Empty,
            ProjectName = tokens.ProjectName ?? string.Empty,
            ClientName = tokens.ClientName ?? string.Empty,
            SourcePath = sourcePath,
            DestinationPath = stablePath,
            StablePath = stablePath,
            VersionPath = versionPath,
            ApiStablePath = apiStablePath,
            ApiVersionPath = apiVersionPath,
            VersionLabel = versionLabel,
            CurrentVersion = versionLabel,
            StableShareUrl = stableShareUrl,
            RetentionUntilUtc = retentionUntilUtc,
            Files = files.Select(ToSummary).ToArray()
        };
    }

    private sealed record DeliveryFingerprint(long SizeBytes, DateTimeOffset LastWriteTimeUtc);

    private static DeliveryFileSummary ToSummary(DeliveryFile file)
        => new(file.RelativePath, file.SizeBytes, file.LastWriteTimeUtc);

    private static DeliveryFileSummary ToSummary(DeliveryFileSummary file)
        => file;

    private static DeliveryEmailFileSummary ToEmailSummary(DeliveryFile file)
        => new(file.RelativePath, file.SizeBytes, file.LastWriteTimeUtc);

    private static DeliveryEmailFileSummary ToEmailSummary(DeliveryFileSummary file)
        => new(file.RelativePath, file.SizeBytes, file.LastWriteTimeUtc);

    private string ResolveDomainRootPath(string domainKey)
    {
        var raw = domainKey switch
        {
            "dropbox" => configuration["Storage:DropboxRoot"] ?? string.Empty,
            "lucidlink" => configuration["Storage:LucidLinkRoot"] ?? string.Empty,
            "nas" => configuration["Storage:NasRoot"] ?? string.Empty,
            _ => string.Empty
        };

        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        return Path.GetFullPath(raw.Trim());
    }

    private static string? TryBuildLocalStablePath(
        string? rootPath,
        string dropboxDeliveryRelpath,
        string clientFolder,
        string projectFolderName,
        bool testMode)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            return null;
        }

        var baseRoot = testMode
            ? Path.Combine(rootPath, "99_TestRuns")
            : rootPath;

        var basePath = Path.Combine(baseRoot, dropboxDeliveryRelpath, clientFolder);
        var targetPath = Path.Combine(basePath, projectFolderName);
        return Path.Combine(targetPath, "01_Deliverables", "Final");
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

    private static IReadOnlyList<string> NormalizeEmailList(IEnumerable<string> values)
    {
        return values
            .SelectMany(value => (value ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static EmailSendResult FailedEmailResult(
        IReadOnlyList<string> recipients,
        string subject,
        string error,
        string? replyTo)
    {
        return new EmailSendResult(
            Status: "failed",
            Provider: "email",
            FromAddress: DeliveryFromAddress,
            To: recipients,
            Subject: subject,
            SentAtUtc: null,
            ProviderMessageId: null,
            Error: error,
            TemplateVersion: DeliveryEmailTemplateVersion,
            ReplyTo: replyTo
        );
    }

    private static EmailSendResult SkippedEmailResult(
        IReadOnlyList<string> recipients,
        string subject,
        string reason,
        string? replyTo)
    {
        return new EmailSendResult(
            Status: "skipped",
            Provider: "email",
            FromAddress: DeliveryFromAddress,
            To: recipients,
            Subject: subject,
            SentAtUtc: null,
            ProviderMessageId: null,
            Error: reason,
            TemplateVersion: DeliveryEmailTemplateVersion,
            ReplyTo: replyTo
        );
    }

    private static DeliveryShareState ReadShareState(JsonElement metadata)
    {
        try
        {
            using var doc = JsonDocument.Parse(metadata.GetRawText());
            if (!doc.RootElement.TryGetProperty("delivery", out var delivery))
            {
                return new DeliveryShareState(null, null, null, null);
            }

            if (!delivery.TryGetProperty("current", out var current))
            {
                return new DeliveryShareState(null, null, null, null);
            }

            var shareUrl = TryGetString(current, "stableShareUrl") ?? TryGetString(current, "shareUrl");
            var shareId = TryGetString(current, "stableShareId") ?? TryGetString(current, "shareId");
            var shareStatus = TryGetString(current, "shareStatus");
            var lastVerified = TryGetDateTimeOffset(current, "lastShareVerifiedAtUtc");

            return new DeliveryShareState(shareUrl, shareId, shareStatus, lastVerified);
        }
        catch
        {
            return new DeliveryShareState(null, null, null, null);
        }
    }

    private static DeliveryHistory ReadDeliveryHistory(JsonElement metadata)
    {
        try
        {
            using var doc = JsonDocument.Parse(metadata.GetRawText());
            if (!doc.RootElement.TryGetProperty("delivery", out var delivery))
            {
                return new DeliveryHistory(null, Array.Empty<DeliveryFileSummary>());
            }

            string? currentVersion = null;
            if (delivery.TryGetProperty("current", out var current))
            {
                currentVersion = TryGetString(current, "currentVersion");
            }

            var lastFiles = Array.Empty<DeliveryFileSummary>();
            if (delivery.TryGetProperty("runs", out var runs) && runs.ValueKind == JsonValueKind.Array)
            {
                for (var index = runs.GetArrayLength() - 1; index >= 0; index--)
                {
                    var run = runs[index];
                    var versionLabel = TryGetString(run, "versionLabel");
                    if (string.IsNullOrWhiteSpace(currentVersion) && !string.IsNullOrWhiteSpace(versionLabel))
                    {
                        currentVersion = versionLabel;
                    }

                    if (run.TryGetProperty("files", out var filesElement)
                        && filesElement.ValueKind == JsonValueKind.Array)
                    {
                        var parsed = ReadDeliveryFiles(filesElement);
                        if (parsed.Count > 0)
                        {
                            lastFiles = parsed.ToArray();
                            break;
                        }
                    }
                }
            }

            return new DeliveryHistory(currentVersion, lastFiles);
        }
        catch
        {
            return new DeliveryHistory(null, Array.Empty<DeliveryFileSummary>());
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

    private static string FindRepoRoot()
    {
        var startPaths = new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory };
        foreach (var start in startPaths)
        {
            var current = new DirectoryInfo(start);
            while (current is not null)
            {
                if (File.Exists(Path.Combine(current.FullName, "MGF.sln")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }
        }

        throw new InvalidOperationException($"Could not locate repo root (MGF.sln) from {Directory.GetCurrentDirectory()}.");
    }

    private static string ResolveTemplatesRoot()
    {
        var baseDir = AppContext.BaseDirectory;
        var templatesRoot = Path.Combine(baseDir, "artifacts", "templates");
        if (Directory.Exists(templatesRoot))
        {
            return templatesRoot;
        }

        throw new DirectoryNotFoundException($"Templates folder not found at {templatesRoot}.");
    }

    private static string ResolveDeliveryTemplatePath()
    {
        var templatesRoot = ResolveTemplatesRoot();
        return Path.Combine(templatesRoot, "dropbox_delivery_container.json");
    }
}



