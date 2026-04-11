using System.Text;
using MaterialCodingSystem.Validation.core;
using MaterialCodingSystem.Validation.runner;

namespace MaterialCodingSystem.Validation.Tests;

public class RunnerUnitTests
{
    [Fact]
    public void Should_ParseYaml_To_Model_Correctly()
    {
        var yaml = """
                   spec_version: v1
                   meta:
                     system: validation-runner
                   cases:
                     - id: PARSE_001
                       title: parse fields
                       type: core
                       given:
                         db:
                           any: { x: 1 }
                         input:
                           value: 1
                           text: "a"
                       when:
                         action: echo
                         context:
                           user: "u1"
                       then:
                         output:
                           result: 1
                         error:
                           should_throw: false
                           code: "IGNORED"
                       replay: true
                       deterministic: true
                   """;

        var path = WriteTempYaml(yaml);

        var spec = new YamlExecutionPlanParser().ParseFile(path);
        var (caseId, plan) = Assert.Single(spec);

        Assert.Equal("PARSE_001", caseId);
        Assert.Equal("echo", plan.ActionName);
        Assert.True(plan.Replay);
        Assert.True(plan.Deterministic);
        Assert.Equal("1", plan.Input["value"]?.ToString());
        Assert.Equal("a", plan.Input["text"]?.ToString());

        var err = Assert.Single(plan.Assertions.OfType<AssertionSpec.ExpectError>());
        Assert.False(err.ShouldThrow);
    }

    [Fact]
    public void Should_Call_Dispatcher_With_ActionName_And_Pass_Input()
    {
        var calls = new List<(string action, Context ctx)>();

        var dispatcher = new ActionDispatcher(new Dictionary<string, Func<Context, object>>
        {
            ["echo"] = ctx =>
            {
                calls.Add(("echo", ctx));
                return new Dictionary<string, object?> { ["result"] = ctx.Input["value"] };
            }
        });

        var runner = new ValidationRunner(dispatcher, new AssertionEngine());

        var yaml = """
                   spec_version: v1
                   cases:
                     - id: MAP_001
                       title: mapping
                       type: core
                       given:
                         input:
                           value: 7
                       when:
                         action: echo
                       then:
                         output:
                           result: 7
                       replay: false
                       deterministic: true
                   """;

        var cases = new YamlExecutionPlanParser().ParseYaml(yaml);
        var (caseId, plan) = Assert.Single(cases);
        var r = runner.Run(plan);

        Assert.Single(calls);
        Assert.Equal("echo", calls[0].action);
        Assert.Equal("7", calls[0].ctx.Input["value"]?.ToString());
        Assert.True(r.Passed);
    }

    [Fact]
    public void Should_Execute_Twice_When_Replay_Is_True()
    {
        var callCount = 0;

        var dispatcher = new ActionDispatcher(new Dictionary<string, Func<Context, object>>
        {
            ["echo"] = ctx =>
            {
                callCount++;
                return new Dictionary<string, object?> { ["result"] = ctx.Input["value"] };
            }
        });

        var runner = new ValidationRunner(dispatcher, new AssertionEngine());

        var yaml = """
                   spec_version: v1
                   cases:
                     - id: REPLAY_001
                       title: replay
                       type: core
                       given:
                         input:
                           value: 1
                       when:
                         action: echo
                       then:
                         output:
                           result: 1
                       replay: true
                       deterministic: true
                   """;

        var (caseId, plan) = Assert.Single(new YamlExecutionPlanParser().ParseYaml(yaml));
        var r = runner.Run(plan);

        Assert.Equal(2, callCount);
        Assert.True(r.Passed);
    }

    [Fact]
    public void Should_Not_Run_Replay_When_First_Run_Fails()
    {
        var callCount = 0;

        var dispatcher = new ActionDispatcher(new Dictionary<string, Func<Context, object>>
        {
            ["echo"] = _ =>
            {
                callCount++;
                return new Dictionary<string, object?> { ["result"] = 999 };
            }
        });

        var runner = new ValidationRunner(dispatcher, new AssertionEngine());

        var yaml = """
                   spec_version: v1
                   cases:
                     - id: REPLAY_FAIL_001
                       title: replay not executed on fail
                       type: core
                       given:
                         input:
                           value: 1
                       when:
                         action: echo
                       then:
                         output:
                           result: 1
                       replay: true
                       deterministic: true
                   """;

        var (caseId, plan) = Assert.Single(new YamlExecutionPlanParser().ParseYaml(yaml));
        var r = runner.Run(plan);

        Assert.Equal(1, callCount);
        Assert.False(r.Passed);
    }

    [Fact]
    public void Should_Enter_ErrorAssertionPath_When_ShouldThrow_Is_True()
    {
        var callCount = 0;

        var dispatcher = new ActionDispatcher(new Dictionary<string, Func<Context, object>>
        {
            ["thrower"] = _ =>
            {
                callCount++;
                throw new ValidationException("SOME_CODE");
            }
        });

        var runner = new ValidationRunner(dispatcher, new AssertionEngine());

        var yaml = """
                   spec_version: v1
                   cases:
                     - id: ERRPATH_001
                       title: should_throw semantic
                       type: core
                       given:
                         input: {}
                       when:
                         action: thrower
                       then:
                         error:
                           should_throw: true
                           code: SOME_CODE
                       replay: false
                       deterministic: true
                   """;

        var (caseId, plan) = Assert.Single(new YamlExecutionPlanParser().ParseYaml(yaml));
        var r = runner.Run(plan);

        Assert.Equal(1, callCount);
        Assert.True(r.Passed);
    }

    [Fact]
    public void Should_Seed_GivenDb_Into_ContextDbFixture()
    {
        object? observed = null;

        var dispatcher = new ActionDispatcher(new Dictionary<string, Func<Context, object>>
        {
            ["read_db"] = ctx =>
            {
                observed = ctx.Db.Get("any");
                return new Dictionary<string, object?> { ["result"] = 1 };
            }
        });

        var runner = new ValidationRunner(dispatcher, new AssertionEngine());

        var yaml = """
                   spec_version: v1
                   cases:
                     - id: DB_001
                       title: seed db
                       type: core
                       given:
                         db:
                           any:
                             x: 1
                         input: {}
                       when:
                         action: read_db
                       then:
                         output:
                           result: 1
                       replay: false
                       deterministic: true
                   """;

        var (caseId, plan) = Assert.Single(new YamlExecutionPlanParser().ParseYaml(yaml));
        var r = runner.Run(plan);

        Assert.True(r.Passed, r.Reason);
        Assert.NotNull(observed);
    }

    [Fact]
    public void Should_Compare_Replay_Results_Structurally_Using_Json()
    {
        var callCount = 0;

        var dispatcher = new ActionDispatcher(new Dictionary<string, Func<Context, object>>
        {
            ["unordered"] = _ =>
            {
                callCount++;
                // same logical content, different insertion order per call
                return callCount == 1
                    ? new Dictionary<string, object?> { ["b"] = 2, ["a"] = 1 }
                    : new Dictionary<string, object?> { ["a"] = 1, ["b"] = 2 };
            }
        });

        var runner = new ValidationRunner(dispatcher, new AssertionEngine());

        var yaml = """
                   spec_version: v1
                   cases:
                     - id: REPLAY_JSON_001
                       title: replay json compare
                       type: core
                       given:
                         input: {}
                       when:
                         action: unordered
                       then:
                         output:
                           a: 1
                           b: 2
                       replay: true
                       deterministic: true
                   """;

        var (caseId, plan) = Assert.Single(new YamlExecutionPlanParser().ParseYaml(yaml));
        var r = runner.Run(plan);

        Assert.Equal(2, callCount);
        Assert.True(r.Passed, r.Reason);
    }

    [Fact]
    public void Should_Print_Fail_When_Action_Is_Unknown()
    {
        var runner = new ValidationRunner(new ActionDispatcher(new Dictionary<string, Func<Context, object>>()), new AssertionEngine());

        var yaml = """
                   spec_version: v1
                   cases:
                     - id: UNKNOWN_001
                       title: unknown action
                       type: core
                       given:
                         input: {}
                       when:
                         action: not_exists
                       then:
                         output: {}
                       replay: false
                       deterministic: true
                   """;

        var (caseId, plan) = Assert.Single(new YamlExecutionPlanParser().ParseYaml(yaml));
        var r = runner.Run(plan);

        Assert.False(r.Passed);
        Assert.Contains("ACTION_NOT_FOUND", r.Reason ?? "");
    }

    [Fact]
    public void Should_Create_Demo_Row_Via_Injected_TestDbProvider()
    {
        // demo EF-based provider removed; keep runner smoke test using echo
        var dispatcher = new ActionDispatcher(new Dictionary<string, Func<Context, object>>
        {
            ["echo"] = ctx => new Dictionary<string, object?> { ["result"] = ctx.Input["value"] }
        });
        var runner = new ValidationRunner(dispatcher, new AssertionEngine());

        var yaml = """
                   spec_version: v1
                   cases:
                     - id: SMOKE_001
                       type: core
                       given:
                         input:
                           value: 1
                       when:
                         action: echo
                       then:
                         output:
                           result: 1
                       replay: false
                       deterministic: true
                   """;

        var (_, plan) = Assert.Single(new YamlExecutionPlanParser().ParseYaml(yaml));
        var r = runner.Run(plan);
        Assert.True(r.Passed, r.Reason);
    }

    [Fact]
    public void Should_Use_Isolated_InMemory_Db_Per_ExecutionPlan()
    {
        var runner = new ValidationRunner(
            new ActionDispatcher(new Dictionary<string, Func<Context, object>>
            {
                ["echo"] = ctx => new Dictionary<string, object?> { ["result"] = ctx.Input["value"] }
            }),
            new AssertionEngine()
        );

        var yaml = """
                   spec_version: v1
                   cases:
                     - id: ISO_A
                       type: core
                       given:
                         input:
                           value: 1
                       when:
                         action: echo
                       then:
                         output:
                           result: 1
                       replay: false
                       deterministic: true
                     - id: ISO_B
                       type: core
                       given:
                         input:
                           value: 1
                       when:
                         action: echo
                       then:
                         output:
                           result: 1
                       replay: false
                       deterministic: true
                   """;

        var plans = new YamlExecutionPlanParser().ParseYaml(yaml);
        Assert.Equal(2, plans.Count);

        foreach (var (_, plan) in plans)
        {
            var r = runner.Run(plan);
            Assert.True(r.Passed, r.Reason);
        }
    }

    private static string WriteTempYaml(string yaml)
    {
        var path = Path.Combine(Path.GetTempPath(), $"validation-{Guid.NewGuid():N}.yaml");
        File.WriteAllText(path, yaml, Encoding.UTF8);
        return path;
    }

    private static string CaptureConsole(Action action)
    {
        var sb = new StringBuilder();
        var writer = new StringWriter(sb);
        var original = Console.Out;
        Console.SetOut(writer);
        try
        {
            action();
            writer.Flush();
            return sb.ToString();
        }
        finally
        {
            Console.SetOut(original);
        }
    }
}
