using MaterialCodingSystem.Domain.Services;

namespace MaterialCodingSystem.Tests.Domain;

public class SuffixAllocatorTests
{
    [Fact]
    public void AllocateNextSuffix_ContinuousSequence_ReturnsNext()
    {
        var next = SuffixAllocator.AllocateNextSuffix(new[] { 'A', 'B', 'C' });
        Assert.Equal('D', next);
    }

    [Fact]
    public void AllocateNextSuffix_Empty_Throws()
    {
        var ex = Assert.Throws<DomainException>(() => SuffixAllocator.AllocateNextSuffix(Array.Empty<char>()));
        Assert.Equal("SUFFIX_SEQUENCE_BROKEN", ex.Code);
    }

    [Fact]
    public void AllocateNextSuffix_HoleExists_ThrowsSequenceBroken()
    {
        var ex = Assert.Throws<DomainException>(() => SuffixAllocator.AllocateNextSuffix(new[] { 'A', 'C' }));
        Assert.Equal("SUFFIX_SEQUENCE_BROKEN", ex.Code);
    }

    [Fact]
    public void AllocateNextSuffix_Overflow_Throws()
    {
        var all = Enumerable.Range('A', 26).Select(x => (char)x).ToArray();
        var ex = Assert.Throws<DomainException>(() => SuffixAllocator.AllocateNextSuffix(all));
        Assert.Equal("SUFFIX_OVERFLOW", ex.Code);
    }

    [Fact]
    public void AllocateNextSuffix_NotStartingWithA_ThrowsSequenceBroken()
    {
        var ex = Assert.Throws<DomainException>(() => SuffixAllocator.AllocateNextSuffix(new[] { 'B', 'C' }));
        Assert.Equal("SUFFIX_SEQUENCE_BROKEN", ex.Code);
    }

    [Fact]
    public void AllocateNextSuffix_SingleA_ReturnsB()
    {
        Assert.Equal('B', SuffixAllocator.AllocateNextSuffix(new[] { 'A' }));
    }

    [Fact]
    public void AllocateNextSuffix_DuplicateEntries_BreaksContinuity_ThrowsSequenceBroken()
    {
        var ex = Assert.Throws<DomainException>(() => SuffixAllocator.AllocateNextSuffix(new[] { 'A', 'A' }));
        Assert.Equal("SUFFIX_SEQUENCE_BROKEN", ex.Code);
    }
}

