namespace MGF.UseCases.Operations.RootIntegrity.RunRootIntegrity;

using MGF.Contracts.Abstractions.RootIntegrity;
using MGF.Contracts.Abstractions.ProjectWorkflows;

public sealed class RunRootIntegrityUseCase : IRunRootIntegrityUseCase
{
    private readonly IRootIntegrityStore store;
    private readonly IRootIntegrityExecutor executor;
    private readonly IProjectWorkflowLock workflowLock;

    public RunRootIntegrityUseCase(
        IRootIntegrityStore store,
        IRootIntegrityExecutor executor,
        IProjectWorkflowLock workflowLock)
    {
        this.store = store;
        this.executor = executor;
        this.workflowLock = workflowLock;
    }

    public async Task<RunRootIntegrityResult> ExecuteAsync(
        RunRootIntegrityRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Payload is null)
        {
            throw new InvalidOperationException("RootIntegrity payload is required.");
        }

        var startedAt = DateTimeOffset.UtcNow;
        var payload = request.Payload;

        if (!IsReportMode(payload.Mode) && !IsRepairMode(payload.Mode))
        {
            var errors = new List<string>
            {
                $"Invalid mode '{payload.Mode}'. Expected 'report' or 'repair'."
            };
            var result = BuildResult(payload, string.Empty, startedAt, errors);
            return new RunRootIntegrityResult(result);
        }

        var contract = await store.GetContractAsync(payload.ProviderKey, payload.RootKey, cancellationToken);
        if (contract is null)
        {
            var errors = new List<string>
            {
                $"No storage_root_contracts entry for provider_key={payload.ProviderKey} root_key={payload.RootKey}."
            };
            var result = BuildResult(payload, string.Empty, startedAt, errors);
            return new RunRootIntegrityResult(result);
        }

        var lockScopeId = StorageMutationScopes.ForRoot(payload.ProviderKey, payload.RootKey);
        await using var lockLease = await workflowLock.TryAcquireAsync(
            lockScopeId,
            ProjectWorkflowKinds.StorageMutation,
            request.JobId,
            cancellationToken);
        if (lockLease is null)
        {
            throw new ProjectWorkflowLockUnavailableException(lockScopeId, ProjectWorkflowKinds.StorageMutation);
        }

        var runResult = await executor.ExecuteAsync(payload, contract, request.JobId, cancellationToken);
        return new RunRootIntegrityResult(runResult);
    }

    private static RootIntegrityResult BuildResult(
        RootIntegrityPayload payload,
        string rootPath,
        DateTimeOffset startedAt,
        List<string> errors)
    {
        var finishedAt = DateTimeOffset.UtcNow;

        return new RootIntegrityResult(
            ProviderKey: payload.ProviderKey,
            RootKey: payload.RootKey,
            RootPath: rootPath,
            Mode: payload.Mode,
            DryRun: payload.DryRun,
            StartedAt: startedAt,
            FinishedAt: finishedAt,
            MissingRequired: Array.Empty<string>(),
            MissingOptional: Array.Empty<string>(),
            UnknownEntries: Array.Empty<RootIntegrityEntry>(),
            RootFiles: Array.Empty<RootIntegrityEntry>(),
            QuarantinePlan: Array.Empty<RootIntegrityMovePlan>(),
            GuardrailBlocks: Array.Empty<RootIntegrityMovePlan>(),
            Actions: Array.Empty<RootIntegrityAction>(),
            Warnings: Array.Empty<string>(),
            Errors: errors
        );
    }

    private static bool IsRepairMode(string mode)
    {
        return string.Equals(mode, "repair", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsReportMode(string mode)
    {
        return string.Equals(mode, "report", StringComparison.OrdinalIgnoreCase);
    }
}
