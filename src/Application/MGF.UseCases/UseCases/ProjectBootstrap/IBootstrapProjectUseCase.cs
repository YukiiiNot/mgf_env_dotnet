namespace MGF.UseCases.ProjectBootstrap;

public interface IBootstrapProjectUseCase
{
    Task<BootstrapProjectResult> ExecuteAsync(
        BootstrapProjectRequest request,
        CancellationToken cancellationToken = default);
}
