using System.IO;
using System.Security.Cryptography;
using MaterialCodingSystem.Application.Contracts;
using MaterialCodingSystem.Application.Interfaces;

namespace MaterialCodingSystem.Application;

public sealed record ValidateBomArchiveIntegrityRequest(
    string FinishedCode,
    string Version,
    string SourceFilePath
);

public sealed record ValidateBomArchiveIntegrityResponse(
    bool IsOk,
    string Reason,
    string ArchivedFilePath
);

/// <summary>
/// 只读校验：DB record + file_path 存在 + hash 与源文件一致。
/// </summary>
public sealed class ValidateBomArchiveIntegrityUseCase
{
    private readonly IBomArchiveRepository _repo;

    public ValidateBomArchiveIntegrityUseCase(IBomArchiveRepository repo)
    {
        _repo = repo;
    }

    public async Task<Result<ValidateBomArchiveIntegrityResponse>> ExecuteAsync(
        ValidateBomArchiveIntegrityRequest req,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.FinishedCode) || string.IsNullOrWhiteSpace(req.Version))
            return Result<ValidateBomArchiveIntegrityResponse>.Fail(ErrorCodes.VALIDATION_ERROR, "finished_code/version required.");
        if (string.IsNullOrWhiteSpace(req.SourceFilePath))
            return Result<ValidateBomArchiveIntegrityResponse>.Fail(ErrorCodes.VALIDATION_ERROR, "source_file_path required.");

        var record = await _repo.GetAsync(req.FinishedCode, req.Version, ct);
        if (record is null)
            return Result<ValidateBomArchiveIntegrityResponse>.Fail(ErrorCodes.NOT_FOUND, "archive record not found.");

        if (!File.Exists(record.FilePath))
        {
            return Result<ValidateBomArchiveIntegrityResponse>.Ok(new ValidateBomArchiveIntegrityResponse(
                IsOk: false,
                Reason: "归档文件不存在。",
                ArchivedFilePath: record.FilePath
            ));
        }

        if (!File.Exists(req.SourceFilePath))
        {
            return Result<ValidateBomArchiveIntegrityResponse>.Ok(new ValidateBomArchiveIntegrityResponse(
                IsOk: false,
                Reason: "源文件不存在，无法校验Hash一致性。",
                ArchivedFilePath: record.FilePath
            ));
        }

        var srcHash = await Sha256Async(req.SourceFilePath, ct);
        var dstHash = await Sha256Async(record.FilePath, ct);
        if (!string.Equals(srcHash, dstHash, StringComparison.OrdinalIgnoreCase))
        {
            return Result<ValidateBomArchiveIntegrityResponse>.Ok(new ValidateBomArchiveIntegrityResponse(
                IsOk: false,
                Reason: "归档文件Hash与源文件不一致。",
                ArchivedFilePath: record.FilePath
            ));
        }

        return Result<ValidateBomArchiveIntegrityResponse>.Ok(new ValidateBomArchiveIntegrityResponse(
            IsOk: true,
            Reason: "",
            ArchivedFilePath: record.FilePath
        ));
    }

    private static async Task<string> Sha256Async(string path, CancellationToken ct)
    {
        await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(fs, ct);
        return Convert.ToHexString(hash);
    }
}

