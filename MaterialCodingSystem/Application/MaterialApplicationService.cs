using System.IO;
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
            var startSerialNo = req.StartSerialNo <= 0 ? 1 : req.StartSerialNo;
            if (startSerialNo < 1)
            {
                return Result<CreateCategoryResponse>.Fail(ErrorCodes.VALIDATION_ERROR, "start_serial_no must be >= 1.");
            }

            try
            {
                await _repo.InsertCategoryAsync(code, name, startSerialNo, ct);
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

            return Result<CreateCategoryResponse>.Ok(new CreateCategoryResponse(code, name, startSerialNo));
        }, ct);

    public Task<Result<IReadOnlyList<CategoryDto>>> ListCategories(CancellationToken ct = default)
        => _uow.ExecuteAsync(async () =>
        {
            var rows = await _repo.ListCategoriesAsync(ct);
            var items = rows.Select(x => new CategoryDto(x.Code, x.Name, x.StartSerialNo)).ToList();
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

            var categoryName = await _repo.GetCategoryNameByCodeAsync(snap.CategoryCode, ct);
            if (string.IsNullOrWhiteSpace(categoryName))
            {
                return Result<GroupInfoDto>.Fail(ErrorCodes.CATEGORY_NOT_FOUND, "category_code not found.");
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
                CategoryName: categoryName,
                SerialNo: snap.SerialNo,
                ExistingSuffixes: existing,
                NextSuffix: nextSuffix
            ));
        }, ct);

    /// <summary>
    /// 下一组流水号建议值 = max(serial_no)+1（无组时为 1）。仅查询与 +1，不插入组。
    /// 供 PRD Validation 等经 Application 入口使用。
    /// </summary>
    public Task<Result<int>> AllocateNextGroupSerial(string categoryCodeRaw, CancellationToken ct = default)
        => _uow.ExecuteAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(categoryCodeRaw))
            {
                return Result<int>.Fail(ErrorCodes.VALIDATION_ERROR, "category_code is required.");
            }

            CategoryCode categoryCode;
            try
            {
                categoryCode = new CategoryCode(categoryCodeRaw);
            }
            catch (DomainException ex) when (ex.Code == "VALIDATION_ERROR")
            {
                return Result<int>.Fail(ErrorCodes.VALIDATION_ERROR, ex.Message);
            }

            var max = await _repo.GetMaxSerialNoAsync(categoryCode, ct);
            return Result<int>.Ok(max + 1);
        }, ct);

    public async Task<Result<CreateMaterialItemAResponse>> CreateMaterialItemA(CreateMaterialItemARequest req, CancellationToken ct = default)
    {
        var res = await CreateMaterial(new CreateMaterialRequest(
            CategoryCode: req.CategoryCode,
            Spec: req.Spec,
            Name: req.Name,
            Description: req.Description,
            Brand: req.Brand,
            CodeMode: CreateMaterialCodeMode.Auto
        ), ct);

        if (!res.IsSuccess)
        {
            return Result<CreateMaterialItemAResponse>.Fail(
                res.Error!.Code,
                res.Error.Message,
                res.Error.ValidationErrors ?? new Dictionary<string, string>());
        }

        var d = res.Data!;
        return Result<CreateMaterialItemAResponse>.Ok(new CreateMaterialItemAResponse(
            GroupId: d.GroupId,
            CategoryCode: d.CategoryCode,
            SerialNo: d.SerialNo,
            Code: d.Code,
            Suffix: d.Suffix,
            Spec: d.Spec,
            SpecNormalized: d.SpecNormalized
        ));
    }

    public Task<Result<CreateMaterialResponse>> CreateMaterial(CreateMaterialRequest req, CancellationToken ct = default)
        => ExecuteWithRetry(async () =>
        {
            // Idempotency (success-only): same RequestId returns first success result
            if (!string.IsNullOrWhiteSpace(req.RequestId))
            {
                var existing = await _repo.GetCreateMaterialSuccessByRequestIdAsync(req.RequestId!, ct);
                if (existing is not null)
                {
                    return Result<CreateMaterialResponse>.Ok(existing);
                }
            }

            if (string.IsNullOrWhiteSpace(req.Description))
                return Result<CreateMaterialResponse>.Fail(ErrorCodes.VALIDATION_ERROR, "description is required.");

            CategoryCode categoryCode;
            Spec spec;
            try
            {
                categoryCode = new CategoryCode(req.CategoryCode);
                spec = new Spec(req.Spec);
            }
            catch (DomainException ex) when (ex.Code == "VALIDATION_ERROR")
            {
                return Result<CreateMaterialResponse>.Fail(ErrorCodes.VALIDATION_ERROR, ex.Message);
            }

            var categoryId = await _repo.GetCategoryIdByCodeAsync(categoryCode, ct);
            if (categoryId is null)
                return Result<CreateMaterialResponse>.Fail(ErrorCodes.CATEGORY_NOT_FOUND, "category_code not found.");

            var categoryName = await _repo.GetCategoryNameByCodeAsync(categoryCode, ct);
            if (string.IsNullOrWhiteSpace(categoryName))
                return Result<CreateMaterialResponse>.Fail(ErrorCodes.CATEGORY_NOT_FOUND, "category_code not found.");

            // Auto/manual share spec uniqueness (per existing rules)
            var specExists = await _repo.SpecExistsAsync(categoryCode, spec, ct);
            if (specExists)
                return Result<CreateMaterialResponse>.Fail(ErrorCodes.SPEC_DUPLICATE, "spec duplicate.");

            var startSerialNo = await _repo.GetCategoryStartSerialNoAsync(categoryId.Value, ct);

            if (req.CodeMode == CreateMaterialCodeMode.Auto)
            {
                var maxSerial = await _repo.GetMaxSerialNoAsync(categoryCode, ct);
                var serialNo = Math.Max(maxSerial, startSerialNo - 1) + 1;

                int groupId;
                try
                {
                    groupId = await _repo.InsertGroupAsync(categoryCode, serialNo, ct);
                }
                catch (DbConstraintViolationException ex) when (ex.Constraint == IMaterialRepository.CONSTRAINT_GROUP_CATEGORY_SERIAL)
                {
                    throw;
                }

                var group = MaterialGroup.CreateNew(
                    categoryCode: categoryCode,
                    serialNo: serialNo,
                    spec: spec,
                    name: categoryName,
                    description: req.Description,
                    brand: req.Brand
                );

                var itemA = group.Items.Single(i => i.Suffix.Value == 'A');
                try
                {
                    await _repo.InsertItemAsync(groupId, itemA, ct);
                }
                catch (DbConstraintViolationException ex) when (ex.Constraint == IMaterialRepository.CONSTRAINT_ITEM_CATEGORY_SPEC)
                {
                    return Result<CreateMaterialResponse>.Fail(ErrorCodes.SPEC_DUPLICATE, "spec duplicate.");
                }
                catch (DbConstraintViolationException ex) when (ex.Constraint == IMaterialRepository.CONSTRAINT_ITEM_CODE)
                {
                    return Result<CreateMaterialResponse>.Fail(ErrorCodes.CODE_CONFLICT_RETRY, "code conflict.");
                }

                var ok = new CreateMaterialResponse(
                    GroupId: groupId,
                    CategoryCode: categoryCode.Value,
                    SerialNo: serialNo,
                    Code: itemA.Code,
                    Suffix: "A",
                    Spec: itemA.Spec.Value,
                    SpecNormalized: itemA.SpecNormalized.Value
                );

                if (!string.IsNullOrWhiteSpace(req.RequestId))
                {
                    try
                    {
                        await _repo.InsertCreateMaterialSuccessLogAsync(req.RequestId!, ok, ct);
                    }
                    catch (DbConstraintViolationException)
                    {
                        var existing = await _repo.GetCreateMaterialSuccessByRequestIdAsync(req.RequestId!, ct);
                        if (existing is not null)
                            return Result<CreateMaterialResponse>.Ok(existing);
                    }
                }

                return Result<CreateMaterialResponse>.Ok(ok);
            }

            // Manual existing code
            if (string.IsNullOrWhiteSpace(req.ExistingCode))
                return Result<CreateMaterialResponse>.Fail(ErrorCodes.VALIDATION_ERROR, "existing_code is required.");

            var normalizedExisting = req.ExistingCode.Trim().ToUpperInvariant();
            var codes = await _repo.ListCategoryCodesAsync(ct);
            var matched = codes
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c.Trim().ToUpperInvariant())
                .Where(c => normalizedExisting.StartsWith(c, StringComparison.Ordinal))
                .Where(c => normalizedExisting.Length - c.Length == 8)
                .OrderByDescending(c => c.Length)
                .FirstOrDefault();

            if (string.IsNullOrWhiteSpace(matched))
                return Result<CreateMaterialResponse>.Fail(ErrorCodes.VALIDATION_ERROR, "existing_code category_code invalid.");

            ParsedMaterialCode parsed;
            try
            {
                parsed = MaterialCodeParser.ParseExistingCodeWithCategory(normalizedExisting, matched);
            }
            catch (DomainException ex) when (ex.Code == "VALIDATION_ERROR")
            {
                return Result<CreateMaterialResponse>.Fail(ErrorCodes.VALIDATION_ERROR, ex.Message);
            }

            if (!string.Equals(parsed.CategoryCode, categoryCode.Value, StringComparison.Ordinal))
                return Result<CreateMaterialResponse>.Fail(ErrorCodes.VALIDATION_ERROR, "existing_code category_code mismatch.");

            var existingCodeRaw = parsed.NormalizedCode;
            var serialNoManual = parsed.SerialNo;
            var suffixChar = parsed.Suffix;

            // warning+confirm
            if (!req.ForceConfirm && serialNoManual > startSerialNo)
            {
                return Result<CreateMaterialResponse>.Ok(new CreateMaterialResponse(
                    GroupId: 0,
                    CategoryCode: categoryCode.Value,
                    SerialNo: serialNoManual,
                    Code: existingCodeRaw,
                    Suffix: suffixChar.ToString(),
                    Spec: spec.Value,
                    SpecNormalized: new SpecNormalized(SpecNormalizer.NormalizeV1(req.Description)).Value,
                    RequiresConfirmation: true,
                    WarningCode: "MANUAL_CODE_ABOVE_START",
                    Message: "当前输入编码已超过该分类自动起始值，确认该物料属于新编号区间"
                ));
            }

            // group resolution by category_id + serial_no
            var existingGroupId = await _repo.GetGroupIdByCategoryIdAndSerialNoAsync(categoryId.Value, serialNoManual, ct);

            if (suffixChar != 'A')
            {
                if (existingGroupId is null)
                    return Result<CreateMaterialResponse>.Fail(ErrorCodes.VALIDATION_ERROR, $"不存在 {CodeGenerator.GenerateItemCode(categoryCode.Value, serialNoManual, 'A')} 主料，替代料号不能创建");
            }

            int groupIdFinal;
            if (existingGroupId is not null)
            {
                groupIdFinal = existingGroupId.Value;
            }
            else
            {
                if (suffixChar != 'A')
                    return Result<CreateMaterialResponse>.Fail(ErrorCodes.VALIDATION_ERROR, "group not found.");

                try
                {
                    groupIdFinal = await _repo.InsertGroupAsync(categoryCode, serialNoManual, ct);
                }
                catch (DbConstraintViolationException ex) when (ex.Constraint == IMaterialRepository.CONSTRAINT_GROUP_CATEGORY_SERIAL)
                {
                    // manual must not retry by changing serial; treat as validation error
                    return Result<CreateMaterialResponse>.Fail(ErrorCodes.VALIDATION_ERROR, "group already exists.");
                }
            }

            // anchor presence for suffix != A
            if (suffixChar != 'A')
            {
                var baseSnap = await _repo.GetBaseItemStatusByGroupIdAsync(groupIdFinal, ct);
                if (baseSnap is null)
                    return Result<CreateMaterialResponse>.Fail(ErrorCodes.VALIDATION_ERROR, "group has no anchor item(A); please repair data.");
            }

            var item = new MaterialItem(
                code: existingCodeRaw,
                suffix: new MaterialCodingSystem.Domain.ValueObjects.Suffix(suffixChar),
                spec: spec,
                name: categoryName,
                description: req.Description,
                specNormalized: new SpecNormalized(SpecNormalizer.NormalizeV1(req.Description)),
                brand: string.IsNullOrWhiteSpace(req.Brand) ? null : req.Brand
            );

            try
            {
                await _repo.InsertItemAsync(groupIdFinal, item, ct);
            }
            catch (DbConstraintViolationException ex) when (
                ex.Constraint == IMaterialRepository.CONSTRAINT_ITEM_CODE
                || ex.Constraint == IMaterialRepository.CONSTRAINT_ITEM_GROUP_SUFFIX
                || ex.Constraint == IMaterialRepository.CONSTRAINT_ITEM_CATEGORY_SPEC)
            {
                return Result<CreateMaterialResponse>.Fail(ErrorCodes.VALIDATION_ERROR, "constraint violation.");
            }

            var ok2 = new CreateMaterialResponse(
                GroupId: groupIdFinal,
                CategoryCode: categoryCode.Value,
                SerialNo: serialNoManual,
                Code: existingCodeRaw,
                Suffix: suffixChar.ToString(),
                Spec: spec.Value,
                SpecNormalized: item.SpecNormalized.Value
            );

            if (!string.IsNullOrWhiteSpace(req.RequestId))
            {
                try
                {
                    await _repo.InsertCreateMaterialSuccessLogAsync(req.RequestId!, ok2, ct);
                }
                catch (DbConstraintViolationException)
                {
                    var existing = await _repo.GetCreateMaterialSuccessByRequestIdAsync(req.RequestId!, ct);
                    if (existing is not null)
                        return Result<CreateMaterialResponse>.Ok(existing);
                }
            }

            return Result<CreateMaterialResponse>.Ok(ok2);
        }, ct, retryConstraint: IMaterialRepository.CONSTRAINT_GROUP_CATEGORY_SERIAL);

    public Task<Result<CreateReplacementResponse>> CreateReplacement(CreateReplacementRequest req, CancellationToken ct = default)
        => ExecuteWithRetry(async () =>
        {
            var validationErrors = new Dictionary<string, string>();
            if (string.IsNullOrWhiteSpace(req.Description))
            {
                validationErrors["description"] = "规格描述不能为空。";
            }

            if (string.IsNullOrWhiteSpace(req.Brand))
            {
                validationErrors["brand"] = "品牌不能为空。";
            }

            if (validationErrors.Count > 0)
            {
                return Result<CreateReplacementResponse>.Fail(
                    ErrorCodes.VALIDATION_ERROR,
                    "validation error.",
                    validationErrors);
            }

            Spec spec;
            try
            {
                spec = new Spec(req.Spec);
            }
            catch (DomainException ex) when (ex.Code == "VALIDATION_ERROR")
            {
                return Result<CreateReplacementResponse>.Fail(
                    ErrorCodes.VALIDATION_ERROR,
                    ex.Message,
                    new Dictionary<string, string> { ["spec"] = ex.Message });
            }

            var snap = await _repo.GetGroupSnapshotAsync(req.GroupId, ct);
            if (snap is null)
            {
                return Result<CreateReplacementResponse>.Fail(ErrorCodes.NOT_FOUND, "group not found.");
            }

            var baseSnap = await _repo.GetBaseItemStatusByGroupIdAsync(req.GroupId, ct);
            if (baseSnap is not null && baseSnap.Status == 0)
            {
                return Result<CreateReplacementResponse>.Fail(ErrorCodes.ANCHOR_ITEM_DEPRECATED, "anchor item deprecated.");
            }

            var categoryName = await _repo.GetCategoryNameByCodeAsync(snap.CategoryCode, ct);
            if (string.IsNullOrWhiteSpace(categoryName))
            {
                return Result<CreateReplacementResponse>.Fail(ErrorCodes.CATEGORY_NOT_FOUND, "category_code not found.");
            }

            var specExists = await _repo.SpecExistsAsync(snap.CategoryCode, spec, ct);
            if (specExists)
            {
                return Result<CreateReplacementResponse>.Fail(
                    ErrorCodes.SPEC_DUPLICATE,
                    "spec duplicate.",
                    new Dictionary<string, string> { ["spec"] = "规格号已存在。" });
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
            // name 写入规则：从分类名快照注入（不来自用户输入）
            item = new MaterialItem(
                code: item.Code,
                suffix: item.Suffix,
                spec: item.Spec,
                name: categoryName,
                description: item.Description,
                specNormalized: item.SpecNormalized,
                brand: item.Brand
            );

            try
            {
                await _repo.InsertItemAsync(req.GroupId, item, ct);
            }
            catch (DbConstraintViolationException ex) when (ex.Constraint == IMaterialRepository.CONSTRAINT_ITEM_GROUP_SUFFIX)
            {
                throw;
            }
            catch (DbConstraintViolationException ex) when (ex.Constraint == IMaterialRepository.CONSTRAINT_ITEM_CATEGORY_SPEC)
            {
                return Result<CreateReplacementResponse>.Fail(ErrorCodes.SPEC_DUPLICATE, "spec duplicate.");
            }
            catch (DbConstraintViolationException ex) when (ex.Constraint == IMaterialRepository.CONSTRAINT_ITEM_CODE)
            {
                return Result<CreateReplacementResponse>.Fail(ErrorCodes.CODE_CONFLICT_RETRY, "code conflict.");
            }
            catch (DbConstraintViolationException)
            {
                return Result<CreateReplacementResponse>.Fail(ErrorCodes.INTERNAL_ERROR, "constraint violation on insert item.");
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

    public Task<Result<CreateReplacementResponse>> CreateReplacementByCode(CreateReplacementByCodeRequest req, CancellationToken ct = default)
        => ExecuteWithRetry(async () =>
        {
            var validationErrors = new Dictionary<string, string>();
            if (string.IsNullOrWhiteSpace(req.BaseMaterialCode))
            {
                validationErrors["base_material_code"] = "基准物料编码不能为空。";
            }

            if (string.IsNullOrWhiteSpace(req.Description))
            {
                validationErrors["description"] = "规格描述不能为空。";
            }

            if (string.IsNullOrWhiteSpace(req.Brand))
            {
                validationErrors["brand"] = "品牌不能为空。";
            }

            if (validationErrors.Count > 0)
            {
                return Result<CreateReplacementResponse>.Fail(
                    ErrorCodes.VALIDATION_ERROR,
                    "validation error.",
                    validationErrors);
            }

            Spec spec;
            try
            {
                spec = new Spec(req.Spec);
            }
            catch (DomainException ex) when (ex.Code == "VALIDATION_ERROR")
            {
                return Result<CreateReplacementResponse>.Fail(
                    ErrorCodes.VALIDATION_ERROR,
                    ex.Message,
                    new Dictionary<string, string> { ["spec"] = ex.Message });
            }

            // 1) base code 是否存在
            var baseSnap = await _repo.GetItemStatusByCodeAsync(req.BaseMaterialCode.Trim(), ct);
            if (baseSnap is null)
            {
                return Result<CreateReplacementResponse>.Fail(ErrorCodes.NOT_FOUND, "base item not found.");
            }

            // 2) base status == 1
            if (baseSnap.Status == 0)
            {
                return Result<CreateReplacementResponse>.Fail(ErrorCodes.ANCHOR_ITEM_DEPRECATED, "anchor item deprecated.");
            }

            // 3) 解析 group
            var groupId = await _repo.GetGroupIdByItemCodeAsync(req.BaseMaterialCode.Trim(), ct);
            if (groupId is null)
            {
                return Result<CreateReplacementResponse>.Fail(ErrorCodes.NOT_FOUND, "group not found.");
            }

            // 4) category 是否存在（由 group snapshot 的 category_code 反查 name；缺失即 CATEGORY_NOT_FOUND）
            var snap = await _repo.GetGroupSnapshotAsync(groupId.Value, ct);
            if (snap is null)
            {
                return Result<CreateReplacementResponse>.Fail(ErrorCodes.NOT_FOUND, "group not found.");
            }

            var categoryName = await _repo.GetCategoryNameByCodeAsync(snap.CategoryCode, ct);
            if (string.IsNullOrWhiteSpace(categoryName))
            {
                return Result<CreateReplacementResponse>.Fail(ErrorCodes.CATEGORY_NOT_FOUND, "category_code not found.");
            }

            // 5) 规格唯一性
            var specExists = await _repo.SpecExistsAsync(snap.CategoryCode, spec, ct);
            if (specExists)
            {
                return Result<CreateReplacementResponse>.Fail(
                    ErrorCodes.SPEC_DUPLICATE,
                    "spec duplicate.",
                    new Dictionary<string, string> { ["spec"] = "规格号已存在。" });
            }

            // 6) 才允许进入 suffix 分配（连续性/overflow）
            try
            {
                _ = SuffixAllocator.AllocateNextSuffix(snap.ExistingSuffixes);
            }
            catch (DomainException ex) when (ex.Code is "SUFFIX_SEQUENCE_BROKEN" or "SUFFIX_OVERFLOW")
            {
                return Result<CreateReplacementResponse>.Fail(ex.Code, ex.Message);
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

            var item = group.AddReplacement(spec, categoryName, req.Description, req.Brand);

            try
            {
                await _repo.InsertItemAsync(groupId.Value, item, ct);
            }
            catch (DbConstraintViolationException ex) when (ex.Constraint == IMaterialRepository.CONSTRAINT_ITEM_GROUP_SUFFIX)
            {
                throw;
            }
            catch (DbConstraintViolationException ex) when (ex.Constraint == IMaterialRepository.CONSTRAINT_ITEM_CATEGORY_SPEC)
            {
                return Result<CreateReplacementResponse>.Fail(ErrorCodes.SPEC_DUPLICATE, "spec duplicate.");
            }
            catch (DbConstraintViolationException ex) when (ex.Constraint == IMaterialRepository.CONSTRAINT_ITEM_CODE)
            {
                return Result<CreateReplacementResponse>.Fail(ErrorCodes.CODE_CONFLICT_RETRY, "code conflict.");
            }
            catch (DbConstraintViolationException)
            {
                return Result<CreateReplacementResponse>.Fail(ErrorCodes.INTERNAL_ERROR, "constraint violation on insert item.");
            }

            return Result<CreateReplacementResponse>.Ok(new CreateReplacementResponse(
                ItemId: 0,
                GroupId: groupId.Value,
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
                var code = retryConstraint == IMaterialRepository.CONSTRAINT_ITEM_GROUP_SUFFIX
                    ? ErrorCodes.SUFFIX_ALLOCATION_FAILED
                    : ErrorCodes.CODE_CONFLICT_RETRY;
                return Result<T>.Fail(code, "conflict retry exceeded.");
            }
        }

        var code2 = retryConstraint == IMaterialRepository.CONSTRAINT_ITEM_GROUP_SUFFIX
            ? ErrorCodes.SUFFIX_ALLOCATION_FAILED
            : ErrorCodes.CODE_CONFLICT_RETRY;
        return Result<T>.Fail(code2, "conflict retry exceeded.");
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

    /// <summary>
    /// CreateMaterial 候选收敛：仅基于 spec（规格号）模糊匹配；固定 status=1；不使用 spec_normalized。
    /// 注意：此用例仅供“新建主物料(A)”候选提示使用，不影响 SearchBySpec 行为。
    /// </summary>
    public Task<Result<PagedResult<MaterialItemSpecHit>>> SearchCandidatesBySpecOnlyAsync(
        string categoryCode,
        string keyword,
        int limit = 20,
        CancellationToken ct = default)
        => _uow.ExecuteAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(categoryCode))
            {
                return Result<PagedResult<MaterialItemSpecHit>>.Fail(ErrorCodes.VALIDATION_ERROR, "category_code is required.");
            }

            if (string.IsNullOrWhiteSpace(keyword))
            {
                return Result<PagedResult<MaterialItemSpecHit>>.Fail(ErrorCodes.VALIDATION_ERROR, "spec_keyword is required.");
            }

            var fixedLimit = Math.Min(limit <= 0 ? 20 : limit, 20);
            return Result<PagedResult<MaterialItemSpecHit>>.Ok(
                await _repo.SearchCandidatesBySpecOnlyAsync(categoryCode.Trim(), keyword.Trim(), fixedLimit, ct));
        }, ct);

    public Task<Result<PagedResult<MaterialItemSpecHit>>> SearchBySpecAllAsync(
        string keyword,
        bool includeDeprecated,
        int limit = 20,
        CancellationToken ct = default
    )
        => _uow.ExecuteAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return Result<PagedResult<MaterialItemSpecHit>>.Fail(ErrorCodes.VALIDATION_ERROR, "spec_keyword is required.");
            }

            var fixedLimit = Math.Min(limit <= 0 ? 20 : limit, 20);
            return Result<PagedResult<MaterialItemSpecHit>>.Ok(
                await _repo.SearchBySpecAllAsync(keyword.Trim(), includeDeprecated, fixedLimit, ct));
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
            // PRD V1.3：Sheet1=全量（含废弃）；分类 Sheet 同样包含全部数据
            var rows = await _repo.ListAllItemsForExportAsync(ct);
            try
            {
                await _excelExporter.WriteAsync(filePath, rows, ct);
            }
            catch (IOException ex) when (IsExportTargetFileInUse(ex))
            {
                return Result<ExportMaterialsResponse>.Fail(
                    ErrorCodes.EXPORT_FILE_IN_USE,
                    "export target file is in use and cannot be overwritten");
            }

            // UI 文案：{2} 个分类 Sheet（不包含 Sheet1；无数据时也为 0）
            var sheetCount = rows.Select(r => r.CategoryCode).Distinct().Count();
            return Result<ExportMaterialsResponse>.Ok(new ExportMaterialsResponse(
                FilePath: filePath,
                RowCount: rows.Count,
                SheetCount: sheetCount));
        }, ct);
    }

    /// <summary>目标文件被其他进程占用（如 Excel 已打开），ClosedXML 保存时会失败。</summary>
    private static bool IsExportTargetFileInUse(IOException ex)
    {
        // Win32 ERROR_SHARING_VIOLATION → HRESULT 0x80070020（与 FileSystem.DeleteFile 一致）
        const int sharingViolationHResult = unchecked((int)0x80070020);
        if (ex.HResult == sharingViolationHResult)
            return true;
        // 兜底：英文/部分本地化消息
        return ex.Message.Contains("another process", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("另一个程序", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("正由另一进程", StringComparison.OrdinalIgnoreCase);
    }
}

