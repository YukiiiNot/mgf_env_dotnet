namespace MGF.UseCases.Operations.ProjectBootstrap.BootstrapProject;

using MGF.Contracts.Abstractions.ProjectBootstrap;

public interface IBootstrapProjectUseCase
{
    Task<BootstrapProjectResult> ExecuteAsync(
        BootstrapProjectRequest request,
        CancellationToken cancellationToken = default);
}
