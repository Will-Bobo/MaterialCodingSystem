using MaterialCodingSystem.Application.Contracts;
using MaterialCodingSystem.Application.Interfaces;

namespace MaterialCodingSystem.Application;

public sealed record CanArchiveBomRequest(string FilePath);

public sealed record CanArchiveBomResponse(
    bool IsAllowed,
    string Reason,
    string FinishedCode,
    string Version
);

/// <summary>
/// V1.4：归档权限判断唯一入口。UI 不得自行根据 NEW/ERROR 判断是否可归档。
/// </summary>
public sealed class CanArchiveBomUseCase
{
    private readonly AnalyzeBomUseCase _analyze;
    private readonly IBomArchiveRepository _archiveRepo;

    public CanArchiveBomUseCase(AnalyzeBomUseCase analyze, IBomArchiveRepository archiveRepo)
    {
        _analyze = analyze;
        _archiveRepo = archiveRepo;
    }

    public async Task<Result<CanArchiveBomResponse>> ExecuteAsync(CanArchiveBomRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.FilePath))
            return Result<CanArchiveBomResponse>.Fail(ErrorCodes.VALIDATION_ERROR, "file_path is required.");

        var analyzed = await _analyze.ExecuteAsync(new AnalyzeBomRequest(req.FilePath), ct);
        if (!analyzed.IsSuccess || analyzed.Data is null)
            return Result<CanArchiveBomResponse>.Fail(analyzed.Error!.Code, analyzed.Error.Message);

        var a = analyzed.Data;

        if (a.NewCount != 0)
        {
            return Result<CanArchiveBomResponse>.Ok(new CanArchiveBomResponse(
                IsAllowed: false,
                Reason: "存在 NEW 物料，禁止归档。",
                FinishedCode: a.FinishedCode,
                Version: a.Version
            ));
        }

        if (a.ErrorCount != 0)
        {
            return Result<CanArchiveBomResponse>.Ok(new CanArchiveBomResponse(
                IsAllowed: false,
                Reason: "存在 ERROR 物料，禁止归档。",
                FinishedCode: a.FinishedCode,
                Version: a.Version
            ));
        }

        return Result<CanArchiveBomResponse>.Ok(new CanArchiveBomResponse(
            IsAllowed: true,
            Reason: "",
            FinishedCode: a.FinishedCode,
            Version: a.Version
        ));
    }
}

