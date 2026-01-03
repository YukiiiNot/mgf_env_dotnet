namespace MGF.UseCases.Operations.ProjectArchive.RunProjectArchive;

public interface IRunProjectArchiveUseCase
{
    Task<RunProjectArchiveResult> ExecuteAsync(
        RunProjectArchiveRequest request,
        CancellationToken cancellationToken = default);
}
