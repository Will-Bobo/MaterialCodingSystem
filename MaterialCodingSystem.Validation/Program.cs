using MaterialCodingSystem.Validation.actions;
using MaterialCodingSystem.Validation.core;
using MaterialCodingSystem.Validation.infrastructure;
using MaterialCodingSystem.Validation.runner;
using MaterialCodingSystem.Validation.services;

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

        // 方案 A：测试环境通过注入 TestDbContextProvider，与主业务解耦（主工程不引用 Runner）。
        var dbProvider = new TestDbContextProvider();
        var demoService = new DemoMaterialService(dbProvider);

        var dispatcher = new ActionDispatcher(new Dictionary<string, Func<Context, object>>
        {
            ["echo"] = BuiltInActions.Echo,
            ["create_demo"] = demoService.Create
        });

        var runner = new ValidationRunner(dispatcher, new AssertionEngine());

        foreach (var (caseId, plan) in cases)
        {
            var r = runner.Run(plan);
            Console.WriteLine($"Case: {caseId}");
            Console.WriteLine($"Result: {(r.Passed ? "PASS" : "FAIL")}");
            if (!r.Passed)
                Console.WriteLine($"Reason: {r.Reason}");
            Console.WriteLine();
        }
    }
}