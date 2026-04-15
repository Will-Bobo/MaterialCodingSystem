namespace MaterialCodingSystem.Application.Interfaces;

public interface IBackupRepository
{
    Task VacuumIntoAsync(string targetPath, CancellationToken ct = default);
    Task<List<string>> ListBackupFilesAsync(CancellationToken ct = default);
    Task DeleteFileAsync(string path, CancellationToken ct = default);
}

