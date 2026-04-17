using System.Text;

namespace MaterialCodingSystem.Tests.Architecture;

public sealed class RuleLeakGuardTests
{
    [Fact]
    public void Domain_And_Application_Should_Not_Reference_Excel_APIs()
    {
        var repoRoot = GetRepoRoot();
        var targets = new[]
        {
            Path.Combine(repoRoot, "MaterialCodingSystem", "Domain"),
            Path.Combine(repoRoot, "MaterialCodingSystem", "Application"),
        };

        // 说明：
        // - Application 中现有导出链路会出现 ClosedXML 的“文案/注释/资源”等文本（例如提示信息），这是既有功能，
        //   本 guard 只约束“Excel API 引用/using”，不因文案提及而误伤。
        // - Domain/Application 只要出现 Excel 相关 using 或类型引用，即视为规则回流风险。
        var forbiddenSnippets = new[]
        {
            "using ClosedXML",
            "ClosedXML.",
            "IXLWorksheet",
            "using ExcelDataReader",
            "ExcelDataReader.",
            "using System.Data",
            "System.Data.DataTable",
            "DataTable ",
            "DataTable\t",
            "DataTable\r",
            "DataTable\n",
            "DataTable>",
            "DataTable,",
            "DataTable)",
            "DataTable;",
        };

        var violations = new List<string>();
        foreach (var dir in targets)
        {
            foreach (var file in Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories))
            {
                var text = File.ReadAllText(file, Encoding.UTF8);
                foreach (var token in forbiddenSnippets)
                {
                    if (text.Contains(token, StringComparison.Ordinal))
                        violations.Add($"{file} contains '{token}'");
                }
            }
        }

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    private static string GetRepoRoot()
    {
        // tests bin/Debug/net8.0... -> walk up to repo root that contains MaterialCodingSystem.sln or folder
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "MaterialCodingSystem");
            if (Directory.Exists(candidate))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("repo root not found.");
    }
}

