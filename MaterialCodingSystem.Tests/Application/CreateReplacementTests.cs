using MaterialCodingSystem.Application;
using MaterialCodingSystem.Application.Contracts;
using MaterialCodingSystem.Application.Interfaces;

namespace MaterialCodingSystem.Tests.Application;

public class CreateReplacementTests
{
    [Fact]
    public async Task CreateReplacement_WhenInsertItemCategorySpecConstraint_Returns_SPEC_DUPLICATE_WithoutRetry()
    {
        var uow = new CountingUnitOfWork();
        var repo = new FakeMaterialRepository
        {
            GroupExists = true,
            ExistingSuffixes = new[] { 'A' },
            GroupCategoryCode = "ZDA",
            GroupSerialNo = 1,
            SpecExists = false,
            FailItemInsertWithCategorySpecTimes = 1
        };
        var app = new MaterialApplicationService(uow, repo);

        var res = await app.CreateReplacement(new CreateReplacementRequest(
            GroupId: 1,
            Spec: "NEW",
            Name: "n2",
            Description: "d2",
            Brand: "b2"
        ));

        Assert.False(res.IsSuccess);
        Assert.Equal(ErrorCodes.SPEC_DUPLICATE, res.Error!.Code);
        Assert.Equal(1, uow.Executions);
    }

    [Fact]
    public async Task CreateReplacement_WhenNameBlank_ReturnsValidationError()
    {
        var app = new MaterialApplicationService(
            uow: new NoopUnitOfWork(),
            repo: new FakeMaterialRepository { GroupExists = true, ExistingSuffixes = new[] { 'A' } }
        );

        var res = await app.CreateReplacement(new CreateReplacementRequest(
            GroupId: 1,
            Spec: "NEW",
            Name: "\t",
            Description: "d2",
            Brand: null
        ));

        Assert.True(res.IsSuccess);
    }

    [Fact]
    public async Task CreateReplacement_WhenDescriptionBlank_ReturnsValidationError()
    {
        var app = new MaterialApplicationService(
            uow: new NoopUnitOfWork(),
            repo: new FakeMaterialRepository { GroupExists = true, ExistingSuffixes = new[] { 'A' } }
        );

        var res = await app.CreateReplacement(new CreateReplacementRequest(
            GroupId: 1,
            Spec: "NEW",
            Name: "n2",
            Description: "   ",
            Brand: null
        ));

        Assert.False(res.IsSuccess);
        Assert.Equal(ErrorCodes.VALIDATION_ERROR, res.Error!.Code);
        Assert.Equal("description is required.", res.Error.Message);
    }

    [Fact]
    public async Task CreateReplacement_WhenSpecDuplicate_ReturnsSpecDuplicate()
    {
        var repo = new FakeMaterialRepository
        {
            GroupExists = true,
            ExistingSuffixes = new[] { 'A' },
            GroupCategoryCode = "ZDA",
            GroupSerialNo = 1,
            SpecExists = true
        };
        var app = new MaterialApplicationService(uow: new NoopUnitOfWork(), repo: repo);

        var res = await app.CreateReplacement(new CreateReplacementRequest(
            GroupId: 1,
            Spec: "DUP",
            Name: "n2",
            Description: "d2",
            Brand: "b2"
        ));

        Assert.False(res.IsSuccess);
        Assert.Equal(ErrorCodes.SPEC_DUPLICATE, res.Error!.Code);
    }

    [Fact]
    public async Task CreateReplacement_WhenSuffixOverflow_ReturnsSuffixOverflow()
    {
        var all = Enumerable.Range('A', 26).Select(x => (char)x).ToArray();
        var repo = new FakeMaterialRepository
        {
            GroupExists = true,
            ExistingSuffixes = all,
            GroupCategoryCode = "ZDA",
            GroupSerialNo = 1,
            SpecExists = false
        };
        var app = new MaterialApplicationService(uow: new NoopUnitOfWork(), repo: repo);

        var res = await app.CreateReplacement(new CreateReplacementRequest(
            GroupId: 1,
            Spec: "S-Z-OVERFLOW",
            Name: "n",
            Description: "d",
            Brand: "b"
        ));

        Assert.False(res.IsSuccess);
        Assert.Equal(ErrorCodes.SUFFIX_OVERFLOW, res.Error!.Code);
    }

    [Fact]
    public async Task CreateReplacement_WhenGroupNotFound_ReturnsNotFound()
    {
        var app = new MaterialApplicationService(
            uow: new NoopUnitOfWork(),
            repo: new FakeMaterialRepository { GroupExists = false }
        );

        var res = await app.CreateReplacement(new CreateReplacementRequest(
            GroupId: 123,
            Spec: "S2",
            Name: "n2",
            Description: "d2",
            Brand: "b2"
        ));

        Assert.False(res.IsSuccess);
        Assert.Equal(ErrorCodes.NOT_FOUND, res.Error!.Code);
    }

    [Fact]
    public async Task CreateReplacement_WhenSuffixSequenceBroken_ReturnsSequenceBroken()
    {
        var repo = new FakeMaterialRepository
        {
            GroupExists = true,
            ExistingSuffixes = new[] { 'A', 'C' }, // 缺口
            GroupCategoryCode = "ZDA",
            GroupSerialNo = 1
        };
        var app = new MaterialApplicationService(uow: new NoopUnitOfWork(), repo: repo);

        var res = await app.CreateReplacement(new CreateReplacementRequest(
            GroupId: 1,
            Spec: "S2",
            Name: "n2",
            Description: "d2",
            Brand: "b2"
        ));

        Assert.False(res.IsSuccess);
        Assert.Equal(ErrorCodes.SUFFIX_SEQUENCE_BROKEN, res.Error!.Code);
    }

    [Fact]
    public async Task CreateReplacement_WhenSuffixConflict_RetriesUpTo3Times_ThenSucceeds()
    {
        var uow = new CountingUnitOfWork();
        var repo = new FakeMaterialRepository
        {
            GroupExists = true,
            ExistingSuffixes = new[] { 'A' },
            GroupCategoryCode = "ZDA",
            GroupSerialNo = 1,
            FailItemInsertWithSuffixConflictTimes = 2
        };

        var app = new MaterialApplicationService(uow: uow, repo: repo);

        var res = await app.CreateReplacement(new CreateReplacementRequest(
            GroupId: 1,
            Spec: "S2",
            Name: "n2",
            Description: "d2",
            Brand: "b2"
        ));

        Assert.True(res.IsSuccess);
        Assert.Equal(3, uow.Executions);
        Assert.Equal("B", res.Data!.Suffix);
    }

    [Fact]
    public async Task CreateReplacement_WhenSuffixConflict_Exceeds3_ReturnsSuffixAllocationFailed()
    {
        var uow = new CountingUnitOfWork();
        var repo = new FakeMaterialRepository
        {
            GroupExists = true,
            ExistingSuffixes = new[] { 'A' },
            GroupCategoryCode = "ZDA",
            GroupSerialNo = 1,
            FailItemInsertWithSuffixConflictTimes = 3
        };

        var app = new MaterialApplicationService(uow: uow, repo: repo);

        var res = await app.CreateReplacement(new CreateReplacementRequest(
            GroupId: 1,
            Spec: "S2",
            Name: "n2",
            Description: "d2",
            Brand: "b2"
        ));

        Assert.False(res.IsSuccess);
        Assert.Equal(ErrorCodes.SUFFIX_ALLOCATION_FAILED, res.Error!.Code);
        Assert.Equal(3, uow.Executions);
    }
}

