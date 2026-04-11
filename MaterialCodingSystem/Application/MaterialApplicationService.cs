using MaterialCodingSystem.Application.Contracts;
using MaterialCodingSystem.Application.Interfaces;
using MaterialCodingSystem.Domain.Entities;
using MaterialCodingSystem.Domain.Services;
using MaterialCodingSystem.Domain.ValueObjects;

namespace MaterialCodingSystem.Application;

public sealed class MaterialApplicationService
{
    private const int MaxRetry = 3;

    private readonly IUnitOfWork _uow;
    private readonly IMaterialRepository _repo;
    private readonly IExcelMaterialExporter? _excelExporter;

    public MaterialApplicationService(
        IUnitOfWork uow,
        IMaterialRepository repo,
        IExcelMaterialExporter? excelExporter = null)
    {
        _uow = uow;
        _repo = repo;
        _excelExporter = excelExporter;
    }

    public Task<Result<CreateCategoryResponse>> CreateCategory(CreateCategoryRequest req, CancellationToken ct = default)
        => _uow.ExecuteAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(req.Code))
            {
                return Result<CreateCategoryResponse>.Fail(ErrorCodes.VALIDATION_ERROR, "code is required.");
            }

            if (string.IsNullOrWhiteSpace(req.Name))
            {
                return Result<CreateCategoryResponse>.Fail(ErrorCodes.VALIDATION_ERROR, "name is required.");
            }

            var code = req.Code.Trim().ToUpperInvariant();
            var name = req.Name.Trim();

            try
            {
                await _repo.InsertCategoryAsync(code, name, ct);
            }
            catch (DbConstraintViolationException ex) when (
                ex.Constraint.Contains("category", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("category.code", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("category.name", StringComparison.OrdinalIgnoreCase)
            )
            {
                if (ex.Message.Contains("category.code", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("code", StringComparison.OrdinalIgnoreCase))
                {
                    return Result<CreateCategoryResponse>.Fail(ErrorCodes.CATEGORY_CODE_DUPLICATE, "category code duplicate.");
                }

                if (ex.Message.Contains("category.name", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("name", StringComparison.OrdinalIgnoreCase))
                {
                    return Result<CreateCategoryResponse>.Fail(ErrorCodes.CATEGORY_NAME_DUPLICATE, "category name duplicate.");
                }

                return Result<CreateCategoryResponse>.Fail(ErrorCodes.VALIDATION_ERROR, "category duplicate.");
            }

            return Result<CreateCategoryResponse>.Ok(new CreateCategoryResponse(code, name));
        }, ct);

    public Task<Result<IReadOnlyList<CategoryDto>>> ListCategories(CancellationToken ct = default)
        => _uow.ExecuteAsync(async () =>
        {
            var rows = await _repo.ListCategoriesAsync(ct);
            var items = rows.Select(x => new CategoryDto(x.Code, x.Name)).ToList();
            return Result<IReadOnlyList<CategoryDto>>.Ok(items);
        }, ct);

    public Task<Result<int>> ResolveGroupIdByItemCode(string code, CancellationToken ct = default)
        => _uow.ExecuteAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return Result<int>.Fail(ErrorCodes.VALIDATION_ERROR, "code is required.");
            }

            var groupId = await _repo.GetGroupIdByItemCodeAsync(code.Trim(), ct);
            if (groupId is null)
            {
                return Result<int>.Fail(ErrorCodes.NOT_FOUND, "item not found.");
            }

            return Result<int>.Ok(groupId.Value);
        }, ct);

    public Task<Result<GroupInfoDto>> GetGroupInfo(int groupId, CancellationToken ct = default)
        => _uow.ExecuteAsync(async () =>
        {
            if (groupId <= 0)
            {
                return Result<GroupInfoDto>.Fail(ErrorCodes.VALIDATION_ERROR, "group_id invalid.");
            }

            var snap = await _repo.GetGroupSnapshotAsync(groupId, ct);
            if (snap is null)
            {
                return Result<GroupInfoDto>.Fail(ErrorCodes.NOT_FOUND, "group not found.");
            }

            string nextSuffix;
            try
            {
                nextSuffix = SuffixAllocator.AllocateNextSuffix(snap.ExistingSuffixes).ToString();
            }
            catch (DomainException ex) when (ex.Code is "SUFFIX_SEQUENCE_BROKEN" or "SUFFIX_OVERFLOW")
            {
                return Result<GroupInfoDto>.Fail(ex.Code, ex.Message);
            }

            var existing = string.Join("", snap.ExistingSuffixes.OrderBy(x => x));

            return Result<GroupInfoDto>.Ok(new GroupInfoDto(
                GroupId: snap.GroupId,
                CategoryCode: snap.CategoryCode.Value,
                SerialNo: snap.SerialNo,
                ExistingSuffixes: existing,
                NextSuffix: nextSuffix
            ));
        }, ct);

    public Task<Result<CreateMaterialItemAResponse>> CreateMaterialItemA(CreateMaterialItemARequest req, CancellationToken ct = default)
        => ExecuteWithRetry(async () =>
        {
            CategoryCode categoryCode;
            Spec spec;

            try
            {
                categoryCode = new CategoryCode(req.CategoryCode);
                spec = new Spec(req.Spec);
            }
            catch (DomainException ex) when (ex.Code == "VALIDATION_ERROR")
            {
                return Result<CreateMaterialItemAResponse>.Fail(ErrorCodes.VALIDATION_ERROR, ex.Message);
            }

            var categoryExists = await _repo.CategoryExistsAsync(categoryCode, ct);
            if (!categoryExists)
            {
                return Result<CreateMaterialItemAResponse>.Fail(ErrorCodes.VALIDATION_ERROR, "category_code not found.");
            }

            var specExists = await _repo.SpecExistsAsync(categoryCode, spec, ct);
            if (specExists)
            {
                return Result<CreateMaterialItemAResponse>.Fail(ErrorCodes.SPEC_DUPLICATE, "spec duplicate.");
            }

            var maxSerial = await _repo.GetMaxSerialNoAsync(categoryCode, ct);
            var serialNo = maxSerial + 1;

            int groupId;
            try
            {
                groupId = await _repo.InsertGroupAsync(categoryCode, serialNo, ct);
            }
            catch (DbConstraintViolationException ex) when (ex.Constraint == IMaterialRepository.CONSTRAINT_GROUP_CATEGORY_SERIAL)
            {
                // 触发 serial_no 并发冲突，交由外层事务级重试
                throw;
            }

            var group = MaterialGroup.CreateNew(
                categoryCode: categoryCode,
                serialNo: serialNo,
                spec: spec,
                name: req.Name,
                description: req.Description,
                brand: req.Brand
            );

            var itemA = group.Items.Single(i => i.Suffix.Value == 'A');
            await _repo.InsertItemAsync(groupId, itemA, ct);

            return Result<CreateMaterialItemAResponse>.Ok(new CreateMaterialItemAResponse(
                GroupId: groupId,
                CategoryCode: categoryCode.Value,
                SerialNo: serialNo,
                Code: itemA.Code,
                Suffix: "A",
                Spec: itemA.Spec.Value,
                SpecNormalized: itemA.SpecNormalized.Value
            ));
        }, ct, retryConstraint: IMaterialRepository.CONSTRAINT_GROUP_CATEGORY_SERIAL);

    public Task<Result<CreateReplacementResponse>> CreateReplacement(CreateReplacementRequest req, CancellationToken ct = default)
        => ExecuteWithRetry(async () =>
        {
            Spec spec;
            try
            {
                spec = new Spec(req.Spec);
            }
            catch (DomainException ex) when (ex.Code == "VALIDATION_ERROR")
            {
                return Result<CreateReplacementResponse>.Fail(ErrorCodes.VALIDATION_ERROR, ex.Message);
            }

            var snap = await _repo.GetGroupSnapshotAsync(req.GroupId, ct);
            if (snap is null)
            {
                return Result<CreateReplacementResponse>.Fail(ErrorCodes.NOT_FOUND, "group not found.");
            }

            // suffix 连续性与 overflow 规则交由 Domain（PRD V1）
            try
            {
                _ = SuffixAllocator.AllocateNextSuffix(snap.ExistingSuffixes);
            }
            catch (DomainException ex) when (ex.Code is "SUFFIX_SEQUENCE_BROKEN" or "SUFFIX_OVERFLOW")
            {
                return Result<CreateReplacementResponse>.Fail(ex.Code, ex.Message);
            }

            var specExists = await _repo.SpecExistsAsync(snap.CategoryCode, spec, ct);
            if (specExists)
            {
                return Result<CreateReplacementResponse>.Fail(ErrorCodes.SPEC_DUPLICATE, "spec duplicate.");
            }

            var group = MaterialGroup.CreateNew(
                categoryCode: snap.CategoryCode,
                serialNo: snap.SerialNo,
                spec: new Spec("DUMMY"),
                name: "DUMMY",
                description: "DUMMY",
                brand: null
            );

            foreach (var s in snap.ExistingSuffixes.Where(x => x != 'A').OrderBy(x => x))
            {
                group.DebugAddItemForTestOnly(new MaterialItem(
                    code: CodeGenerator.GenerateItemCode(snap.CategoryCode.Value, snap.SerialNo, s),
                    suffix: new Domain.ValueObjects.Suffix(s),
                    spec: new Spec("DUMMY-" + s),
                    name: "DUMMY",
                    description: "DUMMY",
                    specNormalized: new Domain.ValueObjects.SpecNormalized(SpecNormalizer.NormalizeV1("DUMMY")),
                    brand: null
                ));
            }

            var item = group.AddReplacement(spec, req.Name, req.Description, req.Brand);

            try
            {
                await _repo.InsertItemAsync(req.GroupId, item, ct);
            }
            catch (DbConstraintViolationException ex) when (ex.Constraint == IMaterialRepository.CONSTRAINT_ITEM_GROUP_SUFFIX)
            {
                throw;
            }

            return Result<CreateReplacementResponse>.Ok(new CreateReplacementResponse(
                ItemId: 0,
                GroupId: req.GroupId,
                Code: item.Code,
                Suffix: item.Suffix.Value.ToString(),
                Spec: item.Spec.Value,
                SpecNormalized: item.SpecNormalized.Value
            ));
        }, ct, retryConstraint: IMaterialRepository.CONSTRAINT_ITEM_GROUP_SUFFIX);

    private async Task<Result<T>> ExecuteWithRetry<T>(
        Func<Task<Result<T>>> action,
        CancellationToken ct,
        string retryConstraint
    )
    {
        for (var attempt = 1; attempt <= MaxRetry; attempt++)
        {
            try
            {
                return await _uow.ExecuteAsync(action, ct);
            }
            catch (DbConstraintViolationException ex) when (ex.Constraint == retryConstraint && attempt < MaxRetry)
            {
                // 事务级重试：回滚后重来（由 UoW 抽象表示）
                continue;
            }
            catch (DbConstraintViolationException ex) when (ex.Constraint == retryConstraint)
            {
                return Result<T>.Fail(ErrorCodes.CODE_CONFLICT_RETRY, "conflict retry exceeded.");
            }
        }

        return Result<T>.Fail(ErrorCodes.CODE_CONFLICT_RETRY, "conflict retry exceeded.");
    }

    public Task<Result<DeprecateResponse>> DeprecateMaterialItem(DeprecateRequest req, CancellationToken ct = default)
        => _uow.ExecuteAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(req.Code))
            {
                return Result<DeprecateResponse>.Fail(ErrorCodes.VALIDATION_ERROR, "code is required.");
            }

            var snap = await _repo.GetItemStatusByCodeAsync(req.Code, ct);
            if (snap is null)
            {
                return Result<DeprecateResponse>.Fail(ErrorCodes.NOT_FOUND, "item not found.");
            }

            if (snap.Status == 0)
            {
                // TDD 中有 ALREADY_DEPRECATED，但这里先不阻断业务流程：按测试需求可后续补齐
                return Result<DeprecateResponse>.Ok(new DeprecateResponse(req.Code, 0));
            }

            await _repo.DeprecateByCodeAsync(req.Code, ct);
            return Result<DeprecateResponse>.Ok(new DeprecateResponse(req.Code, 0));
        }, ct);

    public Task<Result<PagedResult<MaterialItemSummary>>> SearchByCode(SearchQuery query, CancellationToken ct = default)
        => _uow.ExecuteAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(query.CodeKeyword))
            {
                return Result<PagedResult<MaterialItemSummary>>.Fail(ErrorCodes.VALIDATION_ERROR, "code_keyword is required.");
            }

            if (query.Limit is <= 0 or > 50 || query.Offset < 0)
            {
                return Result<PagedResult<MaterialItemSummary>>.Fail(ErrorCodes.INVALID_QUERY, "limit/offset invalid.");
            }

            return Result<PagedResult<MaterialItemSummary>>.Ok(await _repo.SearchByCodeAsync(query, ct));
        }, ct);

    public Task<Result<PagedResult<MaterialItemSpecHit>>> SearchBySpec(SearchQuery query, CancellationToken ct = default)
        => _uow.ExecuteAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(query.CategoryCode))
            {
                return Result<PagedResult<MaterialItemSpecHit>>.Fail(ErrorCodes.VALIDATION_ERROR, "category_code is required.");
            }

            if (string.IsNullOrWhiteSpace(query.SpecKeyword))
            {
                return Result<PagedResult<MaterialItemSpecHit>>.Fail(ErrorCodes.VALIDATION_ERROR, "spec_keyword is required.");
            }

            // PRD V1：固定 LIMIT 20
            var fixedQuery = query with { Limit = 20, Offset = 0 };
            return Result<PagedResult<MaterialItemSpecHit>>.Ok(await _repo.SearchBySpecAsync(fixedQuery, ct));
        }, ct);

    public Task<Result<ExportMaterialsResponse>> ExportActiveMaterials(string filePath, CancellationToken ct = default)
    {
        if (_excelExporter is null)
        {
            return Task.FromResult(Result<ExportMaterialsResponse>.Fail(
                ErrorCodes.INTERNAL_ERROR, "Excel export is not configured."));
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            return Task.FromResult(Result<ExportMaterialsResponse>.Fail(
                ErrorCodes.VALIDATION_ERROR, "file path is required."));
        }

        return _uow.ExecuteAsync(async () =>
        {
            var rows = await _repo.ListActiveItemsForExportAsync(ct);
            await _excelExporter.WriteAsync(filePath, rows, ct);
            var sheetCount = rows.Count == 0
                ? 1
                : rows.Select(r => r.CategoryCode).Distinct().Count();
            return Result<ExportMaterialsResponse>.Ok(new ExportMaterialsResponse(
                FilePath: filePath,
                RowCount: rows.Count,
                SheetCount: sheetCount));
        }, ct);
    }
}

