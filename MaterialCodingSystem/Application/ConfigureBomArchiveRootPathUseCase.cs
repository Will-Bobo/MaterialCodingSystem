using MaterialCodingSystem.Application.Contracts;
using MaterialCodingSystem.Application.Interfaces;
using MaterialCodingSystem.Application.Logging;
using Microsoft.Extensions.Logging;

namespace MaterialCodingSystem.Application;

public sealed record ConfigureBomArchiveRootPathResponse(string RootPath);

public sealed class ConfigureBomArchiveRootPathUseCase
{
    private readonly IBomArchivePreferenceStore _prefs;
    private readonly IBomArchiveInteraction _ui;
    private readonly ILogger<ConfigureBomArchiveRootPathUseCase> _logger;

    public ConfigureBomArchiveRootPathUseCase(
        IBomArchivePreferenceStore prefs,
        IBomArchiveInteraction ui,
        ILogger<ConfigureBomArchiveRootPathUseCase>? logger = null)
    {
        _prefs = prefs;
        _ui = ui;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ConfigureBomArchiveRootPathUseCase>.Instance;
    }

    public Task<Result<ConfigureBomArchiveRootPathResponse>> ExecuteAsync(CancellationToken ct = default)
        => McsLoggingExtensions.RunUseCaseAsync(_logger, McsActions.BomConfigureArchiveRoot, null, ct,
            async () =>
            {
                ct.ThrowIfCancellationRequested();
                var initial = _prefs.GetBomArchiveRootPath();
                var picked = _ui.PickArchiveRootFolder(initial);
                if (string.IsNullOrWhiteSpace(picked))
                    return Result<ConfigureBomArchiveRootPathResponse>.Fail(ErrorCodes.VALIDATION_ERROR, "已取消选择归档目录。");

                _prefs.SetBomArchiveRootPath(picked);
                return Result<ConfigureBomArchiveRootPathResponse>.Ok(new ConfigureBomArchiveRootPathResponse(picked));
            },
            static r => r.IsSuccess ? ("configured", true) : null);
}

