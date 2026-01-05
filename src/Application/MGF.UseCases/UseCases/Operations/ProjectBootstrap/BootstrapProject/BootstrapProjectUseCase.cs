namespace MGF.UseCases.Operations.ProjectBootstrap.BootstrapProject;

using System.Runtime.ExceptionServices;
using System.Text.Json;
using MGF.Contracts.Abstractions;
using MGF.Contracts.Abstractions.ProjectBootstrap;
using MGF.Contracts.Abstractions.ProjectWorkflows;

public sealed class BootstrapProjectUseCase : IBootstrapProjectUseCase
{
    private const string StatusReady = "ready_to_provision";
    private const string StatusProvisioning = "provisioning";
    private const string StatusActive = "active";
    private const string StatusProvisionFailed = "provision_failed";

    private static readonly JsonSerializerOptions BootstrapJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IProjectRepository projectRepository;
    private readonly IClientRepository clientRepository;
    private readonly IProjectBootstrapStore bootstrapStore;
    private readonly IProjectBootstrapProvisioningGateway provisioningGateway;
    private readonly IProjectWorkflowLock workflowLock;

    public BootstrapProjectUseCase(
        IProjectRepository projectRepository,
        IClientRepository clientRepository,
        IProjectBootstrapStore bootstrapStore,
        IProjectBootstrapProvisioningGateway provisioningGateway,
        IProjectWorkflowLock workflowLock)
    {
        this.projectRepository = projectRepository;
        this.clientRepository = clientRepository;
        this.bootstrapStore = bootstrapStore;
        this.provisioningGateway = provisioningGateway;
        this.workflowLock = workflowLock;
    }

    public async Task<BootstrapProjectResult> ExecuteAsync(
        BootstrapProjectRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ProjectId))
        {
            throw new InvalidOperationException("ProjectId is required.");
        }

        if (string.IsNullOrWhiteSpace(request.JobId))
        {
            throw new InvalidOperationException("JobId is required.");
        }

        var project = await projectRepository.GetByIdAsync(request.ProjectId, cancellationToken);
        if (project is null)
        {
            throw new InvalidOperationException($"Project not found: {request.ProjectId}");
        }

        var client = await clientRepository.GetByIdAsync(project.ClientId, cancellationToken);
        var context = new ProjectBootstrapContext(
            ProjectId: project.ProjectId,
            ProjectCode: project.ProjectCode,
            ProjectName: project.Name,
            ClientId: project.ClientId,
            ClientName: client?.DisplayName,
            StatusKey: project.StatusKey,
            DataProfile: project.DataProfile,
            Metadata: project.Metadata);

        if (!request.AllowNonReal && !string.Equals(project.DataProfile, "real", StringComparison.OrdinalIgnoreCase))
        {
            var blocked = provisioningGateway.BuildBlockedNonRealResult(context, request);
            await AppendRunAsync(project.ProjectId, project.Metadata, blocked, cancellationToken);
            return new BootstrapProjectResult(blocked);
        }

        if (!TryValidateStart(project.StatusKey, request.Force, out var statusError, out var alreadyProvisioning))
        {
            var blocked = provisioningGateway.BuildBlockedStatusResult(context, request, statusError, alreadyProvisioning);
            await AppendRunAsync(project.ProjectId, project.Metadata, blocked, cancellationToken);
            return new BootstrapProjectResult(blocked);
        }

        var scopeId = StorageMutationScopes.ForProject(project.ProjectId);
        await using var lockLease = await workflowLock.TryAcquireAsync(
            scopeId,
            ProjectWorkflowKinds.StorageMutation,
            request.JobId,
            cancellationToken);
        if (lockLease is null)
        {
            throw new ProjectWorkflowLockUnavailableException(scopeId, ProjectWorkflowKinds.StorageMutation);
        }

        await bootstrapStore.UpdateProjectStatusAsync(project.ProjectId, StatusProvisioning, cancellationToken);

        var execution = await provisioningGateway.ExecuteAsync(context, request, cancellationToken);
        var updatedRun = await ApplyStorageRootUpsertsAsync(
            project.ProjectId,
            execution.RunResult,
            execution.StorageRootCandidates,
            cancellationToken);

        updatedRun = RecalculateRunResult(updatedRun);

        await AppendRunAsync(project.ProjectId, project.Metadata, updatedRun, cancellationToken);

        var finalStatus = updatedRun.HasErrors ? StatusProvisionFailed : StatusActive;
        await bootstrapStore.UpdateProjectStatusAsync(project.ProjectId, finalStatus, cancellationToken);

        if (execution.Exception is not null)
        {
            ExceptionDispatchInfo.Capture(execution.Exception).Throw();
        }

        return new BootstrapProjectResult(updatedRun);
    }

    private async Task AppendRunAsync(
        string projectId,
        JsonElement metadata,
        ProjectBootstrapRunResult runResult,
        CancellationToken cancellationToken)
    {
        var runResultJson = JsonSerializer.SerializeToElement(runResult, BootstrapJsonOptions);
        await bootstrapStore.AppendProvisioningRunAsync(projectId, metadata, runResultJson, cancellationToken);
    }

    private async Task<ProjectBootstrapRunResult> ApplyStorageRootUpsertsAsync(
        string projectId,
        ProjectBootstrapRunResult runResult,
        IReadOnlyList<ProjectBootstrapStorageRootCandidate> candidates,
        CancellationToken cancellationToken)
    {
        if (candidates.Count == 0)
        {
            return runResult;
        }

        var updatedDomains = new Dictionary<string, ProjectBootstrapDomainResult>(StringComparer.OrdinalIgnoreCase);
        foreach (var domain in runResult.Domains)
        {
            updatedDomains[domain.DomainKey] = domain;
        }

        var updated = false;
        foreach (var candidate in candidates)
        {
            var error = await bootstrapStore.UpsertProjectStorageRootAsync(
                projectId,
                candidate.StorageProviderKey,
                candidate.RootKey,
                candidate.FolderRelpath,
                cancellationToken);

            if (string.IsNullOrWhiteSpace(error))
            {
                continue;
            }

            if (!updatedDomains.TryGetValue(candidate.DomainKey, out var domain))
            {
                continue;
            }

            var notes = domain.Notes.ToList();
            notes.Add(error);
            updatedDomains[candidate.DomainKey] = domain with
            {
                RootState = "storage_root_failed",
                Notes = notes
            };
            updated = true;
        }

        if (!updated)
        {
            return runResult;
        }

        var domains = runResult.Domains
            .Select(domain => updatedDomains.TryGetValue(domain.DomainKey, out var updatedDomain)
                ? updatedDomain
                : domain)
            .ToArray();

        var (hasErrors, lastError) = ComputeErrors(domains);

        return runResult with
        {
            Domains = domains,
            HasErrors = hasErrors,
            LastError = lastError
        };
    }

    private static ProjectBootstrapRunResult RecalculateRunResult(ProjectBootstrapRunResult runResult)
    {
        var (hasErrors, lastError) = ComputeErrors(runResult.Domains);
        if (hasErrors == runResult.HasErrors && string.Equals(lastError, runResult.LastError, StringComparison.Ordinal))
        {
            return runResult;
        }

        return runResult with { HasErrors = hasErrors, LastError = lastError };
    }

    private static (bool HasErrors, string? LastError) ComputeErrors(IReadOnlyList<ProjectBootstrapDomainResult> results)
    {
        var anySuccess = results.Any(IsDomainSuccess);
        var hardFailures = results.Any(IsHardFailure);
        var hasErrors = hardFailures || !anySuccess;
        var lastError = hasErrors ? BuildLastError(results, anySuccess, hardFailures) : null;
        return (hasErrors, lastError);
    }

    private static bool IsDomainSuccess(ProjectBootstrapDomainResult result)
    {
        return result.ProjectContainerProvisioning?.Success == true;
    }

    private static bool IsHardFailure(ProjectBootstrapDomainResult result)
    {
        if (result.RootState.StartsWith("blocked_", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (result.RootState.StartsWith("cleanup_", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (result.RootState.EndsWith("_failed", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (result.DomainRootProvisioning?.Errors.Count > 0)
        {
            return true;
        }

        if (result.ProjectContainerProvisioning?.Errors.Count > 0)
        {
            return true;
        }

        return false;
    }

    private static string? BuildLastError(
        IReadOnlyList<ProjectBootstrapDomainResult> results,
        bool anySuccess,
        bool hardFailures)
    {
        if (!anySuccess)
        {
            return "No domain provisioning succeeded.";
        }

        if (!hardFailures)
        {
            return null;
        }

        foreach (var result in results)
        {
            if (result.DomainRootProvisioning?.Errors.Count > 0)
            {
                return result.DomainRootProvisioning.Errors[0];
            }

            if (result.ProjectContainerProvisioning?.Errors.Count > 0)
            {
                return result.ProjectContainerProvisioning.Errors[0];
            }

            if (result.Notes.Count > 0 && IsErrorRootState(result.RootState))
            {
                return result.Notes[0];
            }
        }

        return "project.bootstrap completed with provisioning errors.";
    }

    private static bool IsErrorRootState(string rootState)
    {
        return rootState.StartsWith("blocked_", StringComparison.OrdinalIgnoreCase)
            || rootState.EndsWith("_failed", StringComparison.OrdinalIgnoreCase)
            || rootState.StartsWith("cleanup_", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryValidateStart(
        string statusKey,
        bool force,
        out string? error,
        out bool alreadyProvisioning)
    {
        alreadyProvisioning = false;
        error = null;

        if (force)
        {
            return true;
        }

        if (string.Equals(statusKey, StatusProvisioning, StringComparison.OrdinalIgnoreCase))
        {
            alreadyProvisioning = true;
            error = "Project is already provisioning.";
            return false;
        }

        if (!string.Equals(statusKey, StatusReady, StringComparison.OrdinalIgnoreCase))
        {
            error = $"Project status '{statusKey}' is not ready_to_provision.";
            return false;
        }

        return true;
    }
}
