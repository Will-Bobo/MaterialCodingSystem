using MaterialCodingSystem.Application;
using MaterialCodingSystem.Application.Contracts;
using MaterialCodingSystem.Application.Interfaces;
using MaterialCodingSystem.Domain.ValueObjects;

namespace MaterialCodingSystem.Tests.Application;

public class CreateMaterialItemATests
{
    [Fact]
    public async Task CreateA_WhenInsertItemCategorySpecConstraint_Returns_SPEC_DUPLICATE_WithoutRetry()
    {
        var uow = new CountingUnitOfWork();
        var repo = new FakeMaterialRepository
        {
            CategoryExists = true,
            SpecExists = false,
            MaxSerialNo = 0,
            FailItemInsertWithCategorySpecTimes = 1
        };
        var app = new MaterialApplicationService(uow, repo);

        var res = await app.CreateMaterialItemA(new CreateMaterialItemARequest(
            CategoryCode: "ZDA",
            Spec: "S1",
            Name: "n",
            Description: "d",
            Brand: "b"
        ));

        Assert.False(res.IsSuccess);
        Assert.Equal(ErrorCodes.SPEC_DUPLICATE, res.Error!.Code);
        Assert.Equal(1, uow.Executions);
    }

    [Fact]
    public async Task CreateA_WhenSpecEmpty_ReturnsValidationError()
    {
        var app = new MaterialApplicationService(
            uow: new NoopUnitOfWork(),
            repo: new FakeMaterialRepository { CategoryExists = true, SpecExists = false }
        );

        var res = await app.CreateMaterialItemA(new CreateMaterialItemARequest(
            CategoryCode: "ZDA",
            Spec: "   ",
            Name: "n",
            Description: "d",
            Brand: "b"
        ));

        Assert.False(res.IsSuccess);
        Assert.Equal(ErrorCodes.VALIDATION_ERROR, res.Error!.Code);
    }

    [Fact]
    public async Task CreateA_WhenNameBlank_IsIgnored_AndStillSucceeds()
    {
        var app = new MaterialApplicationService(
            uow: new NoopUnitOfWork(),
            repo: new FakeMaterialRepository { CategoryExists = true, CategoryName = "默认分类", SpecExists = false }
        );

        var res = await app.CreateMaterialItemA(new CreateMaterialItemARequest(
            CategoryCode: "ZDA",
            Spec: "S1",
            Name: "  ",
            Description: "d",
            Brand: null
        ));

        Assert.True(res.IsSuccess);
    }

    [Fact]
    public async Task CreateA_WhenDescriptionBlank_ReturnsValidationError()
    {
        var app = new MaterialApplicationService(
            uow: new NoopUnitOfWork(),
            repo: new FakeMaterialRepository { CategoryExists = true, SpecExists = false }
        );

        var res = await app.CreateMaterialItemA(new CreateMaterialItemARequest(
            CategoryCode: "ZDA",
            Spec: "S1",
            Name: "n",
            Description: "",
            Brand: null
        ));

        Assert.False(res.IsSuccess);
        Assert.Equal(ErrorCodes.VALIDATION_ERROR, res.Error!.Code);
        Assert.Equal("description is required.", res.Error.Message);
    }

    [Fact]
    public async Task CreateA_WhenCategoryNotFound_ReturnsCategoryNotFound()
    {
        var app = new MaterialApplicationService(
            uow: new NoopUnitOfWork(),
            repo: new FakeMaterialRepository { CategoryExists = false }
        );

        var res = await app.CreateMaterialItemA(new CreateMaterialItemARequest(
            CategoryCode: "ZDA",
            Spec: "S1",
            Name: "n",
            Description: "d",
            Brand: "b"
        ));

        Assert.False(res.IsSuccess);
        Assert.Equal(ErrorCodes.CATEGORY_NOT_FOUND, res.Error!.Code);
    }

    [Fact]
    public async Task CreateA_WhenSpecDuplicate_ReturnsSpecDuplicate()
    {
        var app = new MaterialApplicationService(
            uow: new NoopUnitOfWork(),
            repo: new FakeMaterialRepository { CategoryExists = true, SpecExists = true }
        );

        var res = await app.CreateMaterialItemA(new CreateMaterialItemARequest(
            CategoryCode: "ZDA",
            Spec: "S1",
            Name: "n",
            Description: "d",
            Brand: "b"
        ));

        Assert.False(res.IsSuccess);
        Assert.Equal(ErrorCodes.SPEC_DUPLICATE, res.Error!.Code);
    }

    [Fact]
    public async Task CreateA_Success_ReturnsCodeSuffixA_AndNormalizedFromDescription()
    {
        var repo = new FakeMaterialRepository { CategoryExists = true, SpecExists = false, MaxSerialNo = 0, CategoryStartSerialNo = 1 };
        var app = new MaterialApplicationService(uow: new NoopUnitOfWork(), repo: repo);

        var res = await app.CreateMaterialItemA(new CreateMaterialItemARequest(
            CategoryCode: "ZDA",
            Spec: "CL10A106KP8NNNC",
            Name: "电容",
            Description: " 10uF  16V 0603 ",
            Brand: "SAMSUNG"
        ));

        Assert.True(res.IsSuccess);
        Assert.Equal("ZDA0000001A", res.Data!.Code);
        Assert.Equal("A", res.Data.Suffix);
        Assert.Equal("10UF 16V 0603", res.Data.SpecNormalized);
        Assert.Equal(1, repo.InsertGroupCalled);
        Assert.Equal(1, repo.InsertItemCalled);
        Assert.Equal(new CategoryCode("ZDA"), repo.LastInsertedGroupCategoryCode);
    }

    [Fact]
    public async Task CreateA_WhenStartSerialNo5000_AndNoExisting_FirstAutoIs5000()
    {
        var repo = new FakeMaterialRepository
        {
            CategoryExists = true,
            SpecExists = false,
            MaxSerialNo = 0,
            CategoryStartSerialNo = 5000
        };
        var app = new MaterialApplicationService(uow: new NoopUnitOfWork(), repo: repo);

        var res = await app.CreateMaterialItemA(new CreateMaterialItemARequest(
            CategoryCode: "ZDA",
            Spec: "S5000",
            Name: "n",
            Description: "d",
            Brand: "b"
        ));

        Assert.True(res.IsSuccess);
        Assert.Equal("ZDA0005000A", res.Data!.Code);
    }

    [Fact]
    public async Task CreateA_WhenStartSerialNo5000_AndMax5008_NextIs5009()
    {
        var repo = new FakeMaterialRepository
        {
            CategoryExists = true,
            SpecExists = false,
            MaxSerialNo = 5008,
            CategoryStartSerialNo = 5000
        };
        var app = new MaterialApplicationService(uow: new NoopUnitOfWork(), repo: repo);

        var res = await app.CreateMaterialItemA(new CreateMaterialItemARequest(
            CategoryCode: "ZDA",
            Spec: "S5009",
            Name: "n",
            Description: "d",
            Brand: "b"
        ));

        Assert.True(res.IsSuccess);
        Assert.Equal("ZDA0005009A", res.Data!.Code);
    }

    [Fact]
    public async Task ManualExistingCode_WhenAboveStartSerialNo_ReturnsRequiresConfirmation()
    {
        var repo = new FakeMaterialRepository
        {
            CategoryExists = true,
            SpecExists = false,
            CategoryStartSerialNo = 5000,
            GroupExists = false
        };
        var app = new MaterialApplicationService(uow: new NoopUnitOfWork(), repo: repo);

        var res = await app.CreateMaterial(new CreateMaterialRequest(
            CategoryCode: "ZDA",
            Spec: "S1",
            Name: "n",
            Description: "d",
            Brand: "b",
            CodeMode: CreateMaterialCodeMode.ManualExistingCode,
            ExistingCode: "ZDA0005001A",
            ForceConfirm: false
        ));

        Assert.True(res.IsSuccess);
        Assert.True(res.Data!.RequiresConfirmation);
        Assert.Equal("MANUAL_CODE_ABOVE_START", res.Data.WarningCode);
    }

    [Fact]
    public async Task ManualExistingCode_SuffixB_WhenGroupNotExists_Fails()
    {
        var repo = new FakeMaterialRepository
        {
            CategoryExists = true,
            SpecExists = false,
            CategoryStartSerialNo = 1,
            GroupExists = false
        };
        var app = new MaterialApplicationService(uow: new NoopUnitOfWork(), repo: repo);

        var res = await app.CreateMaterial(new CreateMaterialRequest(
            CategoryCode: "ZDA",
            Spec: "S1",
            Name: "n",
            Description: "d",
            Brand: "b",
            CodeMode: CreateMaterialCodeMode.ManualExistingCode,
            ExistingCode: "ZDA0000123B",
            ForceConfirm: true
        ));

        Assert.False(res.IsSuccess);
        Assert.Equal(ErrorCodes.VALIDATION_ERROR, res.Error!.Code);
    }

    [Fact]
    public async Task ManualExistingCode_WhenGroupExistsWithOnlyB_CanInsertA_WithoutCreatingGroupAgain()
    {
        var repo = new FakeMaterialRepository
        {
            CategoryExists = true,
            SpecExists = false,
            CategoryStartSerialNo = 1,
            GroupExists = true
        };
        repo.CategoryCodes.Clear();
        repo.CategoryCodes.Add("ZDA");

        var app = new MaterialApplicationService(uow: new NoopUnitOfWork(), repo: repo);

        var res = await app.CreateMaterial(new CreateMaterialRequest(
            CategoryCode: "ZDA",
            Spec: "S-A",
            Name: "n",
            Description: "d",
            Brand: "b",
            CodeMode: CreateMaterialCodeMode.ManualExistingCode,
            ExistingCode: "ZDA0000123A",
            ForceConfirm: true
        ));

        Assert.True(res.IsSuccess);
        Assert.Equal("ZDA0000123A", res.Data!.Code);
        Assert.Equal(0, repo.InsertGroupCalled);
        Assert.Equal(1, repo.InsertItemCalled);
    }

    [Fact]
    public async Task CreateMaterial_WhenSameRequestId_SubmitTwice_SecondReturnsFirstResult()
    {
        var repo = new FakeMaterialRepository
        {
            CategoryExists = true,
            SpecExists = false,
            MaxSerialNo = 0,
            CategoryStartSerialNo = 1
        };
        repo.CategoryCodes.Clear();
        repo.CategoryCodes.Add("ZDA");

        var app = new MaterialApplicationService(uow: new NoopUnitOfWork(), repo: repo);

        var req = new CreateMaterialRequest(
            CategoryCode: "ZDA",
            Spec: "S1",
            Name: "n",
            Description: "d",
            Brand: "b",
            CodeMode: CreateMaterialCodeMode.Auto,
            RequestId: "RID-1"
        );

        var r1 = await app.CreateMaterial(req);
        var r2 = await app.CreateMaterial(req);

        Assert.True(r1.IsSuccess);
        Assert.True(r2.IsSuccess);
        Assert.Equal(r1.Data!.Code, r2.Data!.Code);
    }

    [Fact]
    public async Task Parser_WhenCategoryCodesOverlap_LongestPrefixMatched()
    {
        var repo = new FakeMaterialRepository
        {
            CategoryExists = true,
            SpecExists = false,
            CategoryStartSerialNo = 1,
            GroupExists = true
        };
        repo.CategoryCodes.Clear();
        repo.CategoryCodes.AddRange(new[] { "ZD", "ZDA", "ZDAA" });

        var app = new MaterialApplicationService(uow: new NoopUnitOfWork(), repo: repo);

        var res = await app.CreateMaterial(new CreateMaterialRequest(
            CategoryCode: "ZDA",
            Spec: "S1",
            Name: "n",
            Description: "d",
            Brand: "b",
            CodeMode: CreateMaterialCodeMode.ManualExistingCode,
            ExistingCode: "ZDA0000123A",
            ForceConfirm: true
        ));

        Assert.True(res.IsSuccess);
        Assert.Equal("ZDA0000123A", res.Data!.Code);
    }

    [Fact]
    public async Task Parser_WhenNoCategoryPrefixMatched_ReturnsValidationError()
    {
        var repo = new FakeMaterialRepository
        {
            CategoryExists = true,
            SpecExists = false,
            CategoryStartSerialNo = 1,
            GroupExists = true
        };
        repo.CategoryCodes.Clear();
        repo.CategoryCodes.AddRange(new[] { "ZD", "ZDA" });

        var app = new MaterialApplicationService(uow: new NoopUnitOfWork(), repo: repo);

        var res = await app.CreateMaterial(new CreateMaterialRequest(
            CategoryCode: "ZDA",
            Spec: "S1",
            Name: "n",
            Description: "d",
            Brand: "b",
            CodeMode: CreateMaterialCodeMode.ManualExistingCode,
            ExistingCode: "XXX0000123A",
            ForceConfirm: true
        ));

        Assert.False(res.IsSuccess);
        Assert.Equal(ErrorCodes.VALIDATION_ERROR, res.Error!.Code);
    }

    [Fact]
    public async Task CreateA_WhenSerialConflict_RetriesUpTo3Times_ThenSucceeds()
    {
        var uow = new CountingUnitOfWork();
        var repo = new FakeMaterialRepository
        {
            CategoryExists = true,
            SpecExists = false,
            MaxSerialNo = 0,
            FailGroupInsertWithSerialConflictTimes = 2
        };

        var app = new MaterialApplicationService(uow: uow, repo: repo);

        var res = await app.CreateMaterialItemA(new CreateMaterialItemARequest(
            CategoryCode: "ZDA",
            Spec: "S1",
            Name: "n",
            Description: "d",
            Brand: "b"
        ));

        Assert.True(res.IsSuccess);
        Assert.Equal("ZDA0000003A", res.Data!.Code); // 1/2 冲突后成功写入 3
        Assert.Equal(3, uow.Executions);
    }

    [Fact]
    public async Task CreateA_WhenSerialConflict_Exceeds3_ReturnsCodeConflictRetry()
    {
        var uow = new CountingUnitOfWork();
        var repo = new FakeMaterialRepository
        {
            CategoryExists = true,
            SpecExists = false,
            MaxSerialNo = 0,
            FailGroupInsertWithSerialConflictTimes = 3
        };

        var app = new MaterialApplicationService(uow: uow, repo: repo);

        var res = await app.CreateMaterialItemA(new CreateMaterialItemARequest(
            CategoryCode: "ZDA",
            Spec: "S1",
            Name: "n",
            Description: "d",
            Brand: "b"
        ));

        Assert.False(res.IsSuccess);
        Assert.Equal(ErrorCodes.CODE_CONFLICT_RETRY, res.Error!.Code);
        Assert.Equal(3, uow.Executions);
    }
}

