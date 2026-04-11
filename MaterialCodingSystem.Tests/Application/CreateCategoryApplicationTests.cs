using MaterialCodingSystem.Application;
using MaterialCodingSystem.Application.Contracts;
namespace MaterialCodingSystem.Tests.Application;

public class CreateCategoryApplicationTests
{
    [Fact]
    public async Task CreateCategory_EmptyCode_ReturnsValidationError()
    {
        var app = new MaterialApplicationService(new NoopUnitOfWork(), new FakeMaterialRepository());

        var res = await app.CreateCategory(new CreateCategoryRequest(Code: " ", Name: "N"));

        Assert.False(res.IsSuccess);
        Assert.Equal(ErrorCodes.VALIDATION_ERROR, res.Error!.Code);
    }

    [Fact]
    public async Task CreateCategory_EmptyName_ReturnsValidationError()
    {
        var app = new MaterialApplicationService(new NoopUnitOfWork(), new FakeMaterialRepository());

        var res = await app.CreateCategory(new CreateCategoryRequest(Code: "ZDA", Name: "  "));

        Assert.False(res.IsSuccess);
        Assert.Equal(ErrorCodes.VALIDATION_ERROR, res.Error!.Code);
    }

    [Fact]
    public async Task CreateCategory_DuplicateCode_ReturnsCategoryCodeDuplicate()
    {
        var repo = new FakeMaterialRepository
        {
            InsertCategoryConstraintViolationMessage = "UNIQUE constraint failed: category.code"
        };
        var app = new MaterialApplicationService(new NoopUnitOfWork(), repo);

        var res = await app.CreateCategory(new CreateCategoryRequest(Code: "ZDA", Name: "电阻"));

        Assert.False(res.IsSuccess);
        Assert.Equal(ErrorCodes.CATEGORY_CODE_DUPLICATE, res.Error!.Code);
    }

    [Fact]
    public async Task CreateCategory_DuplicateName_ReturnsCategoryNameDuplicate()
    {
        var repo = new FakeMaterialRepository
        {
            InsertCategoryConstraintViolationMessage = "UNIQUE constraint failed: category.name"
        };
        var app = new MaterialApplicationService(new NoopUnitOfWork(), repo);

        var res = await app.CreateCategory(new CreateCategoryRequest(Code: "ZDB", Name: "同名"));

        Assert.False(res.IsSuccess);
        Assert.Equal(ErrorCodes.CATEGORY_NAME_DUPLICATE, res.Error!.Code);
    }

    [Fact]
    public async Task CreateCategory_Success_TrimsAndUppercasesCode()
    {
        var app = new MaterialApplicationService(new NoopUnitOfWork(), new FakeMaterialRepository());

        var res = await app.CreateCategory(new CreateCategoryRequest(Code: " zda ", Name: " 类A "));

        Assert.True(res.IsSuccess);
        Assert.Equal("ZDA", res.Data!.Code);
        Assert.Equal("类A", res.Data.Name);
    }
}
