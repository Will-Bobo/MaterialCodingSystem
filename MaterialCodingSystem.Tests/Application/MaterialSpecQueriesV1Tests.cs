using MaterialCodingSystem.Application;
using MaterialCodingSystem.Domain.Services;

namespace MaterialCodingSystem.Tests.Application;

/// <summary>
/// Application 入口 <see cref="MaterialSpecQueriesV1"/> 与 Domain <see cref="SpecNormalizer"/> 行为一致（基线）。
/// </summary>
public class MaterialSpecQueriesV1Tests
{
    [Theory]
    [InlineData(" 10uF  16V ", "10UF 16V")]
    [InlineData(null, "")]
    public void NormalizeDescriptionToSpecNormalized_Matches_Domain_NormalizeV1(string? input, string expected)
    {
        var viaApp = MaterialSpecQueriesV1.NormalizeDescriptionToSpecNormalized(input);
        Assert.True(viaApp.IsSuccess);
        Assert.Equal(expected, viaApp.Data);
        Assert.Equal(expected, SpecNormalizer.NormalizeV1(input));
    }
}
