using MaterialCodingSystem.Domain.Services;

namespace MaterialCodingSystem.Tests.Domain;

public class SpecNormalizerTests
{
    [Theory]
    [InlineData(" 10uF  16V ", "10UF 16V")]
    [InlineData("\t10uF\t16V\t", "10UF 16V")]
    [InlineData("abc", "ABC")]
    [InlineData("  a   b   c  ", "A B C")]
    public void Normalize_V1_OnlyTrimCollapseUppercase(string input, string expected)
    {
        var actual = SpecNormalizer.NormalizeV1(input);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Normalize_V1_NullInput_ReturnsEmptyString()
    {
        var actual = SpecNormalizer.NormalizeV1(null);
        Assert.Equal(string.Empty, actual);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("\r\na\r\n", "A")]
    [InlineData("a\u00A0b", "A B")] // NBSP treated as whitespace → single space
    public void Normalize_V1_EdgeCases(string input, string expected)
    {
        Assert.Equal(expected, SpecNormalizer.NormalizeV1(input));
    }

    [Fact]
    public void Normalize_V1_OnlyUnicodeLetters_Uppercases()
    {
        Assert.Equal("ÄBC", SpecNormalizer.NormalizeV1(" äbc "));
    }
}
