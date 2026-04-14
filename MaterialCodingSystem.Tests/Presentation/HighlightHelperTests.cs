using MaterialCodingSystem.Presentation.ViewModels;

namespace MaterialCodingSystem.Tests.Presentation;

public sealed class HighlightHelperTests
{
    [Fact]
    public void Build_WhenKeywordEmpty_ReturnsNoMatch()
    {
        var r = HighlightHelper.Build("   ", "CL10A106XXXX");
        Assert.False(r.HasMatch);
        Assert.Equal("CL10A106XXXX", r.Prefix);
        Assert.Equal("", r.Match);
        Assert.Equal("", r.Suffix);
    }

    [Fact]
    public void Build_WhenExactMatch_HighlightsAll()
    {
        var r = HighlightHelper.Build("CL10A106", "CL10A106");
        Assert.True(r.HasMatch);
        Assert.Equal("", r.Prefix);
        Assert.Equal("CL10A106", r.Match);
        Assert.Equal("", r.Suffix);
    }

    [Fact]
    public void Build_TrimKeyword_AndIgnoreCase_FindsFirstMatch()
    {
        var r = HighlightHelper.Build(" cl10a106 ", "XCL10A106KP8");
        Assert.True(r.HasMatch);
        Assert.Equal("X", r.Prefix);
        Assert.Equal("CL10A106", r.Match); // preserve original casing from text
        Assert.Equal("KP8", r.Suffix);
    }
}

