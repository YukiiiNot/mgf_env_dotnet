namespace MGF.Contracts.Abstractions.ProjectArchive;

public interface IProjectArchiveData
{
    Task<ProjectArchivePathTemplates> GetArchivePathTemplatesAsync(CancellationToken cancellationToken = default);
}
