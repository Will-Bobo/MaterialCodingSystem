using MaterialCodingSystem.Application.Contracts;
using MaterialCodingSystem.Application.Interfaces;
using MaterialCodingSystem.Domain.Services;
using MaterialCodingSystem.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace MaterialCodingSystem.Application;

/// <summary>
/// V1.4 BOM 审核（Phase1）：仅解析 + 审核输出，不做导入/归档/UI。
/// </summary>
public sealed class AnalyzeBomUseCase
{
    private readonly ParseBomUseCase _parseBom;
    private readonly IBomFileFormatDetector _formatDetector;
    private readonly IMaterialRepository _repo;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<AnalyzeBomUseCase> _logger;

    public AnalyzeBomUseCase(
        ParseBomUseCase parseBom,
        IBomFileFormatDetector formatDetector,
        IMaterialRepository repo,
        IUnitOfWork uow,
        ILogger<AnalyzeBomUseCase> logger)
    {
        _parseBom = parseBom;
        _formatDetector = formatDetector;
        _repo = repo;
        _uow = uow;
        _logger = logger;
    }

    public Task<Result<AnalyzeBomResponse>> ExecuteAsync(AnalyzeBomRequest req, CancellationToken ct = default)
        => _uow.ExecuteAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(req.FilePath))
                return Result<AnalyzeBomResponse>.Fail(ErrorCodes.VALIDATION_ERROR, "file_path is required.");

            var formatRes = _formatDetector.Detect(req.FilePath);
            if (!formatRes.IsSuccess)
                return Result<AnalyzeBomResponse>.Fail(formatRes.Error!.Code, formatRes.Error.Message);

            var detected = formatRes.Data;
            if (detected == BomExcelFileFormat.Unknown)
                return Result<AnalyzeBomResponse>.Fail(ErrorCodes.INVALID_EXCEL, "unable to detect excel format.");

            var parsedRes = _parseBom.Execute(req.FilePath);
            if (!parsedRes.IsSuccess || parsedRes.Data is null)
                return Result<AnalyzeBomResponse>.Fail(parsedRes.Error!.Code, parsedRes.Error.Message);

            var doc = parsedRes.Data;
            var finishedCode = doc.Header.FinishedCode;
            var version = doc.Header.Version;

            var outRows = new List<BomAuditRowDto>(doc.Rows.Count);
            var pass = 0;
            var @new = 0;
            var error = 0;
            var missingCode = 0;
            int? firstErrorRowNo = null;

            var codeCache = new Dictionary<string, MaterialItemCodeSpecSnapshot?>(StringComparer.OrdinalIgnoreCase);
            var specCache = new Dictionary<string, bool>(StringComparer.Ordinal); // key=categoryCode||spec

            foreach (var r in doc.Rows)
            {
                ct.ThrowIfCancellationRequested();

                var codeRaw = r.Code?.Trim() ?? "";
                var specRaw = r.Spec?.Trim() ?? "";

                if (string.IsNullOrWhiteSpace(codeRaw))
                {
                    var (s, reason, isMissing) = BomAuditRules.ErrorMissingCode();
                    if (isMissing) missingCode++;
                    error++;
                    firstErrorRowNo ??= r.ExcelRowNo;
                    outRows.Add(new BomAuditRowDto(r.ExcelRowNo, s, "", r.Name ?? "", specRaw, r.Description ?? "", r.Brand ?? "", reason));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(specRaw))
                {
                    var (s, reason, _) = BomAuditRules.ErrorMissingSpec();
                    error++;
                    firstErrorRowNo ??= r.ExcelRowNo;
                    outRows.Add(new BomAuditRowDto(r.ExcelRowNo, s, codeRaw, r.Name ?? "", "", r.Description ?? "", r.Brand ?? "", reason));
                    continue;
                }

                // V1.4：NEW 判定需要 spec 在启用态(status=1)数据集中不存在（废弃 spec 不占用）
                // category_code 从 code 前 3 位推导；不在本阶段做严格格式校验（保守：长度不足直接按 ERROR 处理）
                if (codeRaw.Length < 3)
                {
                    error++;
                    firstErrorRowNo ??= r.ExcelRowNo;
                    outRows.Add(new BomAuditRowDto(r.ExcelRowNo, BomAuditStatus.ERROR, codeRaw, r.Name ?? "", specRaw, r.Description ?? "", r.Brand ?? "",
                        "物料编码格式不正确"));
                    continue;
                }

                var categoryCode = new CategoryCode(codeRaw[..3].Trim().ToUpperInvariant());
                var spec = new Spec(specRaw);

                var normalizedCode = codeRaw.Trim().ToUpperInvariant();
                if (!codeCache.TryGetValue(normalizedCode, out var codeSnap))
                {
                    codeSnap = await _repo.GetCodeSpecByCodeAsync(normalizedCode, ct);
                    codeCache[normalizedCode] = codeSnap;
                }

                var specKey = categoryCode.Value + "||" + spec.Value;
                if (!specCache.TryGetValue(specKey, out var specExistsActive))
                {
                    specExistsActive = await _repo.SpecExistsAsync(categoryCode, spec, ct); // already active-only
                    specCache[specKey] = specExistsActive;
                }

                if (codeSnap is not null)
                {
                    // 需求：已废弃物料（status=0）在 BOM 审核中视为异常（优先级最高）
                    if (codeSnap.Status != 1)
                    {
                        error++;
                        firstErrorRowNo ??= r.ExcelRowNo;
                        outRows.Add(new BomAuditRowDto(
                            r.ExcelRowNo,
                            BomAuditStatus.ERROR,
                            codeRaw,
                            r.Name ?? "",
                            specRaw,
                            r.Description ?? "",
                            r.Brand ?? "",
                            "物料已废弃，禁止使用"));
                        continue;
                    }

                    // PASS / ERROR(B/C)
                    if (string.Equals(codeSnap.Spec, spec.Value, StringComparison.Ordinal))
                    {
                        var (s, reason, _) = BomAuditRules.Pass();
                        pass++;
                        outRows.Add(new BomAuditRowDto(r.ExcelRowNo, s, codeRaw, r.Name ?? "", specRaw, r.Description ?? "", r.Brand ?? "", reason));
                        continue;
                    }

                    if (specExistsActive)
                    {
                        var (s, reason, _) = BomAuditRules.ErrorCodeSpecConflict();
                        error++;
                        firstErrorRowNo ??= r.ExcelRowNo;
                        outRows.Add(new BomAuditRowDto(r.ExcelRowNo, s, codeRaw, r.Name ?? "", specRaw, r.Description ?? "", r.Brand ?? "", reason));
                        continue;
                    }

                    {
                        var (s, reason, _) = BomAuditRules.ErrorCodeExistsSpecMismatch();
                        error++;
                        firstErrorRowNo ??= r.ExcelRowNo;
                        outRows.Add(new BomAuditRowDto(r.ExcelRowNo, s, codeRaw, r.Name ?? "", specRaw, r.Description ?? "", r.Brand ?? "", reason));
                        continue;
                    }
                }

                // code 不存在
                if (specExistsActive)
                {
                    var (s, reason, _) = BomAuditRules.ErrorSpecExists();
                    error++;
                    firstErrorRowNo ??= r.ExcelRowNo;
                    outRows.Add(new BomAuditRowDto(r.ExcelRowNo, s, codeRaw, r.Name ?? "", specRaw, r.Description ?? "", r.Brand ?? "", reason));
                    continue;
                }

                {
                    var (s, reason, _) = BomAuditRules.New();
                    @new++;
                    outRows.Add(new BomAuditRowDto(r.ExcelRowNo, s, codeRaw, r.Name ?? "", specRaw, r.Description ?? "", r.Brand ?? "", reason));
                }
            }

            var resp = new AnalyzeBomResponse(
                FinishedCode: finishedCode,
                Version: version,
                Rows: outRows,
                TotalCount: outRows.Count,
                PassCount: pass,
                NewCount: @new,
                ErrorCount: error,
                MissingCodeErrorCount: missingCode,
                FirstErrorRowNo: firstErrorRowNo
            );

            // 一致性校验（Hardening）：统计必须与 rows[] 汇总一致
            var pass2 = outRows.Count(r => r.Status == BomAuditStatus.PASS);
            var new2 = outRows.Count(r => r.Status == BomAuditStatus.NEW);
            var err2 = outRows.Count(r => r.Status == BomAuditStatus.ERROR);
            var miss2 = outRows.Count(r => r.Status == BomAuditStatus.ERROR && r.ErrorReason == "缺少物料编码");
            if (pass2 != resp.PassCount || new2 != resp.NewCount || err2 != resp.ErrorCount || miss2 != resp.MissingCodeErrorCount)
            {
                _logger.LogError(
                    "BOM analyze inconsistent. finished_code={finishedCode} version={version} pass={pass}/{pass2} new={new}/{new2} err={err}/{err2} miss={miss}/{miss2}",
                    finishedCode, version, resp.PassCount, pass2, resp.NewCount, new2, resp.ErrorCount, err2, resp.MissingCodeErrorCount, miss2);
                return Result<AnalyzeBomResponse>.Fail(ErrorCodes.BOM_ANALYZE_INCONSISTENT_STATE, "analyze inconsistent state.");
            }

            return Result<AnalyzeBomResponse>.Ok(resp);
        }, ct);
}

