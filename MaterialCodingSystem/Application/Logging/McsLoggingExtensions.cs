using System.Diagnostics;
using MaterialCodingSystem.Application.Contracts;
using MaterialCodingSystem.Application;
using Microsoft.Extensions.Logging;

namespace MaterialCodingSystem.Application.Logging;

public static class McsLoggingExtensions
{
    public const int QuerySlowMs = 100;

    /// <summary>检索 + 纯只读列表/解析类查询：Start/Success 降噪。</summary>
    private static readonly HashSet<string> QuietReadActions = new(StringComparer.Ordinal)
    {
        McsActions.MaterialListCategories,
        McsActions.MaterialResolveGroupIdByItemCode,
        McsActions.MaterialGetGroupInfo,
        McsActions.MaterialSearchByCode,
        McsActions.MaterialSearchBySpec,
        McsActions.MaterialSearchCandidatesBySpecOnly,
        McsActions.MaterialSearchBySpecAll,
        McsActions.BomListArchive
    };

    private static string Ci => McsCorrelationContext.Current ?? "";

    private static LogLevel ResolveStartLevel(string action) =>
        QuietReadActions.Contains(action) ? LogLevel.Debug : LogLevel.Information;

    private static LogLevel ResolveSuccessLevel(string action, long durationMs)
    {
        if (!QuietReadActions.Contains(action))
            return LogLevel.Information;
        return durationMs < QuerySlowMs ? LogLevel.Debug : LogLevel.Information;
    }

    /// <summary>门禁争用：立即未拿到锁后，记录排队等待耗时。</summary>
    public static void LogMaintenanceGateBlocked(ILogger logger, string gateName, long durationMs)
    {
        logger.Log(LogLevel.Warning,
            "MCS pipeline={Pipeline} action={Action} state={State} gate_name={GateName} error_code={ErrorCode} duration_ms={DurationMs} correlation_id={CorrelationId}",
            "End", McsActions.SystemMaintenanceGate, "Blocked", gateName, ErrorCodes.MAINTENANCE_BUSY, durationMs, Ci);
    }

    public static void LogStart(ILogger logger, string action, string? primaryId = null)
    {
        var level = ResolveStartLevel(action);
        if (primaryId is null)
            logger.Log(level,
                "MCS pipeline={Pipeline} action={Action} state={State} correlation_id={CorrelationId}",
                "Start", action, "Start", Ci);
        else
            logger.Log(level,
                "MCS pipeline={Pipeline} action={Action} state={State} primary_id={PrimaryId} correlation_id={CorrelationId}",
                "Start", action, "Start", primaryId, Ci);
    }

    public static void LogSuccess(
        ILogger logger,
        string action,
        long durationMs,
        string? primaryId = null,
        string? extensionKey = null,
        object? extensionValue = null)
    {
        var level = ResolveSuccessLevel(action, durationMs);

        if (extensionKey is not null && extensionValue is not null)
        {
            if (primaryId is null)
                logger.Log(level,
                    "MCS pipeline={Pipeline} action={Action} state={State} duration_ms={DurationMs} ext_key={ExtKey} ext_value={ExtValue} correlation_id={CorrelationId}",
                    "End", action, "Success", durationMs, extensionKey, extensionValue, Ci);
            else
                logger.Log(level,
                    "MCS pipeline={Pipeline} action={Action} state={State} duration_ms={DurationMs} primary_id={PrimaryId} ext_key={ExtKey} ext_value={ExtValue} correlation_id={CorrelationId}",
                    "End", action, "Success", durationMs, primaryId, extensionKey, extensionValue, Ci);
        }
        else if (primaryId is null)
        {
            logger.Log(level,
                "MCS pipeline={Pipeline} action={Action} state={State} duration_ms={DurationMs} correlation_id={CorrelationId}",
                "End", action, "Success", durationMs, Ci);
        }
        else
        {
            logger.Log(level,
                "MCS pipeline={Pipeline} action={Action} state={State} duration_ms={DurationMs} primary_id={PrimaryId} correlation_id={CorrelationId}",
                "End", action, "Success", durationMs, primaryId, Ci);
        }
    }

    public static void LogBlocked(ILogger logger, string action, string errorCode, long durationMs, string? primaryId = null)
    {
        if (primaryId is null)
            logger.Log(LogLevel.Warning,
                "MCS pipeline={Pipeline} action={Action} state={State} duration_ms={DurationMs} error_code={ErrorCode} correlation_id={CorrelationId}",
                "End", action, "Blocked", durationMs, errorCode, Ci);
        else
            logger.Log(LogLevel.Warning,
                "MCS pipeline={Pipeline} action={Action} state={State} duration_ms={DurationMs} primary_id={PrimaryId} error_code={ErrorCode} correlation_id={CorrelationId}",
                "End", action, "Blocked", durationMs, primaryId, errorCode, Ci);
    }

    public static void LogFailed(ILogger logger, string action, string errorCode, long durationMs, string? primaryId = null)
    {
        if (primaryId is null)
            logger.Log(LogLevel.Error,
                "MCS pipeline={Pipeline} action={Action} state={State} duration_ms={DurationMs} error_code={ErrorCode} correlation_id={CorrelationId}",
                "End", action, "Failed", durationMs, errorCode, Ci);
        else
            logger.Log(LogLevel.Error,
                "MCS pipeline={Pipeline} action={Action} state={State} duration_ms={DurationMs} primary_id={PrimaryId} error_code={ErrorCode} correlation_id={CorrelationId}",
                "End", action, "Failed", durationMs, primaryId, errorCode, Ci);
    }

    /// <summary>Error Log（栈）。若异常链已标记记录则跳过。</summary>
    public static void LogException(ILogger logger, Exception ex, string action, string errorCode)
    {
        if (McsExceptionMarker.IsLogged(ex))
            return;

        logger.LogError(ex,
            "MCS ErrorLog action={Action} error_code={ErrorCode} correlation_id={CorrelationId}",
            action, errorCode, Ci);
        McsExceptionMarker.MarkLogged(ex);
    }

    public static Result<T> RunUseCaseSync<T>(
        ILogger logger,
        string action,
        string? primaryId,
        Func<Result<T>> body,
        Func<Result<T>, (string Key, object Value)?>? successExtension = null,
        Func<string, bool>? treatAsBlocked = null)
    {
        McsCorrelationContext.EnsureRootCorrelationId();
        LogStart(logger, action, primaryId);
        var sw = Stopwatch.StartNew();
        try
        {
            var result = body();
            sw.Stop();
            LogEnd(logger, action, primaryId, sw.ElapsedMilliseconds, result, successExtension, treatAsBlocked);
            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            LogFailed(logger, action, ErrorCodes.INTERNAL_ERROR, sw.ElapsedMilliseconds, primaryId);
            LogException(logger, ex, action, ErrorCodes.INTERNAL_ERROR);
            throw;
        }
    }

    public static async Task<Result<T>> RunUseCaseAsync<T>(
        ILogger logger,
        string action,
        string? primaryId,
        CancellationToken ct,
        Func<Task<Result<T>>> body,
        Func<Result<T>, (string Key, object Value)?>? successExtension = null,
        Func<string, bool>? treatAsBlocked = null)
    {
        McsCorrelationContext.EnsureRootCorrelationId();
        LogStart(logger, action, primaryId);
        var sw = Stopwatch.StartNew();
        try
        {
            var result = await body().ConfigureAwait(false);
            sw.Stop();
            LogEnd(logger, action, primaryId, sw.ElapsedMilliseconds, result, successExtension, treatAsBlocked);
            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            LogFailed(logger, action, ErrorCodes.INTERNAL_ERROR, sw.ElapsedMilliseconds, primaryId);
            LogException(logger, ex, action, ErrorCodes.INTERNAL_ERROR);
            throw;
        }
    }

    private static void LogEnd<T>(
        ILogger logger,
        string action,
        string? primaryId,
        long durationMs,
        Result<T> result,
        Func<Result<T>, (string Key, object Value)?>? successExtension,
        Func<string, bool>? treatAsBlocked)
    {
        if (result.IsSuccess)
        {
            var ext = successExtension?.Invoke(result);
            if (ext.HasValue)
                LogSuccess(logger, action, durationMs, primaryId, ext.Value.Key, ext.Value.Value);
            else
                LogSuccess(logger, action, durationMs, primaryId);
            return;
        }

        var code = result.Error!.Code;
        if (treatAsBlocked?.Invoke(code) == true)
            LogBlocked(logger, action, code, durationMs, primaryId);
        else
            LogFailed(logger, action, code, durationMs, primaryId);
    }
}
