namespace MGF.Storage.ProjectBootstrap;

using Microsoft.Extensions.Configuration;
using MGF.Contracts.Abstractions.ProjectBootstrap;

public sealed class ProjectBootstrapProvisioningGateway : IProjectBootstrapProvisioningGateway
{
    private readonly IConfiguration configuration;

    public ProjectBootstrapProvisioningGateway(IConfiguration configuration)
    {
        this.configuration = configuration;
    }

    public Task<ProjectBootstrapExecutionResult> ExecuteAsync(
        ProjectBootstrapContext context,
        BootstrapProjectRequest request,
        CancellationToken cancellationToken = default)
    {
        var bootstrapper = new ProjectBootstrapper(configuration);
        return bootstrapper.RunAsync(context, request, cancellationToken);
    }

    public ProjectBootstrapRunResult BuildBlockedNonRealResult(
        ProjectBootstrapContext context,
        BootstrapProjectRequest request)
    {
        var bootstrapper = new ProjectBootstrapper(configuration);
        return bootstrapper.BuildBlockedNonRealResult(context, request);
    }

    public ProjectBootstrapRunResult BuildBlockedStatusResult(
        ProjectBootstrapContext context,
        BootstrapProjectRequest request,
        string? statusError,
        bool alreadyProvisioning)
    {
        var bootstrapper = new ProjectBootstrapper(configuration);
        return bootstrapper.BuildBlockedStatusResult(context, request, statusError, alreadyProvisioning);
    }
}
