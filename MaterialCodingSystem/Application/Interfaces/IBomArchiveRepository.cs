using MaterialCodingSystem.Application.Contracts;

namespace MaterialCodingSystem.Application.Interfaces;

public sealed record BomArchiveRecord(
    long Id,
    string FinishedCode,
    string Version,
    string FilePath,
    string CreatedAt
);

public interface IBomArchiveRepository
{
    Task<bool> ExistsAsync(string finishedCode, string version, CancellationToken ct = default);

    Task<BomArchiveRecord?> GetAsync(string finishedCode, string version, CancellationToken ct = default);

    Task InsertAsync(string finishedCode, string version, string filePath, CancellationToken ct = default);

    Task<IReadOnlyList<BomArchiveRecord>> ListAsync(string? finishedCode = null, CancellationToken ct = default);
}

