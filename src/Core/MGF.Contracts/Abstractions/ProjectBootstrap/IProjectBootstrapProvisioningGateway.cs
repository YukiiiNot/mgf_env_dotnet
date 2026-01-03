namespace MGF.Contracts.Abstractions.ProjectBootstrap;

public interface IProjectBootstrapProvisioningGateway
{
    Task<ProjectBootstrapExecutionResult> ExecuteAsync(
        ProjectBootstrapContext context,
        BootstrapProjectRequest request,
        CancellationToken cancellationToken = default);

    ProjectBootstrapRunResult BuildBlockedNonRealResult(
        ProjectBootstrapContext context,
        BootstrapProjectRequest request);

    ProjectBootstrapRunResult BuildBlockedStatusResult(
        ProjectBootstrapContext context,
        BootstrapProjectRequest request,
        string? statusError,
        bool alreadyProvisioning);
}
