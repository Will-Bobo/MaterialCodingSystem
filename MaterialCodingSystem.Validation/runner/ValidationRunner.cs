using MaterialCodingSystem.Validation.core;

namespace MaterialCodingSystem.Validation.runner;

/// <summary>
/// Spec Runner：将 YAML 用例映射为
/// <list type="bullet">
/// <item><description><b>Given</b> → <see cref="SqliteDbFixture.Reset"/> + <see cref="SqliteDbFixture.Seed"/>（独占 <c>:memory:</c> 单连接）</description></item>
/// <item><description><b>When</b> → <see cref="ActionDispatcher.Dispatch"/>（业务动作应调用 <c>MaterialApplicationService</c> 等 Application API）</description></item>
/// <item><description><b>Then</b> → <see cref="AssertionEngine.Assert"/>（output / error / db exists）</description></item>
/// </list>
/// </summary>
public sealed class ValidationRunner
{
    private readonly ActionDispatcher _dispatcher;
    private readonly AssertionEngine _assert;

    public ValidationRunner(ActionDispatcher dispatcher, AssertionEngine assert)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _assert = assert ?? throw new ArgumentNullException(nameof(assert));
    }

    public ExecutionResult Run(ExecutionPlan plan)
    {
        try
        {
            var result1 = ExecuteOnce(plan);
            _assert.Assert(plan, result1);

            if (plan.Replay)
            {
                var result2 = ExecuteOnce(plan);
                _assert.Assert(plan, result2);

                var j1 = JsonUtil.StableStringify((result1 as ExecutionOutcome)?.Result ?? result1);
                var j2 = JsonUtil.StableStringify((result2 as ExecutionOutcome)?.Result ?? result2);
                if (!string.Equals(j1, j2, StringComparison.Ordinal))
                    throw new Exception("Replay mismatch");
            }

            return ExecutionResult.Pass();
        }
        catch (Exception ex)
        {
            var reason = $"{ex.Message} | when.action={plan.ActionName}";
            return ExecutionResult.Fail(reason, ValidationFailureHints.SuggestCodeRef(ex));
        }
    }

    private object ExecuteOnce(ExecutionPlan plan)
    {
        // 1. 创建独立 Context（每个 plan 独立）
        var db = new SqliteDbFixture();

        // 2. Seed 数据（plan.Seed）
        db.Reset();
        db.Seed(plan.Seed?.Data);

        // 3. context input
        var input = new InputModel(plan.Input);
        var ctx = new Context(db, input);

        // 4. dispatch (action throws -> captured as result)
        return new ExecutionOutcome(db, _dispatcher.Dispatch(plan.ActionName, ctx));
    }
}

public sealed record ExecutionOutcome(DbFixture Db, object Result);

public sealed record ExecutionResult(bool Passed, string? Reason, string? CodeReference)
{
    public static ExecutionResult Pass() => new(true, null, null);

    public static ExecutionResult Fail(string reason, string? codeReference = null) =>
        new(false, reason, codeReference);
}

internal static class ValidationFailureHints
{
    public static string SuggestCodeRef(Exception ex)
    {
        var m = ex.Message ?? "";
        if (m.Contains("Result equals assertion failed", StringComparison.Ordinal))
            return "MaterialCodingSystem.Validation/core/AssertionEngine.cs — AssertResultEquals";
        if (m.Contains("DB exists assertion failed", StringComparison.Ordinal))
            return "MaterialCodingSystem.Validation/core/AssertionEngine.cs — ExpectDbExists";
        if (m.Contains("Expected error", StringComparison.Ordinal) || m.Contains("Unexpected error", StringComparison.Ordinal))
            return "MaterialCodingSystem.Validation/core/AssertionEngine.cs — ExpectError";
        if (m.Contains("Replay mismatch", StringComparison.Ordinal))
            return "MaterialCodingSystem.Validation/runner/ValidationRunner.cs — replay";
        return "MaterialCodingSystem.Validation/actions/PrdActions.cs（或 BuiltInActions）— 对照 YAML when.action";
    }
}