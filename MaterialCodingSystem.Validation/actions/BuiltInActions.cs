using MaterialCodingSystem.Validation.core;

namespace MaterialCodingSystem.Validation.actions;

public static class BuiltInActions
{
    // Minimal pure function action for smoke testing.
    public static object Echo(Context ctx)
    {
        return new Dictionary<string, object?> { ["result"] = ctx.Input["value"] };
    }
}
