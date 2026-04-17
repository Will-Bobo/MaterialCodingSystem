using System.IO;
using MaterialCodingSystem.Application.Contracts;
using MaterialCodingSystem.Application.Interfaces;

namespace MaterialCodingSystem.Application;

public sealed record ArchiveBomRequest(string FilePath);

public sealed record ArchiveBomResponse(string FilePath);

public sealed class ArchiveBomUseCase
{
    private readonly CanArchiveBomUseCase _canArchive;
    private readonly BomArchiveService _archive;
    private readonly IBomArchiveRepository _repo;
    private readonly IBomArchivePreferenceStore _prefs;
    private readonly IBomArchiveInteraction _ui;

    public ArchiveBomUseCase(
        CanArchiveBomUseCase canArchive,
        BomArchiveService archive,
        IBomArchiveRepository repo,
        IBomArchivePreferenceStore prefs,
        IBomArchiveInteraction ui)
    {
        _canArchive = canArchive;
        _archive = archive;
        _repo = repo;
        _prefs = prefs;
        _ui = ui;
    }

    public async Task<Result<ArchiveBomResponse>> ExecuteAsync(ArchiveBomRequest req, CancellationToken ct = default)
    {
        var can = await _canArchive.ExecuteAsync(new CanArchiveBomRequest(req.FilePath), ct);
        if (!can.IsSuccess || can.Data is null)
            return Result<ArchiveBomResponse>.Fail(can.Error!.Code, can.Error.Message);

        if (!can.Data.IsAllowed)
            return Result<ArchiveBomResponse>.Fail(ErrorCodes.VALIDATION_ERROR, can.Data.Reason);

        var root = _prefs.GetBomArchiveRootPath();
        if (string.IsNullOrWhiteSpace(root))
        {
            var picked = _ui.PickArchiveRootFolder(null);
            if (string.IsNullOrWhiteSpace(picked))
                return Result<ArchiveBomResponse>.Fail(ErrorCodes.VALIDATION_ERROR, "已取消选择归档目录。");
            _prefs.SetBomArchiveRootPath(picked);
            root = picked;
        }

        var existing = await _repo.GetAsync(can.Data.FinishedCode, can.Data.Version, ct);
        var overwrite = false;
        if (existing is not null)
        {
            var ok = _ui.ConfirmOverwrite(new BomArchiveOverwritePrompt(
                FinishedCode: existing.FinishedCode,
                Version: existing.Version,
                ExistingFilePath: existing.FilePath,
                ExistingCreatedAt: existing.CreatedAt));
            if (!ok)
                return Result<ArchiveBomResponse>.Fail(ErrorCodes.VALIDATION_ERROR, "已取消覆盖保存。");
            overwrite = true;
        }

        var archived = await _archive.ArchiveAsync(
            req.FilePath,
            can.Data.FinishedCode,
            can.Data.Version,
            archiveRootPath: root,
            overwriteIfExists: overwrite,
            ct);
        if (!archived.IsSuccess || string.IsNullOrWhiteSpace(archived.Data))
            return Result<ArchiveBomResponse>.Fail(archived.Error!.Code, archived.Error.Message);

        var open = _ui.ShowSavedAndAskOpenFolder(new BomArchiveSavedPrompt(
            FinishedCode: can.Data.FinishedCode,
            Version: can.Data.Version,
            SavedFilePath: archived.Data));
        if (open)
        {
            var folder = Path.GetDirectoryName(archived.Data);
            if (!string.IsNullOrWhiteSpace(folder))
                _ui.OpenFolder(folder);
        }

        return Result<ArchiveBomResponse>.Ok(new ArchiveBomResponse(archived.Data));
    }
}

