using System.IO;
using MaterialCodingSystem.Application.Contracts;
using MaterialCodingSystem.Application.Interfaces;
using MaterialCodingSystem.Application.Logging;
using Microsoft.Extensions.Logging;

namespace MaterialCodingSystem.Application;

public sealed class BomArchiveService
{
    private readonly IBomArchiveRepository _repo;
    private readonly IFileSystemBomArchiveStorage _storage;
    private readonly IAppExecutionDirectoryProvider _dirs;
    private readonly ILogger<BomArchiveService> _logger;

    public BomArchiveService(
        IBomArchiveRepository repo,
        IFileSystemBomArchiveStorage storage,
        IAppExecutionDirectoryProvider dirs,
        ILogger<BomArchiveService>? logger = null)
    {
        _repo = repo;
        _storage = storage;
        _dirs = dirs;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<BomArchiveService>.Instance;
    }

    /// <summary>
    /// 执行归档动作（纯“动作层”服务）。是否允许归档由 Application 的 CanArchiveBomUseCase 统一判定；
    /// Presentation 层不得直接调用本服务。
    /// </summary>
    public Task<Result<string>> ArchiveAsync(
        string sourceFilePath,
        string finishedCode,
        string version,
        string? archiveRootPath,
        bool overwriteIfExists,
        CancellationToken ct = default)
        => McsLoggingExtensions.RunUseCaseAsync(_logger, McsActions.BomArchiveService, $"{finishedCode}|{version}", ct,
            async () =>
            {
        if (string.IsNullOrWhiteSpace(sourceFilePath))
            return Result<string>.Fail(ErrorCodes.VALIDATION_ERROR, "未提供源文件路径。");
        if (string.IsNullOrWhiteSpace(finishedCode))
            return Result<string>.Fail(ErrorCodes.VALIDATION_ERROR, "未提供成品编码。");
        if (string.IsNullOrWhiteSpace(version))
            return Result<string>.Fail(ErrorCodes.VALIDATION_ERROR, "未提供版本号。");

        // 防御：即使前置已判定，也要兜底（DB UNIQUE 仍为最终仲裁）
        if (!overwriteIfExists && await _repo.ExistsAsync(finishedCode, version, ct))
            return Result<string>.Fail(ErrorCodes.BOM_ARCHIVE_VERSION_EXISTS, "该版本已存在。");

        var sanitizedVersion = SanitizeFileName(version);
        var ext = Path.GetExtension(sourceFilePath);
        if (string.IsNullOrWhiteSpace(ext)) ext = ".xls";

        string finalPath;
        try
        {
            var root = string.IsNullOrWhiteSpace(archiveRootPath) ? _dirs.GetExecutionDirectory() : archiveRootPath.Trim();
            finalPath = Path.Combine(root, "BOM", SanitizeDirName(finishedCode), sanitizedVersion + ext);
        }
        catch (Exception)
        {
            return Result<string>.Fail(ErrorCodes.BOM_ARCHIVE_PATH_INVALID, "归档路径无效。");
        }

        try
        {
            if (overwriteIfExists)
                await _storage.CopyToArchiveOverwriteAsync(sourceFilePath, finalPath, ct);
            else
                await _storage.CopyToArchiveAsync(sourceFilePath, finalPath, ct);
        }
        catch (FileNotFoundException)
        {
            return Result<string>.Fail(ErrorCodes.NOT_FOUND, "源文件不存在。");
        }
        catch (IOException ex) when (IsFileInUse(ex))
        {
            return Result<string>.Fail(ErrorCodes.BOM_FILE_LOCKED, "文件正在使用中，请关闭后重试。");
        }
        catch (UnauthorizedAccessException)
        {
            return Result<string>.Fail(ErrorCodes.BOM_ARCHIVE_WRITE_FAILED, "归档写入失败。");
        }
        catch (IOException)
        {
            return Result<string>.Fail(ErrorCodes.INTERNAL_ERROR, "归档读写失败。");
        }

        if (!overwriteIfExists)
        {
            try
            {
                await _repo.InsertAsync(finishedCode, version, finalPath, ct);
            }
            catch (Exception ex) when (IsUniqueViolation(ex))
            {
                // if DB says duplicate, cleanup file to avoid orphan
                await _storage.DeleteIfExistsAsync(finalPath, ct);
                return Result<string>.Fail(ErrorCodes.BOM_ARCHIVE_VERSION_EXISTS, "该版本已存在。");
            }
            catch (Exception)
            {
                // unknown DB failure: cleanup file to avoid "file exists but DB missing"
                await _storage.DeleteIfExistsAsync(finalPath, ct);
                return Result<string>.Fail(ErrorCodes.INTERNAL_ERROR, "归档记录写入失败。");
            }
        }
        else
        {
            try
            {
                await _repo.UpdateAsync(finishedCode, version, finalPath, ct);
            }
            catch (Exception)
            {
                return Result<string>.Fail(ErrorCodes.INTERNAL_ERROR, "归档记录更新失败。");
            }
        }

        return Result<string>.Ok(finalPath);
    },
            static r => r.IsSuccess && r.Data is not null ? ("saved_name", McsLog.FileNameForLog(r.Data) ?? "") : null,
            static c => c is ErrorCodes.BOM_ARCHIVE_VERSION_EXISTS or ErrorCodes.BOM_FILE_LOCKED);

    public static string SanitizeFileName(string raw)
    {
        if (raw is null) return "";
        var invalid = new HashSet<char>(Path.GetInvalidFileNameChars());
        var chars = raw.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        return new string(chars);
    }

    private static string SanitizeDirName(string raw)
    {
        if (raw is null) return "";
        var invalid = new HashSet<char>(Path.GetInvalidPathChars().Concat(Path.GetInvalidFileNameChars()));
        var chars = raw.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        return new string(chars);
    }

    private static bool IsFileInUse(IOException ex)
    {
        const int sharingViolationHResult = unchecked((int)0x80070020);
        if (ex.HResult == sharingViolationHResult) return true;
        return ex.Message.Contains("another process", StringComparison.OrdinalIgnoreCase)
               || ex.Message.Contains("另一个程序", StringComparison.OrdinalIgnoreCase)
               || ex.Message.Contains("正由另一进程", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUniqueViolation(Exception ex)
        => ex.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)
           && ex.Message.Contains("bom_archive", StringComparison.OrdinalIgnoreCase);
}

