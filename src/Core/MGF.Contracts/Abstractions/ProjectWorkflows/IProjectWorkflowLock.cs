namespace MGF.Contracts.Abstractions.ProjectWorkflows;

public interface IProjectWorkflowLock
{
    Task<IProjectWorkflowLease?> TryAcquireAsync(
        string projectId,
        string workflowKind,
        string holderId,
        CancellationToken cancellationToken = default);
}

public interface IProjectWorkflowLease : IAsyncDisposable
{
    string ProjectId { get; }
    string WorkflowKind { get; }
    string HolderId { get; }
}

public sealed class ProjectWorkflowLockUnavailableException : InvalidOperationException
{
    public ProjectWorkflowLockUnavailableException(string projectId, string workflowKind)
        : base($"Workflow lock busy for project '{projectId}' (workflow='{workflowKind}').")
    {
        ProjectId = projectId;
        WorkflowKind = workflowKind;
    }

    public string ProjectId { get; }
    public string WorkflowKind { get; }
}
