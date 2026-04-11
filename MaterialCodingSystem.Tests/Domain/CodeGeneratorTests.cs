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
}

