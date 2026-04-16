using MaterialCodingSystem.Domain.ValueObjects;

namespace MaterialCodingSystem.Tests.Domain;

public class ManualMaterialCodeTests
{
    [Theory]
    [InlineData("ELC0000123A", "ELC", 123, 'A')]
    [InlineData("ELC0000123B", "ELC", 123, 'B')]
    [InlineData("ELC0000123Z", "ELC", 123, 'Z')]
    [InlineData(" elc0000123a ", "ELC", 123, 'A')]
    public void Parse_ValidCodes_Succeeds(string raw, string cat, int serialNo, char suffix)
    {
        var parsed = ManualMaterialCode.Parse(raw);
        Assert.Equal($"{cat}{serialNo:D7}{suffix}", parsed.NormalizedCode);
        Assert.Equal(cat, parsed.CategoryCode.Value);
        Assert.Equal(serialNo, parsed.SerialNo);
        Assert.Equal(suffix, parsed.Suffix.Value);
    }

    [Fact]
    public void Parse_SerialZero_ReturnsCodeFormatInvalid()
    {
        var ex = Assert.Throws<MaterialCodingSystem.Domain.Services.DomainException>(() =>
            ManualMaterialCode.Parse("ELC0000000A"));
        Assert.Equal("CODE_FORMAT_INVALID", ex.Code);
    }
}

