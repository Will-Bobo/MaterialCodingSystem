using MaterialCodingSystem.Application.Contracts;
using MaterialCodingSystem.Application.Interfaces;

namespace MaterialCodingSystem.Application;

public sealed record ConfigureBomArchiveRootPathResponse(string RootPath);

public sealed class ConfigureBomArchiveRootPathUseCase
{
    private readonly IBomArchivePreferenceStore _prefs;
    private readonly IBomArchiveInteraction _ui;

    public ConfigureBomArchiveRootPathUseCase(IBomArchivePreferenceStore prefs, IBomArchiveInteraction ui)
    {
        _prefs = prefs;
        _ui = ui;
    }

    public Task<Result<ConfigureBomArchiveRootPathResponse>> ExecuteAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var initial = _prefs.GetBomArchiveRootPath();
        var picked = _ui.PickArchiveRootFolder(initial);
        if (string.IsNullOrWhiteSpace(picked))
            return Task.FromResult(Result<ConfigureBomArchiveRootPathResponse>.Fail(ErrorCodes.VALIDATION_ERROR, "已取消选择归档目录。"));

        _prefs.SetBomArchiveRootPath(picked);
        return Task.FromResult(Result<ConfigureBomArchiveRootPathResponse>.Ok(new ConfigureBomArchiveRootPathResponse(picked)));
    }
}

