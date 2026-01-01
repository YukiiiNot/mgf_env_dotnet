namespace MGF.UseCases.ProjectBootstrap.BootstrapProject;

public interface IBootstrapProjectUseCase
{
    Task<BootstrapProjectResult> ExecuteAsync(
        BootstrapProjectRequest request,
        CancellationToken cancellationToken = default);
}
