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
    // 约束名用于 Application 判定是否需要重试
    public const string CONSTRAINT_GROUP_CATEGORY_SERIAL = "UNIQUE(material_group.category_id, serial_no)";
    public const string CONSTRAINT_ITEM_GROUP_SUFFIX = "UNIQUE(material_item.group_id, suffix)";
    public const string CONSTRAINT_CATEGORY_CODE = "UNIQUE(category.code)";
    public const string CONSTRAINT_CATEGORY_NAME = "UNIQUE(category.name)";

    Task<bool> CategoryExistsAsync(CategoryCode categoryCode, CancellationToken ct = default);

    Task InsertCategoryAsync(string code, string name, CancellationToken ct = default);

    Task<IReadOnlyList<(string Code, string Name)>> ListCategoriesAsync(CancellationToken ct = default);

    Task<bool> SpecExistsAsync(CategoryCode categoryCode, Spec spec, CancellationToken ct = default);

    Task<int> GetMaxSerialNoAsync(CategoryCode categoryCode, CancellationToken ct = default);

    Task<int> InsertGroupAsync(CategoryCode categoryCode, int serialNo, CancellationToken ct = default);

    Task InsertItemAsync(int groupId, MaterialItem item, CancellationToken ct = default);

    Task<MaterialGroupSnapshot?> GetGroupSnapshotAsync(int groupId, CancellationToken ct = default);

    Task<MaterialItemStatusSnapshot?> GetItemStatusByCodeAsync(string code, CancellationToken ct = default);

    Task<int?> GetGroupIdByItemCodeAsync(string code, CancellationToken ct = default);

    Task DeprecateByCodeAsync(string code, CancellationToken ct = default);

    Task<PagedResult<MaterialItemSummary>> SearchByCodeAsync(SearchQuery query, CancellationToken ct = default);

    Task<PagedResult<MaterialItemSpecHit>> SearchBySpecAsync(SearchQuery query, CancellationToken ct = default);

    /// <summary>PRD 7.4：仅 status=1，按 category_code, serial_no, suffix 排序。</summary>
    Task<IReadOnlyList<MaterialExportRow>> ListActiveItemsForExportAsync(CancellationToken ct = default);
}

