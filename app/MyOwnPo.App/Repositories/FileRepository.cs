using MyOwnPo.Repositories.Interfaces;

namespace MyOwnPo.Repositories;

public class FileRepository : IFileRepository
{
    public async Task<string?> LoadFile(string fileName, CancellationToken cancellationToken = default)
    {
        return File.Exists(fileName)
            ? await File.ReadAllTextAsync(fileName, cancellationToken)
            : null;
    }

    public async Task SaveFile(string fileName, string content, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(fileName);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        await File.WriteAllTextAsync(fileName, content, cancellationToken);
    }

    public Task DeleteFile(string fileName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (File.Exists(fileName))
            File.Delete(fileName);

        return Task.CompletedTask;
    }
}