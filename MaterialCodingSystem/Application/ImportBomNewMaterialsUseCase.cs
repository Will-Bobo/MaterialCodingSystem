using MaterialCodingSystem.Application.Contracts;
using Microsoft.Extensions.Logging;

namespace MaterialCodingSystem.Application;

public sealed record ImportBomNewMaterialsRequest(string FilePath, int? ExcelRowNo = null);

public sealed record ImportFailureSummary(string Reason, int Count);

public sealed record ImportBomNewMaterialsResponse(
    AnalyzeBomResponse AnalyzeResult,
    int SuccessCount,
    int FailureCount,
    IReadOnlyList<ImportFailureSummary> TopFailureReasons
);

public sealed class ImportBomNewMaterialsUseCase
{
    private readonly AnalyzeBomUseCase _analyze;
    private readonly MaterialApplicationService _materials;
    private readonly BomImportInProgressGate _gate;
    private readonly ILogger<ImportBomNewMaterialsUseCase> _logger;

    public ImportBomNewMaterialsUseCase(
        AnalyzeBomUseCase analyze,
        MaterialApplicationService materials,
        BomImportInProgressGate gate,
        ILogger<ImportBomNewMaterialsUseCase> logger)
    {
        _analyze = analyze;
        _materials = materials;
        _gate = gate;
        _logger = logger;
    }

    public async Task<Result<ImportBomNewMaterialsResponse>> ExecuteAsync(ImportBomNewMaterialsRequest req, CancellationToken ct = default)
    {
        // 1) analyze current state (Phase1 engine)
        var before = await _analyze.ExecuteAsync(new AnalyzeBomRequest(req.FilePath), ct);
        if (!before.IsSuccess || before.Data is null)
            return Result<ImportBomNewMaterialsResponse>.Fail(before.Error!.Code, before.Error.Message);

        var importKey = $"{before.Data.FinishedCode}||{before.Data.Version}";
        if (!_gate.TryEnter(importKey, out var sem))
        {
            _logger.LogInformation("BOM import in progress. finished_code={finishedCode} version={version}", before.Data.FinishedCode, before.Data.Version);
            return Result<ImportBomNewMaterialsResponse>.Fail(ErrorCodes.BOM_IMPORT_IN_PROGRESS, "import in progress.");
        }

        try
        {
        // 2) import only NEW rows, using existing Manual create chain
        var failures = new Dictionary<int, string>(); // excelRowNo -> reason
        var successCount = 0;

        var targetRows = before.Data.Rows
            .Where(r => r.Status == BomAuditStatus.NEW)
            .Where(r => req.ExcelRowNo is null || r.ExcelRowNo == req.ExcelRowNo.Value)
            .ToList();

        foreach (var row in targetRows)
        {
            ct.ThrowIfCancellationRequested();

            // 与“新建主物料A”体验保持一致：品牌必填。描述必填已由 Manual create 链路校验。
            if (string.IsNullOrWhiteSpace(row.Brand))
            {
                failures[row.ExcelRowNo] = "品牌为必填项";
                _logger.LogWarning(
                    "BOM import row blocked (missing brand). finished_code={finishedCode} version={version} excel_row={excelRow}",
                    before.Data.FinishedCode, before.Data.Version, row.ExcelRowNo);
                continue;
            }

            // category_code derived from code prefix (3 chars). PRD: BOM 不自动生成编码；NEW 的 code 必非空。
            var categoryCode = row.Code.Length >= 3 ? row.Code[..3] : "";
            var create = await _materials.CreateMaterialItemManual(new CreateMaterialItemManualRequest(
                CategoryCode: categoryCode,
                Code: row.Code,
                Spec: row.Spec,
                Name: row.Name,
                DisplayName: row.Name,
                Description: row.Description,
                Brand: row.Brand
            ), ct);

            if (!create.IsSuccess)
            {
                var reason = MapImportFailureToReason(create.Error!.Code, create.Error.Message);
                failures[row.ExcelRowNo] = reason;
                _logger.LogWarning("BOM import row failed. finished_code={finishedCode} version={version} excel_row={excelRow} error_code={errorCode} reason={reason}",
                    before.Data.FinishedCode, before.Data.Version, row.ExcelRowNo, create.Error!.Code, reason);
            }
            else
            {
                successCount++;
            }
        }

        // 3) re-analyze to refresh statuses for successful imports
        var after = await _analyze.ExecuteAsync(new AnalyzeBomRequest(req.FilePath), ct);
        if (!after.IsSuccess || after.Data is null)
            return Result<ImportBomNewMaterialsResponse>.Fail(after.Error!.Code, after.Error.Message);

        // 4) override failed rows as ERROR with error_reason (do not rewrite analyze engine)
        var newRows = after.Data.Rows
            .Select(r =>
            {
                if (!failures.TryGetValue(r.ExcelRowNo, out var reason))
                    return r;
                return r with { Status = BomAuditStatus.ERROR, ErrorReason = reason };
            })
            .ToList();

        var passCount = newRows.Count(r => r.Status == BomAuditStatus.PASS);
        var newCount = newRows.Count(r => r.Status == BomAuditStatus.NEW);
        var errorCount = newRows.Count(r => r.Status == BomAuditStatus.ERROR);
        var missingCodeErrorCount = newRows.Count(r => r.Status == BomAuditStatus.ERROR && r.ErrorReason == "缺少物料编码");
        var firstErrorRowNo = newRows.Where(r => r.Status == BomAuditStatus.ERROR).Select(r => (int?)r.ExcelRowNo).FirstOrDefault();

        var patched = after.Data with
        {
            Rows = newRows,
            TotalCount = newRows.Count,
            PassCount = passCount,
            NewCount = newCount,
            ErrorCount = errorCount,
            MissingCodeErrorCount = missingCodeErrorCount,
            FirstErrorRowNo = firstErrorRowNo
        };

        var top = failures.Values
            .GroupBy(x => x)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .Take(5)
            .Select(g => new ImportFailureSummary(g.Key, g.Count()))
            .ToList();

        return Result<ImportBomNewMaterialsResponse>.Ok(new ImportBomNewMaterialsResponse(
            AnalyzeResult: patched,
            SuccessCount: successCount,
            FailureCount: failures.Count,
            TopFailureReasons: top
        ));
        }
        finally
        {
            _gate.Exit(importKey, sem);
        }
    }

    private static string MapImportFailureToReason(string errorCode, string? errorMessage)
        => errorCode switch
        {
            ErrorCodes.CODE_DUPLICATE => "编码已存在",
            ErrorCodes.SPEC_DUPLICATE => "规格已存在，疑似重复建料",
            ErrorCodes.CODE_FORMAT_INVALID => "物料编码格式不正确",
            ErrorCodes.CATEGORY_MISMATCH => "编码分类与当前选择分类不一致",
            ErrorCodes.CATEGORY_NOT_FOUND => "分类不存在，请新增物料分类后再尝试",
            ErrorCodes.VALIDATION_ERROR when string.Equals(errorMessage, "description is required.", StringComparison.Ordinal)
                => "规格描述为必填项",
            _ => "导入失败"
        };
}

