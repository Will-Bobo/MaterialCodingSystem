// NOTE:
// This project is a validation tool.
// Dapper usage here is allowed and not restricted by main architecture rules.

using MaterialCodingSystem.Validation.actions;
using MaterialCodingSystem.Validation.core;
using MaterialCodingSystem.Validation.runner;

class Program
{
    static void Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.WriteLine("Usage: dotnet run <yaml_path>");
            return;
        }

        var parser = new YamlExecutionPlanParser();
        var cases = parser.ParseFile(args[0]);

        var dispatcher = new ActionDispatcher(new Dictionary<string, Func<Context, object>>
        {
            // pure
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

        foreach (var (caseId, plan) in cases)
        {
            var r = runner.Run(plan);
            Console.WriteLine($"Case: {caseId}");
            Console.WriteLine($"Result: {(r.Passed ? "PASS" : "FAIL")}");
            if (!r.Passed) Console.WriteLine($"Reason: {r.Reason}");
            Console.WriteLine();
        }
    }
}