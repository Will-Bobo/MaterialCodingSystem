namespace MaterialCodingSystem.Validation.core;

public class AssertionEngine
{
    public void Assert(ExecutionPlan plan, object result)
    {
        foreach (var a in plan.Assertions)
        {
            switch (a)
            {
                case AssertionSpec.ExpectError ee:
                    AssertError(ee, result);
                    break;
                case AssertionSpec.ExpectResultEquals eq:
                    AssertResultEquals(eq, result);
                    break;
                default:
                    throw new Exception($"Unknown assertion: {a.GetType().Name}");
            }
        }
    }

    private static void AssertError(AssertionSpec.ExpectError spec, object result)
    {
        if (spec.ShouldThrow)
        {
            if (result is not Exception ex)
                throw new Exception($"Expected error {spec.Code} but got none");

            if (!string.IsNullOrWhiteSpace(spec.Code))
            {
                if (ex is ValidationException vex)
                {
                    if (!string.Equals(vex.Code, spec.Code, StringComparison.Ordinal))
                        throw new Exception($"Expected error {spec.Code} but got {vex.Code}");
                }
                else
                {
                    throw new Exception($"Expected error {spec.Code} but got {ex.GetType().Name}");
                }
            }

            return;
        }

        if (result is Exception ex2)
        {
            if (ex2 is ValidationException vex2)
                throw new Exception($"Unexpected error: {vex2.Code}");
            throw new Exception($"Unexpected error: {ex2.GetType().Name}");
        }
    }

    private static void AssertResultEquals(AssertionSpec.ExpectResultEquals spec, object result)
    {
        if (result is Exception ex2)
        {
            if (ex2 is ValidationException vex2)
                throw new Exception($"Unexpected error: {vex2.Code}");
            throw new Exception($"Unexpected error: {ex2.GetType().Name}");
        }

        var ej = JsonUtil.StableStringify(spec.Expected);
        var aj = JsonUtil.StableStringify(result);
        if (!string.Equals(ej, aj, StringComparison.Ordinal))
            throw new Exception($"Result equals assertion failed. expected={ej} actual={aj}");
    }

    // legacy deep-equals left intentionally removed in favor of JsonUtil-based stable compare
}