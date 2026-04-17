using MaterialCodingSystem.Application;
using MaterialCodingSystem.Application.Contracts;
using MaterialCodingSystem.Domain.ValueObjects;

namespace MaterialCodingSystem.Tests.Application;

public class CreateMaterialItemManualTests
{
    [Theory]
    [InlineData("ELC0000123A", "A")]
    [InlineData("ELC0000123B", "B")]
    [InlineData("ELC0000123Z", "Z")]
    public async Task CreateManual_ValidCodes_Succeeds(string code, string suffix)
    {
        var repo = new FakeMaterialRepository
        {
            CategoryExists = true,
            CategoryName = "电子料",
            CategoryId = 7,
            SpecExists = false,
            GroupIdByCategoryAndSerialNo = 99
        };
        var app = new MaterialApplicationService(new NoopUnitOfWork(), repo);

        var res = await app.CreateMaterialItemManual(new CreateMaterialItemManualRequest(
            CategoryCode: "ELC",
            Code: code,
            Spec: "S1",
            Name: "ignored",
            DisplayName: null,
            Description: " 10uF  16V 0603 ",
            Brand: "B"
        ));

        Assert.True(res.IsSuccess);
        Assert.Equal(code, res.Data!.Code);
        Assert.Equal(suffix, res.Data.Suffix);
        Assert.Equal(123, res.Data.SerialNo);
        Assert.Equal("ELC", res.Data.CategoryCode);
        Assert.Equal("10UF 16V 0603", res.Data.SpecNormalized);
        Assert.Equal(1, repo.InsertItemCalled);
    }

    [Fact]
    public async Task CreateManual_SerialZero_ReturnsCodeFormatInvalid()
    {
        var app = new MaterialApplicationService(new NoopUnitOfWork(), new FakeMaterialRepository());

        var res = await app.CreateMaterialItemManual(new CreateMaterialItemManualRequest(
            CategoryCode: "ELC",
            Code: "ELC0000000A",
            Spec: "S1",
            Name: "n",
            DisplayName: null,
            Description: "d",
            Brand: "b"
        ));

        Assert.False(res.IsSuccess);
        Assert.Equal(ErrorCodes.CODE_FORMAT_INVALID, res.Error!.Code);
    }

    [Fact]
    public async Task CreateManual_CategoryMismatch_ReturnsCategoryMismatch()
    {
        var app = new MaterialApplicationService(new NoopUnitOfWork(), new FakeMaterialRepository());

        var res = await app.CreateMaterialItemManual(new CreateMaterialItemManualRequest(
            CategoryCode: "ELC",
            Code: "ZDA0000123A",
            Spec: "S1",
            Name: "n",
            DisplayName: null,
            Description: "d",
            Brand: "b"
        ));

        Assert.False(res.IsSuccess);
        Assert.Equal(ErrorCodes.CATEGORY_MISMATCH, res.Error!.Code);
    }

    [Fact]
    public async Task CreateManual_WhenCodeDuplicate_ReturnsCodeDuplicate()
    {
        var repo = new FakeMaterialRepository
        {
            CategoryExists = true,
            CategoryName = "电子料",
            CategoryId = 1,
            SpecExists = false,
            GroupIdByCategoryAndSerialNo = 1,
            FailItemInsertWithCodeConflictTimes = 1
        };
        var app = new MaterialApplicationService(new NoopUnitOfWork(), repo);

        var res = await app.CreateMaterialItemManual(new CreateMaterialItemManualRequest(
            CategoryCode: "ELC",
            Code: "ELC0000123A",
            Spec: "S1",
            Name: "n",
            DisplayName: null,
            Description: "d",
            Brand: "b"
        ));

        Assert.False(res.IsSuccess);
        Assert.Equal(ErrorCodes.CODE_DUPLICATE, res.Error!.Code);
    }

    [Fact]
    public async Task CreateManual_WhenGroupSuffixDuplicate_ReturnsCodeDuplicate()
    {
        var repo = new FakeMaterialRepository
        {
            CategoryExists = true,
            CategoryName = "电子料",
            CategoryId = 1,
            SpecExists = false,
            GroupIdByCategoryAndSerialNo = 1,
            FailItemInsertWithSuffixConflictTimes = 1
        };
        var app = new MaterialApplicationService(new NoopUnitOfWork(), repo);

        var res = await app.CreateMaterialItemManual(new CreateMaterialItemManualRequest(
            CategoryCode: "ELC",
            Code: "ELC0000123B",
            Spec: "S1",
            Name: "n",
            DisplayName: null,
            Description: "d",
            Brand: "b"
        ));

        Assert.False(res.IsSuccess);
        Assert.Equal(ErrorCodes.CODE_DUPLICATE, res.Error!.Code);
    }

    [Fact]
    public async Task CreateManual_WhenGroupInsertSerialConflict_RequeriesAndContinues()
    {
        var repo = new FakeMaterialRepository
        {
            CategoryExists = true,
            CategoryName = "电子料",
            CategoryId = 7,
            SpecExists = false,
            GroupIdByCategoryAndSerialNo = null,
            FailGroupInsertWithSerialConflictTimes = 1
        };
        var app = new MaterialApplicationService(new NoopUnitOfWork(), repo);

        // 第一次查不到 group；插入冲突；第二次查询应能复用
        repo.GroupIdByCategoryAndSerialNo = 101;

        var res = await app.CreateMaterialItemManual(new CreateMaterialItemManualRequest(
            CategoryCode: "ELC",
            Code: "ELC0000123A",
            Spec: "S1",
            Name: "n",
            DisplayName: null,
            Description: "d",
            Brand: "b"
        ));

        Assert.True(res.IsSuccess);
        Assert.Equal(101, res.Data!.GroupId);
    }

    [Fact]
    public async Task CreateManual_WhenSpecDuplicate_ReturnsSpecDuplicate()
    {
        var repo = new FakeMaterialRepository
        {
            CategoryExists = true,
            CategoryName = "电子料",
            CategoryId = 1,
            SpecExists = true
        };
        var app = new MaterialApplicationService(new NoopUnitOfWork(), repo);

        var res = await app.CreateMaterialItemManual(new CreateMaterialItemManualRequest(
            CategoryCode: "ELC",
            Code: "ELC0000123A",
            Spec: "S1",
            Name: "n",
            DisplayName: null,
            Description: "d",
            Brand: "b"
        ));

        Assert.False(res.IsSuccess);
        Assert.Equal(ErrorCodes.SPEC_DUPLICATE, res.Error!.Code);
    }
}

