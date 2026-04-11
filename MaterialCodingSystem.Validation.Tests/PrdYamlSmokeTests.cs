using MaterialCodingSystem.Validation.core;
using MaterialCodingSystem.Validation.runner;
using MaterialCodingSystem.Validation.actions;

namespace MaterialCodingSystem.Validation.Tests;

public class PrdYamlSmokeTests
{
    [Fact]
    public void Parser_Should_Not_Lose_Cases_Due_To_DuplicateKey()
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "MaterialCodingSystem.Validation", "specs", "PRD_V1.yaml"
        ));

        var cases = new YamlExecutionPlanParser().ParseFile(path);
        Assert.Contains(cases, x => x.CaseId == "CREATE_A_SUCCESS_001");
    }

    [Fact]
    public void Runner_Can_Run_Normalize_Spec_Case()
    {
        var yaml = """
                   spec_version: v1
                   cases:
                     - id: SPEC_NORMALIZE_001
                       title: description → spec_normalized 规则
                       type: invariant
                       given:
                         input:
                           description: " 10uF   16V   0603  "
                       when:
                         action: normalize_spec
                       then:
                         output:
                           spec_normalized: "10UF 16V 0603"
                       replay: true
                       deterministic: true
                   """;

        var (caseId, plan) = Assert.Single(new YamlExecutionPlanParser().ParseYaml(yaml));
        Assert.Equal("SPEC_NORMALIZE_001", caseId);

        var dispatcher = new ActionDispatcher(new Dictionary<string, Func<Context, object>>
        {
            ["normalize_spec"] = MaterialCodingSystem.Validation.actions.PrdActions.NormalizeSpec
        });

        var r = new ValidationRunner(dispatcher, new AssertionEngine()).Run(plan);
        Assert.True(r.Passed, r.Reason);
    }

    [Fact]
    public void Runner_Can_Run_CreateA_Success_With_DbExists_Assertion()
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "MaterialCodingSystem.Validation", "specs", "PRD_V1.yaml"
        ));

        var cases = new YamlExecutionPlanParser().ParseFile(path);
        var (_, plan) = Assert.Single(cases, x => x.CaseId == "CREATE_A_SUCCESS_001");

        var dispatcher = new ActionDispatcher(new Dictionary<string, Func<Context, object>>
        {
            ["create_material_A"] = PrdActions.CreateMaterialA
        });

        var r = new ValidationRunner(dispatcher, new AssertionEngine()).Run(plan);
        Assert.True(r.Passed, r.Reason);
    }

    [Fact]
    public void PrdV1_Yaml_All_Cases_Pass()
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "MaterialCodingSystem.Validation", "specs", "PRD_V1.yaml"
        ));

        var cases = new YamlExecutionPlanParser().ParseFile(path);
        var dispatcher = new ActionDispatcher(new Dictionary<string, Func<Context, object>>
        {
            ["echo"] = BuiltInActions.Echo,
            ["normalize_spec"] = PrdActions.NormalizeSpec,
            ["create_material_A"] = PrdActions.CreateMaterialA,
            ["create_material_replacement"] = PrdActions.CreateMaterialReplacement,
            ["generate_material_code"] = PrdActions.GenerateMaterialCode,
            ["format_serial"] = PrdActions.FormatSerial,
            ["update_status"] = PrdActions.UpdateStatus,
            ["update_group"] = PrdActions.UpdateGroup,
            ["allocate_group_serial"] = PrdActions.AllocateGroupSerial,
            ["create_material_A_batch"] = PrdActions.CreateMaterialABatch,
            ["create_material_A_concurrent"] = PrdActions.CreateMaterialAConcurrent,
        });

        var runner = new ValidationRunner(dispatcher, new AssertionEngine());
        var failures = new List<string>();
        foreach (var (caseId, plan) in cases)
        {
            var r = runner.Run(plan);
            if (!r.Passed)
                failures.Add($"{caseId}: {r.Reason}");
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }
}

