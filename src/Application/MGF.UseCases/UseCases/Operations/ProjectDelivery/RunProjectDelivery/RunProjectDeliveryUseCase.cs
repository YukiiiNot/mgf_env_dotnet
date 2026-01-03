namespace MGF.UseCases.Operations.ProjectDelivery.RunProjectDelivery;

using System.Text.Json;
using MGF.Contracts.Abstractions;
using MGF.Contracts.Abstractions.Email;
using MGF.Contracts.Abstractions.ProjectDelivery;

public sealed class RunProjectDeliveryUseCase : IRunProjectDeliveryUseCase
{
    private static readonly JsonSerializerOptions DeliveryJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IProjectRepository projectRepository;
    private readonly IClientRepository clientRepository;
    private readonly IProjectDeliveryData deliveryData;
    private readonly IProjectDeliveryStore deliveryStore;
    private readonly IProjectDeliveryExecutor deliveryExecutor;

    public RunProjectDeliveryUseCase(
        IProjectRepository projectRepository,
        IClientRepository clientRepository,
        IProjectDeliveryData deliveryData,
        IProjectDeliveryStore deliveryStore,
        IProjectDeliveryExecutor deliveryExecutor)
    {
        this.projectRepository = projectRepository;
        this.clientRepository = clientRepository;
        this.deliveryData = deliveryData;
        this.deliveryStore = deliveryStore;
        this.deliveryExecutor = deliveryExecutor;
    }

    public async Task<RunProjectDeliveryResult> ExecuteAsync(
        RunProjectDeliveryRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Payload is null)
        {
            throw new InvalidOperationException("ProjectDelivery payload is required.");
        }

        if (string.IsNullOrWhiteSpace(request.JobId))
        {
            throw new InvalidOperationException("JobId is required.");
        }

        var payload = request.Payload;
        var project = await projectRepository.GetByIdAsync(payload.ProjectId, cancellationToken);
        if (project is null)
        {
            throw new InvalidOperationException($"Project not found: {payload.ProjectId}");
        }

        var startedAt = DateTimeOffset.UtcNow;

        if (!payload.AllowNonReal && !string.Equals(project.DataProfile, "real", StringComparison.OrdinalIgnoreCase))
        {
            var blocked = BuildBlockedResults(
                "blocked_non_real",
                $"Project data_profile='{project.DataProfile}' is not eligible for delivery."
            );

            var blockedResult = new ProjectDeliveryRunResult(
                JobId: request.JobId,
                ProjectId: payload.ProjectId,
                EditorInitials: payload.EditorInitials,
                StartedAtUtc: startedAt,
                TestMode: payload.TestMode,
                AllowTestCleanup: payload.AllowTestCleanup,
                AllowNonReal: payload.AllowNonReal,
                Force: payload.Force,
                SourcePath: null,
                DestinationPath: null,
                ApiStablePath: null,
                ApiVersionPath: null,
                VersionLabel: null,
                RetentionUntilUtc: null,
                Files: Array.Empty<DeliveryFileSummary>(),
                Domains: blocked,
                HasErrors: true,
                LastError: $"Project data_profile='{project.DataProfile}' is not eligible for delivery.",
                ShareStatus: null,
                ShareUrl: null,
                ShareId: null,
                ShareError: null,
                Email: null
            );

            await AppendDeliveryRunAsync(deliveryStore, project.ProjectId, project.Metadata, blockedResult, cancellationToken);
            return new RunProjectDeliveryResult(blockedResult);
        }

        if (!ProjectDeliveryGuards.TryValidateStart(project.StatusKey, payload.Force, out var statusError, out var alreadyDelivering))
        {
            var rootState = alreadyDelivering ? "blocked_already_delivering" : "blocked_status_not_ready";
            var blocked = BuildBlockedResults(rootState, statusError ?? "Project status not eligible for delivery.");

            var blockedResult = new ProjectDeliveryRunResult(
                JobId: request.JobId,
                ProjectId: payload.ProjectId,
                EditorInitials: payload.EditorInitials,
                StartedAtUtc: startedAt,
                TestMode: payload.TestMode,
                AllowTestCleanup: payload.AllowTestCleanup,
                AllowNonReal: payload.AllowNonReal,
                Force: payload.Force,
                SourcePath: null,
                DestinationPath: null,
                ApiStablePath: null,
                ApiVersionPath: null,
                VersionLabel: null,
                RetentionUntilUtc: null,
                Files: Array.Empty<DeliveryFileSummary>(),
                Domains: blocked,
                HasErrors: true,
                LastError: statusError,
                ShareStatus: null,
                ShareUrl: null,
                ShareId: null,
                ShareError: null,
                Email: null
            );

            await AppendDeliveryRunAsync(deliveryStore, project.ProjectId, project.Metadata, blockedResult, cancellationToken);
            return new RunProjectDeliveryResult(blockedResult);
        }

        await deliveryStore.UpdateProjectStatusAsync(project.ProjectId, ProjectDeliveryGuards.StatusDelivering, cancellationToken);

        var client = await clientRepository.GetByIdAsync(project.ClientId, cancellationToken);
        var tokens = new DeliveryTokens(project.ProjectCode, project.Name, client?.DisplayName, payload.EditorInitials);
        var dropboxDeliveryRelpath = await deliveryData.GetDropboxDeliveryRelpathAsync(cancellationToken);
        var storageRelpath = await deliveryData.GetProjectStorageRootRelpathAsync(
            project.ProjectId,
            "lucidlink",
            payload.TestMode,
            cancellationToken);

        var sourceResult = await deliveryExecutor.ResolveLucidlinkSourceAsync(payload, storageRelpath, cancellationToken);
        var results = new List<ProjectDeliveryDomainResult> { sourceResult.DomainResult };

        if (!string.Equals(sourceResult.DomainResult.RootState, "source_ready", StringComparison.OrdinalIgnoreCase))
        {
            var blocked = BuildBlockedResult("dropbox", "blocked_source_missing", "LucidLink source not ready.");
            results.Add(blocked);

            var blockedRun = new ProjectDeliveryRunResult(
                JobId: request.JobId,
                ProjectId: payload.ProjectId,
                EditorInitials: payload.EditorInitials,
                StartedAtUtc: startedAt,
                TestMode: payload.TestMode,
                AllowTestCleanup: payload.AllowTestCleanup,
                AllowNonReal: payload.AllowNonReal,
                Force: payload.Force,
                SourcePath: sourceResult.SourcePath,
                DestinationPath: null,
                ApiStablePath: null,
                ApiVersionPath: null,
                VersionLabel: null,
                RetentionUntilUtc: null,
                Files: sourceResult.Files.Select(ToSummary).ToArray(),
                Domains: results,
                HasErrors: true,
                LastError: sourceResult.DomainResult.Notes.FirstOrDefault(),
                ShareStatus: null,
                ShareUrl: null,
                ShareId: null,
                ShareError: null,
                Email: null
            );

            await AppendDeliveryRunAsync(deliveryStore, project.ProjectId, project.Metadata, blockedRun, cancellationToken);
            await deliveryStore.UpdateProjectStatusAsync(project.ProjectId, ProjectDeliveryGuards.StatusDeliveryFailed, cancellationToken);
            return new RunProjectDeliveryResult(blockedRun);
        }

        var dropboxResult = await deliveryExecutor.ProcessDropboxAsync(
            payload,
            tokens,
            sourceResult,
            dropboxDeliveryRelpath,
            project.Metadata,
            cancellationToken);

        results.Add(dropboxResult.DomainResult);

        var hasErrors = results.Any(IsDomainError);
        var lastError = hasErrors ? BuildLastError(results) : null;
        EmailSendResult? emailResult = null;
        if (!hasErrors)
        {
            emailResult = await deliveryExecutor.SendDeliveryEmailAsync(
                payload,
                tokens,
                sourceResult,
                dropboxResult,
                cancellationToken);
        }

        var runResult = new ProjectDeliveryRunResult(
            JobId: request.JobId,
            ProjectId: payload.ProjectId,
            EditorInitials: payload.EditorInitials,
            StartedAtUtc: startedAt,
            TestMode: payload.TestMode,
            AllowTestCleanup: payload.AllowTestCleanup,
            AllowNonReal: payload.AllowNonReal,
            Force: payload.Force,
            SourcePath: sourceResult.SourcePath,
            DestinationPath: dropboxResult.DestinationPath,
            ApiStablePath: dropboxResult.ApiStablePath,
            ApiVersionPath: dropboxResult.ApiVersionPath,
            VersionLabel: dropboxResult.VersionLabel,
            RetentionUntilUtc: dropboxResult.RetentionUntilUtc,
            Files: sourceResult.Files.Select(ToSummary).ToArray(),
            Domains: results,
            HasErrors: hasErrors,
            LastError: lastError,
            ShareStatus: dropboxResult.ShareStatus,
            ShareUrl: dropboxResult.ShareUrl,
            ShareId: dropboxResult.ShareId,
            ShareError: dropboxResult.ShareError,
            Email: emailResult
        );

        await AppendDeliveryRunAsync(deliveryStore, project.ProjectId, project.Metadata, runResult, cancellationToken);

        var finalStatus = hasErrors ? ProjectDeliveryGuards.StatusDeliveryFailed : ProjectDeliveryGuards.StatusDelivered;
        await deliveryStore.UpdateProjectStatusAsync(project.ProjectId, finalStatus, cancellationToken);

        return new RunProjectDeliveryResult(runResult);
    }

    private static DeliveryFileSummary ToSummary(DeliveryFile file)
        => new(file.RelativePath, file.SizeBytes, file.LastWriteTimeUtc);

    private static List<ProjectDeliveryDomainResult> BuildBlockedResults(string rootState, string note)
    {
        return new List<ProjectDeliveryDomainResult>
        {
            BuildBlockedResult("lucidlink", rootState, note),
            BuildBlockedResult("dropbox", rootState, note)
        };
    }

    private static ProjectDeliveryDomainResult BuildBlockedResult(string domainKey, string rootState, string note)
    {
        return new ProjectDeliveryDomainResult(
            DomainKey: domainKey,
            RootPath: string.Empty,
            RootState: rootState,
            DeliveryContainerProvisioning: null,
            Deliverables: Array.Empty<DeliveryFileSummary>(),
            VersionLabel: null,
            DestinationPath: null,
            Notes: new[] { note }
        );
    }

    private static string? BuildLastError(IReadOnlyList<ProjectDeliveryDomainResult> results)
    {
        foreach (var result in results)
        {
            if (result.Notes.Count > 0 && IsDomainError(result))
            {
                return result.Notes[0];
            }
        }

        return "project.delivery completed with errors.";
    }

    private static bool IsDomainError(ProjectDeliveryDomainResult result)
    {
        return result.RootState.StartsWith("blocked_", StringComparison.OrdinalIgnoreCase)
            || result.RootState.EndsWith("_failed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(result.RootState, "delivery_failed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(result.RootState, "cleanup_locked", StringComparison.OrdinalIgnoreCase)
            || string.Equals(result.RootState, "container_verify_failed", StringComparison.OrdinalIgnoreCase);
    }

    private static Task AppendDeliveryRunAsync(
        IProjectDeliveryStore store,
        string projectId,
        JsonElement metadata,
        ProjectDeliveryRunResult runResult,
        CancellationToken cancellationToken)
    {
        var runResultJson = JsonSerializer.SerializeToElement(runResult, DeliveryJsonOptions);
        return store.AppendDeliveryRunAsync(projectId, metadata, runResultJson, cancellationToken);
    }
}
