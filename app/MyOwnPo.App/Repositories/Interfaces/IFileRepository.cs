namespace MyOwnPo.Repositories.Interfaces;

public interface IFileRepository
{
    Task<string?> LoadFile(string fileName, CancellationToken cancellationToken = default);
    Task SaveFile(string fileName, string content, CancellationToken cancellationToken = default);
    Task DeleteFile(string fileName, CancellationToken cancellationToken = default);
}