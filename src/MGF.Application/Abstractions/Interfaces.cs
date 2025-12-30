using MGF.Domain.Entities;

namespace MGF.Application.Abstractions;

public interface IFileStore
{
    Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default);
    Task CreateDirectoryAsync(string path, CancellationToken cancellationToken = default);
    Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default);
    Task WriteAllBytesAsync(string path, byte[] content, CancellationToken cancellationToken = default);
    Task MoveAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default);
    Task CopyAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default);
}

public interface IProjectRepository
{
    Task<Project?> GetByIdAsync(string projectId, CancellationToken cancellationToken = default);
    Task SaveAsync(Project project, CancellationToken cancellationToken = default);
}

public interface IClientRepository
{
    Task<Client?> GetByIdAsync(string clientId, CancellationToken cancellationToken = default);
    Task SaveAsync(Client client, CancellationToken cancellationToken = default);
}

public interface IPersonRepository
{
    Task<Person?> GetByIdAsync(string personId, CancellationToken cancellationToken = default);
    Task SaveAsync(Person person, CancellationToken cancellationToken = default);
}

public interface IProjectService
{
    Task<Project> CreateProjectAsync(string clientId, string projectName, CancellationToken cancellationToken = default);
}
