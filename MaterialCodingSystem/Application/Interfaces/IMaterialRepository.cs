using MaterialCodingSystem.Domain.Entities;
using MaterialCodingSystem.Domain.ValueObjects;
using MaterialCodingSystem.Application.Contracts;

namespace MaterialCodingSystem.Application.Interfaces;

public sealed record MaterialGroupSnapshot(
    int GroupId,
    CategoryCode CategoryCode,
    int SerialNo,
    IReadOnlyCollection<char> ExistingSuffixes
);

public sealed record MaterialItemStatusSnapshot(string Code, int Status);

public interface IMaterialRepository
{
    // 约束名用于 Application 判定是否需要重试（与 SqliteMaterialRepository 解析结果一致）
    public const string CONSTRAINT_GROUP_CATEGORY_SERIAL = "UNIQUE(material_group.category_id, serial_no)";
    public const string CONSTRAINT_ITEM_GROUP_SUFFIX = "UNIQUE(material_item.group_id, suffix)";
    public const string CONSTRAINT_ITEM_CATEGORY_SPEC = "UNIQUE(material_item.category_code, spec)";
    public const string CONSTRAINT_ITEM_CODE = "UNIQUE(material_item.code)";
    public const string CONSTRAINT_UNKNOWN = "UNKNOWN";
    public const string CONSTRAINT_CATEGORY_CODE = "UNIQUE(category.code)";
    public const string CONSTRAINT_CATEGORY_NAME = "UNIQUE(category.name)";

    Task<bool> CategoryExistsAsync(CategoryCode categoryCode, CancellationToken ct = default);

    Task<string?> GetCategoryNameByCodeAsync(CategoryCode categoryCode, CancellationToken ct = default);

    Task InsertCategoryAsync(string code, string name, int startSerialNo, CancellationToken ct = default);

    Task<IReadOnlyList<(string Code, string Name, int StartSerialNo)>> ListCategoriesAsync(CancellationToken ct = default);

    Task<int?> GetCategoryIdByCodeAsync(CategoryCode categoryCode, CancellationToken ct = default);

    Task<int> GetCategoryStartSerialNoAsync(int categoryId, CancellationToken ct = default);

    Task<bool> SpecExistsAsync(CategoryCode categoryCode, Spec spec, CancellationToken ct = default);

    Task<int> GetMaxSerialNoAsync(CategoryCode categoryCode, CancellationToken ct = default);

    Task<int?> GetGroupIdByCategoryIdAndSerialNoAsync(int categoryId, int serialNo, CancellationToken ct = default);

    Task<int> InsertGroupAsync(CategoryCode categoryCode, int serialNo, CancellationToken ct = default);

    Task InsertItemAsync(int groupId, MaterialItem item, CancellationToken ct = default);

    Task<IReadOnlyList<string>> ListCategoryCodesAsync(CancellationToken ct = default);

    Task<CreateMaterialResponse?> GetCreateMaterialSuccessByRequestIdAsync(string requestId, CancellationToken ct = default);

    Task InsertCreateMaterialSuccessLogAsync(string requestId, CreateMaterialResponse response, CancellationToken ct = default);

    Task<MaterialGroupSnapshot?> GetGroupSnapshotAsync(int groupId, CancellationToken ct = default);

    /// <summary>替代料基准校验：同组 suffix='A' 的状态。</summary>
    Task<MaterialItemStatusSnapshot?> GetBaseItemStatusByGroupIdAsync(int groupId, CancellationToken ct = default);

    Task<MaterialItemStatusSnapshot?> GetItemStatusByCodeAsync(string code, CancellationToken ct = default);

    Task<int?> GetGroupIdByItemCodeAsync(string code, CancellationToken ct = default);

    Task DeprecateByCodeAsync(string code, CancellationToken ct = default);

    Task<PagedResult<MaterialItemSummary>> SearchByCodeAsync(SearchQuery query, CancellationToken ct = default);

    Task<PagedResult<MaterialItemSpecHit>> SearchBySpecAsync(SearchQuery query, CancellationToken ct = default);

    Task<PagedResult<MaterialItemSpecHit>> SearchBySpecAllAsync(string keyword, bool includeDeprecated, int limit, CancellationToken ct = default);

    /// <summary>
    /// CreateMaterial 候选收敛：仅基于 spec（规格号）模糊匹配；固定 status=1；不使用 spec_normalized。
    /// </summary>
    Task<PagedResult<MaterialItemSpecHit>> SearchCandidatesBySpecOnlyAsync(
        string categoryCode,
        string keyword,
        int limit,
        CancellationToken ct = default);

    /// <summary>PRD 7.4：仅 status=1，按 category_code, serial_no, suffix 排序。</summary>
    Task<IReadOnlyList<MaterialExportRow>> ListActiveItemsForExportAsync(CancellationToken ct = default);

    /// <summary>PRD 7.4（V1.3）：导出全量（含 status=0），name=category.name，排序见 PRD。</summary>
    Task<IReadOnlyList<MaterialExportRow>> ListAllItemsForExportAsync(CancellationToken ct = default);
}

