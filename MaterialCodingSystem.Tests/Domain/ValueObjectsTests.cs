using MaterialCodingSystem.Domain.Services;
using MaterialCodingSystem.Domain.ValueObjects;

namespace MaterialCodingSystem.Tests.Domain;

public class ValueObjectsTests
{
    [Fact]
    public void CategoryCode_Empty_Throws()
    {
        var ex = Assert.Throws<DomainException>(() => new CategoryCode(" "));
        Assert.Equal("VALIDATION_ERROR", ex.Code);
    }

    [Fact]
    public void Spec_Empty_Throws()
    {
        var ex = Assert.Throws<DomainException>(() => new Spec(""));
        Assert.Equal("VALIDATION_ERROR", ex.Code);
    }

    [Theory]
    [InlineData('A')]
    [InlineData('Z')]
    public void Suffix_Allows_A_To_Z(char s)
    {
        var suffix = new Suffix(s);
        Assert.Equal(s, suffix.Value);
    }

    [Theory]
    [InlineData('@')]
    [InlineData('[')]
    [InlineData('a')]
    public void Suffix_Rejects_NonAtoZ(char s)
    {
        var ex = Assert.Throws<DomainException>(() => new Suffix(s));
        Assert.Equal("VALIDATION_ERROR", ex.Code);
    }
}

