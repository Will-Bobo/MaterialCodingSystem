using MaterialCodingSystem.Application;
using MaterialCodingSystem.Application.Contracts;
using MaterialCodingSystem.Application.Interfaces;
using MaterialCodingSystem.Domain.ValueObjects;

namespace MaterialCodingSystem.Tests.Application;

public class CreateMaterialItemATests
{
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
    public async Task CreateA_WhenCategoryNotFound_ReturnsValidationError()
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
        Assert.Equal(ErrorCodes.VALIDATION_ERROR, res.Error!.Code);
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
        var repo = new FakeMaterialRepository { CategoryExists = true, SpecExists = false, MaxSerialNo = 0 };
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

