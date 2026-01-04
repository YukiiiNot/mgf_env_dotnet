namespace MGF.UseCases.Operations.ProjectArchive.RunProjectArchive;

using System.Text.Json;
using MGF.Contracts.Abstractions;
using MGF.Contracts.Abstractions.ProjectArchive;
using MGF.Contracts.Abstractions.ProjectWorkflows;

public sealed class RunProjectArchiveUseCase : IRunProjectArchiveUseCase
{
    private static readonly JsonSerializerOptions ArchiveJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IProjectRepository projectRepository;
    private readonly IClientRepository clientRepository;
    private readonly IProjectArchiveData archiveData;
    private readonly IProjectArchiveStore archiveStore;
    private readonly IProjectArchiveExecutor archiveExecutor;
    private readonly IProjectWorkflowLock workflowLock;

    public RunProjectArchiveUseCase(
        IProjectRepository projectRepository,
        IClientRepository clientRepository,
        IProjectArchiveData archiveData,
        IProjectArchiveStore archiveStore,
        IProjectArchiveExecutor archiveExecutor,
        IProjectWorkflowLock workflowLock)
    {
        this.projectRepository = projectRepository;
        this.clientRepository = clientRepository;
        this.archiveData = archiveData;
        this.archiveStore = archiveStore;
        this.archiveExecutor = archiveExecutor;
        this.workflowLock = workflowLock;
    }

    public async Task<RunProjectArchiveResult> ExecuteAsync(
        RunProjectArchiveRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Payload is null)
        {
            throw new InvalidOperationException("ProjectArchive payload is required.");
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
        var results = new List<ProjectArchiveDomainResult>();

        if (!payload.AllowNonReal && !string.Equals(project.DataProfile, "real", StringComparison.OrdinalIgnoreCase))
        {
            var blocked = BuildBlockedResults(
                "blocked_non_real",
                $"Project data_profile='{project.DataProfile}' is not eligible for archive."
            );
            var blockedResult = new ProjectArchiveRunResult(
                JobId: request.JobId,
                ProjectId: payload.ProjectId,
                EditorInitials: payload.EditorInitials,
                StartedAtUtc: startedAt,
                TestMode: payload.TestMode,
                AllowTestCleanup: payload.AllowTestCleanup,
                AllowNonReal: payload.AllowNonReal,
                Force: payload.Force,
                Domains: blocked,
                HasErrors: true,
                LastError: $"Project data_profile='{project.DataProfile}' is not eligible for archive."
            );

            await AppendArchiveRunAsync(archiveStore, project.ProjectId, project.Metadata, blockedResult, cancellationToken);
            return new RunProjectArchiveResult(blockedResult);
        }

        if (!ProjectArchiveGuards.TryValidateStart(project.StatusKey, payload.Force, out var statusError, out var alreadyArchiving))
        {
            var rootState = alreadyArchiving ? "blocked_already_archiving" : "blocked_status_not_ready";
            var blocked = BuildBlockedResults(rootState, statusError ?? "Project status not eligible for archiving.");
            var blockedResult = new ProjectArchiveRunResult(
                JobId: request.JobId,
                ProjectId: payload.ProjectId,
                EditorInitials: payload.EditorInitials,
                StartedAtUtc: startedAt,
                TestMode: payload.TestMode,
                AllowTestCleanup: payload.AllowTestCleanup,
                AllowNonReal: payload.AllowNonReal,
                Force: payload.Force,
                Domains: blocked,
                HasErrors: true,
                LastError: statusError
            );

            await AppendArchiveRunAsync(archiveStore, project.ProjectId, project.Metadata, blockedResult, cancellationToken);
            return new RunProjectArchiveResult(blockedResult);
        }

        await using var lockLease = await workflowLock.TryAcquireAsync(
            project.ProjectId,
            ProjectWorkflowKinds.StorageMutation,
            request.JobId,
            cancellationToken);
        if (lockLease is null)
        {
            throw new ProjectWorkflowLockUnavailableException(project.ProjectId, ProjectWorkflowKinds.StorageMutation);
        }

        await archiveStore.UpdateProjectStatusAsync(
            project.ProjectId,
            ProjectArchiveGuards.StatusArchiving,
            cancellationToken);

        var client = await clientRepository.GetByIdAsync(project.ClientId, cancellationToken);
        var tokens = new ProjectArchiveTokens(
            project.ProjectCode,
            project.Name,
            client?.DisplayName,
            payload.EditorInitials);

        var pathTemplates = await archiveData.GetArchivePathTemplatesAsync(cancellationToken);
        var projectFolderName = await archiveExecutor.ResolveProjectFolderNameAsync(tokens, cancellationToken);

        var dropboxResult = await archiveExecutor.ProcessDropboxAsync(
            payload,
            projectFolderName,
            pathTemplates,
            cancellationToken);
        var lucidlinkResult = await archiveExecutor.ProcessLucidlinkAsync(payload, projectFolderName);
        var nasResult = await archiveExecutor.ProcessNasAsync(
            payload,
            projectFolderName,
            lucidlinkResult,
            tokens,
            pathTemplates,
            cancellationToken);

        results.Add(dropboxResult);
        results.Add(lucidlinkResult);
        results.Add(nasResult);

        if (IsDomainSuccess(nasResult)
            && dropboxResult.RootState is "ready_to_archive" or "already_archived")
        {
            var updatedDropbox = await archiveExecutor.FinalizeDropboxArchiveAsync(
                dropboxResult,
                payload,
                projectFolderName,
                pathTemplates,
                cancellationToken
            );
            results[0] = updatedDropbox;
            dropboxResult = updatedDropbox;
        }

        var hasErrors = results.Any(IsDomainError);
        var lastError = hasErrors ? BuildLastError(results) : null;

        var runResult = new ProjectArchiveRunResult(
            JobId: request.JobId,
            ProjectId: payload.ProjectId,
            EditorInitials: payload.EditorInitials,
            StartedAtUtc: startedAt,
            TestMode: payload.TestMode,
            AllowTestCleanup: payload.AllowTestCleanup,
            AllowNonReal: payload.AllowNonReal,
            Force: payload.Force,
            Domains: results,
            HasErrors: hasErrors,
            LastError: lastError
        );

        await AppendArchiveRunAsync(archiveStore, project.ProjectId, project.Metadata, runResult, cancellationToken);

        var finalStatus = hasErrors ? ProjectArchiveGuards.StatusArchiveFailed : ProjectArchiveGuards.StatusArchived;
        await archiveStore.UpdateProjectStatusAsync(project.ProjectId, finalStatus, cancellationToken);

        return new RunProjectArchiveResult(runResult);
    }

    private static bool IsDomainSuccess(ProjectArchiveDomainResult result)
    {
        return result.RootState is "archived" or "already_archived" or "archive_verified" or "archive_repaired" or "source_found";
    }

    private static bool IsDomainError(ProjectArchiveDomainResult result)
    {
        if (result.RootState.StartsWith("blocked_", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (result.RootState.EndsWith("_failed", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (result.RootState.StartsWith("cleanup_", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return result.RootState is "container_missing" or "source_missing" or "archive_move_missing";
    }

    private static string? BuildLastError(IReadOnlyList<ProjectArchiveDomainResult> results)
    {
        foreach (var result in results)
        {
            if (result.Notes.Count > 0 && IsDomainError(result))
            {
                return result.Notes[0];
            }

            if (result.TargetProvisioning?.Errors.Count > 0)
            {
                return result.TargetProvisioning.Errors[0];
            }
        }

        return "project.archive completed with errors.";
    }

    private static List<ProjectArchiveDomainResult> BuildBlockedResults(string rootState, string note)
    {
        return new List<ProjectArchiveDomainResult>
        {
            new(
                DomainKey: "dropbox",
                RootPath: string.Empty,
                RootState: rootState,
                DomainRootProvisioning: null,
                TargetProvisioning: null,
                Actions: Array.Empty<ArchiveActionSummary>(),
                Notes: new[] { note }
            ),
            new(
                DomainKey: "lucidlink",
                RootPath: string.Empty,
                RootState: rootState,
                DomainRootProvisioning: null,
                TargetProvisioning: null,
                Actions: Array.Empty<ArchiveActionSummary>(),
                Notes: new[] { note }
            ),
            new(
                DomainKey: "nas",
                RootPath: string.Empty,
                RootState: rootState,
                DomainRootProvisioning: null,
                TargetProvisioning: null,
                Actions: Array.Empty<ArchiveActionSummary>(),
                Notes: new[] { note }
            )
        };
    }

    private static Task AppendArchiveRunAsync(
        IProjectArchiveStore store,
        string projectId,
        JsonElement metadata,
        ProjectArchiveRunResult runResult,
        CancellationToken cancellationToken)
    {
        var runResultJson = JsonSerializer.SerializeToElement(runResult, ArchiveJsonOptions);
        return store.AppendArchiveRunAsync(projectId, metadata, runResultJson, cancellationToken);
    }
}
