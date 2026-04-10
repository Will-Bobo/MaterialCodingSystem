using System.Text.Json;

namespace MaterialCodingSystem.Validation.core;

public static class JsonUtil
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false
    };

    public static string StableStringify(object? value)
    {
        var normalized = Normalize(value);
        return JsonSerializer.Serialize(normalized, Options);
    }

    private static object? Normalize(object? v)
    {
        if (v is null) return null;

        if (v is IDictionary<string, object?> dso)
        {
            var sorted = new SortedDictionary<string, object?>();
            foreach (var (k, vv) in dso)
                sorted[k] = Normalize(vv);
            return sorted;
        }

        if (v is IDictionary<object, object> d)
        {
            var sorted = new SortedDictionary<string, object?>();
            foreach (var (k, vv) in d)
                sorted[k.ToString() ?? ""] = Normalize(vv);
            return sorted;
        }

        if (v is System.Collections.IEnumerable e && v is not string)
        {
            var list = new List<object?>();
            foreach (var item in e)
                list.Add(Normalize(item));
            return list;
        }

        return v;
    }
}
