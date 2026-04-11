namespace MaterialCodingSystem.Domain.Services;

public static class SuffixAllocator
{
    public static char AllocateNextSuffix(IReadOnlyCollection<char> existingSuffixes)
    {
        if (existingSuffixes is null || existingSuffixes.Count == 0)
        {
            throw new DomainException("SUFFIX_SEQUENCE_BROKEN", "Suffix sequence is empty or missing A.");
        }

        var min = existingSuffixes.Min();
        var max = existingSuffixes.Max();
        var count = existingSuffixes.Count;

        if (min != 'A')
        {
            throw new DomainException("SUFFIX_SEQUENCE_BROKEN", "Suffix sequence must start from A.");
        }

        // 连续性判定：max - 'A' + 1 == count （PRD 口径）
        var expectedCount = (max - 'A') + 1;
        if (expectedCount != count)
        {
            throw new DomainException("SUFFIX_SEQUENCE_BROKEN", "Suffix sequence is not continuous.");
        }

        if (max == 'Z')
        {
            throw new DomainException("SUFFIX_OVERFLOW", "Suffix overflow.");
        }

        return (char)(max + 1);
    }
}

