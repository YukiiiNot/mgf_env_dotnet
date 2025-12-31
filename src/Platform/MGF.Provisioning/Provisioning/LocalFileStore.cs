using MGF.Contracts.Abstractions;

namespace MGF.Provisioning;

public sealed class LocalFileStore : IFileStore
{
    public Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default)
    {
        var exists = File.Exists(path) || Directory.Exists(path);
        return Task.FromResult(exists);
    }

    public Task CreateDirectoryAsync(string path, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(path);
        return Task.CompletedTask;
    }

    public Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default)
    {
        return File.ReadAllBytesAsync(path, cancellationToken);
    }

    public Task WriteAllBytesAsync(string path, byte[] content, CancellationToken cancellationToken = default)
    {
        return File.WriteAllBytesAsync(path, content, cancellationToken);
    }

    public Task MoveAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default)
    {
        if (File.Exists(sourcePath))
        {
            File.Move(sourcePath, destinationPath, overwrite: true);
            return Task.CompletedTask;
        }

        if (Directory.Exists(sourcePath))
        {
            Directory.Move(sourcePath, destinationPath);
            return Task.CompletedTask;
        }

        throw new FileNotFoundException("Source path not found.", sourcePath);
    }

    public Task CopyAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("Source file not found.", sourcePath);
        }

        File.Copy(sourcePath, destinationPath, overwrite: true);
        return Task.CompletedTask;
    }
}


