using MaterialCodingSystem.Application.Interfaces;
using MaterialCodingSystem.Application.Contracts;
using MaterialCodingSystem.Domain.Entities;
using MaterialCodingSystem.Domain.ValueObjects;

namespace MaterialCodingSystem.Tests.Application;

internal sealed class NoopUnitOfWork : IUnitOfWork
{
    public Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken ct = default) => action();
}

internal sealed class CountingUnitOfWork : IUnitOfWork
{
    public int Executions { get; private set; }

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken ct = default)
    {
        Executions++;
        return await action();
    }
}

internal sealed class FakeMaterialRepository : IMaterialRepository
{
    public bool CategoryExists { get; set; } = true;
    public string CategoryName { get; set; } = "默认分类";
    public bool SpecExists { get; set; }

    public int MaxSerialNo { get; set; }

    public int InsertGroupCalled { get; private set; }
    public int InsertItemCalled { get; private set; }

    public CategoryCode? LastInsertedGroupCategoryCode { get; private set; }

    public bool GroupExists { get; set; } = true;
    public string GroupCategoryCode { get; set; } = "ZDA";
    public int GroupSerialNo { get; set; } = 1;
    public IReadOnlyCollection<char> ExistingSuffixes { get; set; } = new[] { 'A' };

    public bool ItemExistsByCode { get; set; } = true;
    public int ItemStatusByCode { get; set; } = 1;
    public int GroupSnapshotCalls { get; private set; }
    public int CategoryNameCalls { get; private set; }
    public int DeprecateCalled { get; private set; }

    public int FailGroupInsertWithSerialConflictTimes { get; set; }
    public int FailItemInsertWithCategorySpecTimes { get; set; }
    public int FailItemInsertWithSuffixConflictTimes { get; set; }
    public int FailItemInsertWithCodeConflictTimes { get; set; }

    public Task<bool> CategoryExistsAsync(CategoryCode categoryCode, CancellationToken ct = default)
        => Task.FromResult(CategoryExists);

    public Task<string?> GetCategoryNameByCodeAsync(CategoryCode categoryCode, CancellationToken ct = default)
    {
        CategoryNameCalls++;
        return Task.FromResult<string?>(CategoryExists ? CategoryName : null);
    }

    public int CategoryId { get; set; } = 1;

    public Task<int?> GetCategoryIdByCodeAsync(CategoryCode categoryCode, CancellationToken ct = default)
        => Task.FromResult<int?>(CategoryExists ? CategoryId : null);

    /// <summary>若设置，则 <see cref="InsertCategoryAsync"/> 抛出与 SQLite 类似的唯一约束信息，供 CreateCategory 映射测试使用。</summary>
    public string? InsertCategoryConstraintViolationMessage { get; set; }

    public Task InsertCategoryAsync(string code, string name, CancellationToken ct = default)
    {
        if (InsertCategoryConstraintViolationMessage is not null)
        {
            throw new DbConstraintViolationException("UNIQUE(category)", InsertCategoryConstraintViolationMessage);
        }

        return Task.CompletedTask;
    }

    public List<(string Code, string Name)> CategoryRows { get; } = new() { ("ZDA", "默认分类") };

    public Task<IReadOnlyList<(string Code, string Name)>> ListCategoriesAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<(string Code, string Name)>>(CategoryRows.ToList());

    public Task<bool> SpecExistsAsync(CategoryCode categoryCode, Spec spec, CancellationToken ct = default)
        => Task.FromResult(SpecExists);

    public Task<int> GetMaxSerialNoAsync(CategoryCode categoryCode, CancellationToken ct = default)
        => Task.FromResult(MaxSerialNo);

    public Task<int> InsertGroupAsync(CategoryCode categoryCode, int serialNo, CancellationToken ct = default)
    {
        InsertGroupCalled++;
        LastInsertedGroupCategoryCode = categoryCode;

        if (FailGroupInsertWithSerialConflictTimes > 0)
        {
            FailGroupInsertWithSerialConflictTimes--;
            MaxSerialNo = Math.Max(MaxSerialNo, serialNo);
            throw new DbConstraintViolationException(
                IMaterialRepository.CONSTRAINT_GROUP_CATEGORY_SERIAL,
                "serial_no conflict"
            );
        }

        MaxSerialNo = Math.Max(MaxSerialNo, serialNo);
        return Task.FromResult(1);
    }

    public int? GroupIdByCategoryAndSerialNo { get; set; } = 1;

    public Task<int?> GetGroupIdByCategoryAndSerialNoAsync(int categoryId, int serialNo, CancellationToken ct = default)
        => Task.FromResult(GroupIdByCategoryAndSerialNo);

    public Task InsertItemAsync(int groupId, MaterialItem item, CancellationToken ct = default)
    {
        InsertItemCalled++;

        if (FailItemInsertWithCodeConflictTimes > 0)
        {
            FailItemInsertWithCodeConflictTimes--;
            throw new DbConstraintViolationException(
                IMaterialRepository.CONSTRAINT_ITEM_CODE,
                "code conflict"
            );
        }

        if (FailItemInsertWithCategorySpecTimes > 0)
        {
            FailItemInsertWithCategorySpecTimes--;
            throw new DbConstraintViolationException(
                IMaterialRepository.CONSTRAINT_ITEM_CATEGORY_SPEC,
                "UNIQUE constraint failed: material_item.category_code, material_item.spec");
        }

        if (FailItemInsertWithSuffixConflictTimes > 0)
        {
            FailItemInsertWithSuffixConflictTimes--;
            throw new DbConstraintViolationException(
                IMaterialRepository.CONSTRAINT_ITEM_GROUP_SUFFIX,
                "suffix conflict"
            );
        }

        return Task.CompletedTask;
    }

    public Task<MaterialGroupSnapshot?> GetGroupSnapshotAsync(int groupId, CancellationToken ct = default)
    {
        GroupSnapshotCalls++;
        if (!GroupExists) return Task.FromResult<MaterialGroupSnapshot?>(null);
        return Task.FromResult<MaterialGroupSnapshot?>(new MaterialGroupSnapshot(
            GroupId: groupId,
            CategoryCode: new CategoryCode(GroupCategoryCode),
            SerialNo: GroupSerialNo,
            ExistingSuffixes: ExistingSuffixes
        ));
    }

    public int BaseItemStatusByGroupId { get; set; } = 1;

    public Task<MaterialItemStatusSnapshot?> GetBaseItemStatusByGroupIdAsync(int groupId, CancellationToken ct = default)
    {
        if (!GroupExists) return Task.FromResult<MaterialItemStatusSnapshot?>(null);
        return Task.FromResult<MaterialItemStatusSnapshot?>(new MaterialItemStatusSnapshot($"{GroupCategoryCode}{GroupSerialNo:D7}A", BaseItemStatusByGroupId));
    }

    public Task<MaterialItemStatusSnapshot?> GetItemStatusByCodeAsync(string code, CancellationToken ct = default)
    {
        if (!ItemExistsByCode) return Task.FromResult<MaterialItemStatusSnapshot?>(null);
        return Task.FromResult<MaterialItemStatusSnapshot?>(new MaterialItemStatusSnapshot(code, ItemStatusByCode));
    }

    public Task<int?> GetGroupIdByItemCodeAsync(string code, CancellationToken ct = default)
        => Task.FromResult<int?>(GroupExists ? 1 : null);

    public Task DeprecateByCodeAsync(string code, CancellationToken ct = default)
    {
        DeprecateCalled++;
        ItemStatusByCode = 0;
        return Task.CompletedTask;
    }

    public List<MaterialItemSpecHit> SpecSearchHits { get; } = new();

    public SearchQuery? LastSearchBySpecQuery { get; private set; }

    public Task<PagedResult<MaterialItemSummary>> SearchByCodeAsync(SearchQuery query, CancellationToken ct = default)
        => Task.FromResult(new PagedResult<MaterialItemSummary>(0, Array.Empty<MaterialItemSummary>()));

    public Task<PagedResult<MaterialItemSpecHit>> SearchBySpecAsync(SearchQuery query, CancellationToken ct = default)
    {
        LastSearchBySpecQuery = query;
        var list = SpecSearchHits.ToList();
        return Task.FromResult(new PagedResult<MaterialItemSpecHit>(list.Count, list));
    }

    public string? LastSearchBySpecAllKeyword { get; private set; }
    public bool LastSearchBySpecAllIncludeDeprecated { get; private set; }
    public int LastSearchBySpecAllLimit { get; private set; }
    public string? LastSearchCandidatesBySpecOnlyCategoryCode { get; private set; }
    public string? LastSearchCandidatesBySpecOnlyKeyword { get; private set; }
    public int LastSearchCandidatesBySpecOnlyLimit { get; private set; }

    public Task<PagedResult<MaterialItemSpecHit>> SearchBySpecAllAsync(string keyword, bool includeDeprecated, int limit, CancellationToken ct = default)
    {
        LastSearchBySpecAllKeyword = keyword;
        LastSearchBySpecAllIncludeDeprecated = includeDeprecated;
        LastSearchBySpecAllLimit = limit;
        var list = SpecSearchHits.Take(limit).ToList();
        return Task.FromResult(new PagedResult<MaterialItemSpecHit>(list.Count, list));
    }

    public Task<PagedResult<MaterialItemSpecHit>> SearchCandidatesBySpecOnlyAsync(
        string categoryCode,
        string keyword,
        int limit,
        CancellationToken ct = default)
    {
        LastSearchCandidatesBySpecOnlyCategoryCode = categoryCode;
        LastSearchCandidatesBySpecOnlyKeyword = keyword;
        LastSearchCandidatesBySpecOnlyLimit = limit;
        var list = SpecSearchHits.Take(limit).ToList();
        return Task.FromResult(new PagedResult<MaterialItemSpecHit>(list.Count, list));
    }

    public Task<IReadOnlyList<MaterialExportRow>> ListActiveItemsForExportAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<MaterialExportRow>>(Array.Empty<MaterialExportRow>());

    public Task<IReadOnlyList<MaterialExportRow>> ListAllItemsForExportAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<MaterialExportRow>>(Array.Empty<MaterialExportRow>());
}

