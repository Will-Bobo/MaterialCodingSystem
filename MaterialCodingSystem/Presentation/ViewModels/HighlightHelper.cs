namespace MaterialCodingSystem.Presentation.ViewModels;

public static class HighlightHelper
{
    public sealed record Result(string Prefix, string Match, string Suffix, bool HasMatch);

    public static Result Build(string? keyword, string? text)
    {
        var t = text ?? "";
        var k = keyword?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(k) || string.IsNullOrWhiteSpace(t))
            return new Result(Prefix: t, Match: "", Suffix: "", HasMatch: false);

        if (k.Length > t.Length)
            return new Result(Prefix: t, Match: "", Suffix: "", HasMatch: false);

        var index = t.IndexOf(k, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
            return new Result(Prefix: t, Match: "", Suffix: "", HasMatch: false);

        var prefix = t.Substring(0, index);
        var match = t.Substring(index, k.Length);
        var suffix = t.Substring(index + k.Length);
        return new Result(Prefix: prefix, Match: match, Suffix: suffix, HasMatch: true);
    }
}

