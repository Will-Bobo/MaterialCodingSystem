using MaterialCodingSystem.Application.Contracts;

namespace MaterialCodingSystem.Application;

public sealed record ArchiveBomRequest(string FilePath);

public sealed record ArchiveBomResponse(string FilePath);

public sealed class ArchiveBomUseCase
{
    private readonly CanArchiveBomUseCase _canArchive;
    private readonly BomArchiveService _archive;

    public ArchiveBomUseCase(CanArchiveBomUseCase canArchive, BomArchiveService archive)
    {
        _canArchive = canArchive;
        _archive = archive;
    }

    public async Task<Result<ArchiveBomResponse>> ExecuteAsync(ArchiveBomRequest req, CancellationToken ct = default)
    {
        var can = await _canArchive.ExecuteAsync(new CanArchiveBomRequest(req.FilePath), ct);
        if (!can.IsSuccess || can.Data is null)
            return Result<ArchiveBomResponse>.Fail(can.Error!.Code, can.Error.Message);

        if (!can.Data.IsAllowed)
            return Result<ArchiveBomResponse>.Fail(ErrorCodes.VALIDATION_ERROR, can.Data.Reason);

        var archived = await _archive.ArchiveAsync(req.FilePath, can.Data.FinishedCode, can.Data.Version, ct);
        if (!archived.IsSuccess || string.IsNullOrWhiteSpace(archived.Data))
            return Result<ArchiveBomResponse>.Fail(archived.Error!.Code, archived.Error.Message);

        return Result<ArchiveBomResponse>.Ok(new ArchiveBomResponse(archived.Data));
    }
}

