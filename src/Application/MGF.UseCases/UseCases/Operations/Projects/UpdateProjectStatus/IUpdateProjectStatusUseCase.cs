namespace MGF.UseCases.Operations.Projects.UpdateProjectStatus;

public interface IUpdateProjectStatusUseCase
{
    Task<UpdateProjectStatusResult> ExecuteAsync(
        UpdateProjectStatusRequest request,
        CancellationToken cancellationToken = default);
}
