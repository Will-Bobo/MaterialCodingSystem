using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using System.Text.RegularExpressions;

namespace MaterialCodingSystem.Validation.core;

public sealed class YamlExecutionPlanParser
{
    public IReadOnlyList<(string CaseId, ExecutionPlan Plan)> ParseFile(string path)
    {
        var yaml = File.ReadAllText(path);
        return ParseYaml(yaml);
    }

    public IReadOnlyList<(string CaseId, ExecutionPlan Plan)> ParseYaml(string yaml)
    {
        yaml = MergeDuplicateTopLevelCases(yaml);

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();

        var spec = deserializer.Deserialize<ValidationSpec>(yaml) ?? new ValidationSpec();
        var list = new List<(string, ExecutionPlan)>();

        foreach (var c in spec.Cases)
        {
            var inputRaw = c.Given?.Input ?? new Dictionary<string, object?>();
            var seedRaw = c.Given?.Db;

            var assertions = new List<AssertionSpec>();

            var err = c.Then?.Error;
            if (err != null)
            {
                var shouldThrow = err.ShouldThrow ?? true; // PRD_V1 默认：出现 error 即表示应抛错
                assertions.Add(new AssertionSpec.ExpectError(shouldThrow, err.Code));
            }

            var output = c.Then?.Output;
            if (output != null)
                assertions.Add(new AssertionSpec.ExpectResultEquals(NormalizeScalars(output)));

            // PRD_V1: DB exists assertions (minimal support)
            var dbThen = c.Then?.Db;
            if (dbThen is not null)
            {
                var normalized = NormalizeScalars(dbThen) as Dictionary<string, object?>;
                if (normalized is not null)
                {
                    foreach (var (table, specObj) in normalized)
                    {
                        if (specObj is not Dictionary<string, object?> specMap) continue;
                        if (!specMap.TryGetValue("exists", out var existsObj)) continue;
                        if (existsObj is not Dictionary<string, object?> where) continue;
                        assertions.Add(new AssertionSpec.ExpectDbExists(table, where));
                    }
                }
            }

            var plan = new ExecutionPlan
            {
                ActionName = c.When?.Action ?? "",
                Input = (Dictionary<string, object?>)(NormalizeScalars(inputRaw) ?? new Dictionary<string, object?>()),
                Assertions = assertions,
                Replay = c.Replay,
                Deterministic = c.Deterministic,
                Seed = seedRaw == null
                    ? null
                    : new DbSeed { Data = (Dictionary<string, object?>)(NormalizeScalars(seedRaw) ?? new Dictionary<string, object?>()) }
            };

            list.Add((c.Id, plan));
        }

        return list;
    }

    private static string MergeDuplicateTopLevelCases(string yaml)
    {
        // Some specs contain duplicated top-level `cases:` keys. YAML parser keeps the last one,
        // so we merge them by stitching case lists into a single `cases:` block.
        var matches = Regex.Matches(yaml, @"(?m)^\s*cases:\s*$");
        if (matches.Count <= 1) return yaml;

        // Keep everything up to (but not including) the second "cases:" line,
        // then append the content after that second "cases:" line.
        var secondIdx = matches[1].Index;
        var secondLineLength = matches[1].Length;

        var head = yaml.Substring(0, secondIdx);
        var tail = yaml.Substring(secondIdx + secondLineLength);

        return head + tail;
    }

    private static object? NormalizeScalars(object? v)
    {
        if (v is null) return null;

        if (v is IDictionary<string, object?> dso)
        {
            var r = new Dictionary<string, object?>();
            foreach (var (k, vv) in dso)
                r[k] = NormalizeScalars(vv);
            return r;
        }

        if (v is IDictionary<object, object> d)
        {
            var r = new Dictionary<string, object?>();
            foreach (var (k, vv) in d)
                r[k.ToString() ?? ""] = NormalizeScalars(vv);
            return r;
        }

        if (v is System.Collections.IList list)
        {
            var r = new List<object?>();
            foreach (var item in list)
                r.Add(NormalizeScalars(item));
            return r;
        }

        if (v is string s)
        {
            if (bool.TryParse(s, out var b)) return b;

            // Avoid converting strings with leading zeros like "001" (unless exactly "0")
            var hasLeadingZero = s.Length > 1 && s[0] == '0' && char.IsDigit(s[1]);
            if (!hasLeadingZero && int.TryParse(s, out var i)) return i;
            if (!hasLeadingZero && long.TryParse(s, out var l)) return l;
            if (!hasLeadingZero && decimal.TryParse(s, out var m)) return m;

            return s;
        }

        return v;
    }
}
