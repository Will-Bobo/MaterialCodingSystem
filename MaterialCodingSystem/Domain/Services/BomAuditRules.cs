using MaterialCodingSystem.Application.Contracts;

namespace MaterialCodingSystem.Domain.Services;

public static class BomAuditRules
{
    public static (BomAuditStatus Status, string? ErrorReason, bool IsMissingCode) ErrorMissingCode()
        => (BomAuditStatus.ERROR, "缺少物料编码", true);

    public static (BomAuditStatus Status, string? ErrorReason, bool IsMissingCode) ErrorMissingSpec()
        => (BomAuditStatus.ERROR, "缺少规格", false);

    public static (BomAuditStatus Status, string? ErrorReason, bool IsMissingCode) Pass()
        => (BomAuditStatus.PASS, null, false);

    public static (BomAuditStatus Status, string? ErrorReason, bool IsMissingCode) New()
        => (BomAuditStatus.NEW, null, false);

    public static (BomAuditStatus Status, string? ErrorReason, bool IsMissingCode) ErrorSpecExists()
        => (BomAuditStatus.ERROR, "规格已存在，疑似重复建料", false);

    public static (BomAuditStatus Status, string? ErrorReason, bool IsMissingCode) ErrorCodeSpecConflict()
        => (BomAuditStatus.ERROR, "编码与规格冲突", false);

    public static (BomAuditStatus Status, string? ErrorReason, bool IsMissingCode) ErrorCodeExistsSpecMismatch()
        => (BomAuditStatus.ERROR, "编码已存在但规格不一致", false);
}

