namespace MaterialCodingSystem.Application.Interfaces;

public interface IFileSystemBomArchiveStorage
{
    /// <summary>
    /// Copy source file to finalPath with "temp + atomic rename" semantics.
    /// Must NOT overwrite existing finalPath.
    /// Must cleanup temp file on failure.
    /// </summary>
    Task CopyToArchiveAsync(string sourceFilePath, string finalPath, CancellationToken ct = default);

    Task DeleteIfExistsAsync(string path, CancellationToken ct = default);
}

