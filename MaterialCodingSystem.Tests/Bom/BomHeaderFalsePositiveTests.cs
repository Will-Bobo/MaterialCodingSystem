using MaterialCodingSystem.Domain.Services;
using MaterialCodingSystem.Domain.Services.Models;

namespace MaterialCodingSystem.Tests.Bom;

public sealed class BomHeaderFalsePositiveTests
{
    [Fact]
    public void Parse_When_FinishedCode_Inline_Nested_PCBA_Should_HeaderMissing()
    {
        var grid = new BomGrid(new[]
        {
            new BomGridRow(1, new[] { "成品编码: PCBA:KC001" }),
            new BomGridRow(2, new[] { "PCBA版本", "V1.0" }),
        });

        var result = BomParsingRules.Parse(grid);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Failure);
        Assert.Equal(BomParsingFailureKind.HeaderMissing, result.Failure!.Kind);
    }

    [Fact]
    public void Parse_When_FinishedCode_Inline_Nested_VER_Should_HeaderMissing()
    {
        var grid = new BomGrid(new[]
        {
            new BomGridRow(1, new[] { "成品编码: VER:123" }),
            new BomGridRow(2, new[] { "PCBA版本", "V1.0" }),
        });

        var result = BomParsingRules.Parse(grid);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Failure);
        Assert.Equal(BomParsingFailureKind.HeaderMissing, result.Failure!.Kind);
    }

    [Fact]
    public void Parse_When_FinishedCode_And_Version_Split_Cells_Should_Succeed()
    {
        var grid = new BomGrid(new[]
        {
            new BomGridRow(1, new[] { "成品编码", "KC001" }),
            new BomGridRow(2, new[] { "PCBA版本", "V1.0" }),
            new BomGridRow(5, new[] { "编码", "名称", "描述", "规格", "品牌" }),
            new BomGridRow(6, new[] { "ZDA0000001A", "n1", "d1", "S1", "B1" }),
        });

        var result = BomParsingRules.Parse(grid);

        Assert.True(result.IsSuccess, result.Failure?.Kind.ToString());
        Assert.Equal("KC001", result.Success!.FinishedCode);
        Assert.Equal("V1.0", result.Success.Version);
    }

    [Fact]
    public void Parse_When_FinishedCode_Inline_Valid_And_PCBA_Version_Row_Should_Succeed()
    {
        var grid = new BomGrid(new[]
        {
            new BomGridRow(1, new[] { "成品编码: KC001" }),
            new BomGridRow(2, new[] { "PCBA版本: V1.0" }),
            new BomGridRow(5, new[] { "编码", "名称", "描述", "规格", "品牌" }),
            new BomGridRow(6, new[] { "ZDA0000001A", "n1", "d1", "S1", "B1" }),
        });

        var result = BomParsingRules.Parse(grid);

        Assert.True(result.IsSuccess, result.Failure?.Kind.ToString());
        Assert.Equal("KC001", result.Success!.FinishedCode);
        Assert.Equal("V1.0", result.Success.Version);
    }

    [Fact]
    public void Parse_When_FinishedCode_Key_In_A1_Value_In_B1_Should_Succeed()
    {
        var grid = new BomGrid(new[]
        {
            new BomGridRow(1, new[] { "成品编码", "KC001" }),
            new BomGridRow(2, new[] { "PCBA版本号", "V1.0-20260414" }),
            new BomGridRow(5, new[] { "编码", "名称", "描述", "规格", "品牌" }),
            new BomGridRow(6, new[] { "ZDA0000001A", "n1", "d1", "S1", "B1" }),
        });

        var result = BomParsingRules.Parse(grid);

        Assert.True(result.IsSuccess, result.Failure?.Kind.ToString());
        Assert.Equal("KC001", result.Success!.FinishedCode);
        Assert.Equal("V1.0-20260414", result.Success.Version);
    }

    [Theory]
    [InlineData("成品编码:KC001")]
    [InlineData("成品编码：KC001")]
    [InlineData("成品编码 : KC001")]
    public void Parse_When_FinishedCode_Inline_Tolerated_Formats_Should_Succeed(string finishedCell)
    {
        var grid = new BomGrid(new[]
        {
            new BomGridRow(1, new[] { finishedCell }),
            new BomGridRow(2, new[] { "PCBA版本", "V1.0" }),
            new BomGridRow(5, new[] { "编码", "名称", "描述", "规格", "品牌" }),
            new BomGridRow(6, new[] { "ZDA0000001A", "n1", "d1", "S1", "B1" }),
        });

        var result = BomParsingRules.Parse(grid);

        Assert.True(result.IsSuccess, result.Failure?.Kind.ToString());
        Assert.Equal("KC001", result.Success!.FinishedCode);
        Assert.Equal("V1.0", result.Success.Version);
    }
}
