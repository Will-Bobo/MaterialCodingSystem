using MaterialCodingSystem.Domain.Services;

namespace MaterialCodingSystem.Tests.Domain;

public class CodeGeneratorTests
{
    [Theory]
    [InlineData("ZDA", 1, 'A', "ZDA0000001A")]
    [InlineData("ZDB", 42, 'B', "ZDB0000042B")]
    [InlineData("ZDC", 9999999, 'Z', "ZDC9999999Z")]
    public void GenerateItemCode_PadsSerialTo7Digits(string categoryCode, int serialNo, char suffix, string expected)
    {
        var actual = CodeGenerator.GenerateItemCode(categoryCode, serialNo, suffix);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void GenerateItemCode_EmptyCategory_ThrowsValidationError()
    {
        var ex = Assert.Throws<DomainException>(() => CodeGenerator.GenerateItemCode(" ", 1, 'A'));
        Assert.Equal("VALIDATION_ERROR", ex.Code);
    }

    [Fact]
    public void GenerateItemCode_NonPositiveSerial_ThrowsValidationError()
    {
        var ex = Assert.Throws<DomainException>(() => CodeGenerator.GenerateItemCode("ZDA", 0, 'A'));
        Assert.Equal("VALIDATION_ERROR", ex.Code);
    }

    [Theory]
    [InlineData('@')]
    [InlineData('a')]
    [InlineData('1')]
    public void GenerateItemCode_InvalidSuffix_ThrowsValidationError(char suffix)
    {
        var ex = Assert.Throws<DomainException>(() => CodeGenerator.GenerateItemCode("ZDA", 1, suffix));
        Assert.Equal("VALIDATION_ERROR", ex.Code);
    }
}

