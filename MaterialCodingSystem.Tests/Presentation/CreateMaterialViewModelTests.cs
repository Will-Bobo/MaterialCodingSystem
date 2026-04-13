using MaterialCodingSystem.Application;
using MaterialCodingSystem.Application.Contracts;
using MaterialCodingSystem.Presentation.Scheduling;
using MaterialCodingSystem.Presentation.UiSemantics;
using MaterialCodingSystem.Presentation.ViewModels;
using MaterialCodingSystem.Tests.Application;

namespace MaterialCodingSystem.Tests.Presentation;

public sealed class CreateMaterialViewModelTests
{
    static CreateMaterialViewModelTests()
    {
        var dir = AppContext.BaseDirectory;
        UiResources.LoadDictionariesForTests(
            Path.Combine(dir, "UiErrors.xaml"),
            Path.Combine(dir, "UiStrings.xaml"));
    }

    private sealed class SynchronousDebouncer : IDebouncer
    {
        public void Debounce(object key, TimeSpan delay, Func<CancellationToken, Task> work)
        {
            work(default).GetAwaiter().GetResult();
        }
    }

    private sealed class NoopUiRenderer : IUiRenderer
    {
        public UiRenderPlan BuildRenderPlan(AppError error, ContextType context) =>
            new(
                "",
                UiPresentation.Banner,
                UiSeverity.Error,
                Array.Empty<string>(),
                "",
                UiClearStrategy.None,
                null);

        public void LogTechnicalFailure(AppError error)
        {
        }

        public bool ConfirmDuplicateCreate() => true;

        public bool ConfirmDeprecate(string code) => true;
    }

    private sealed class NoopUiDispatcher : IUiDispatcher
    {
        public void Apply(UiRenderPlan plan, object host)
        {
        }
    }

    [Fact]
    public async Task When_spec_field_active_uses_spec_as_search_keyword()
    {
        var repo = new FakeMaterialRepository();
        repo.SpecSearchHits.Add(new MaterialItemSpecHit("ZDA0000001A", "PART", "DESC", "N", null, 1, 1));
        var app = new MaterialApplicationService(new NoopUnitOfWork(), repo);
        var vm = new CreateMaterialViewModel(
            app,
            new SynchronousDebouncer(),
            new NoopUiRenderer(),
            new NoopUiDispatcher(),
            _ => Task.CompletedTask,
            () => Task.CompletedTask);

        await Task.Delay(150);
        vm.SelectedCategory = vm.Categories.First(c => c.Code == "ZDA");
        vm.NotifySpecFieldFocused();
        vm.Spec = "PART";

        Assert.Single(vm.CandidateItems);
        Assert.Equal("PART", repo.LastSearchBySpecQuery!.SpecKeyword);
        Assert.Equal("ZDA", repo.LastSearchBySpecQuery.CategoryCode);
    }

    [Fact]
    public async Task When_description_field_active_uses_description_as_search_keyword()
    {
        var repo = new FakeMaterialRepository();
        repo.CategoryRows.Add(("ZDB", "电容"));
        repo.SpecSearchHits.Add(new MaterialItemSpecHit("ZDA0000001A", "P", "10UF 16V", "N", null, 1, 1));
        var app = new MaterialApplicationService(new NoopUnitOfWork(), repo);
        var vm = new CreateMaterialViewModel(
            app,
            new SynchronousDebouncer(),
            new NoopUiRenderer(),
            new NoopUiDispatcher(),
            _ => Task.CompletedTask,
            () => Task.CompletedTask);

        await Task.Delay(150);
        vm.SelectedCategory = vm.Categories.First(c => c.Code == "ZDB");
        vm.NotifyDescriptionFieldFocused();
        vm.Description = "10UF 16V";

        Assert.Single(vm.CandidateItems);
        Assert.Equal("10UF 16V", repo.LastSearchBySpecQuery!.SpecKeyword);
        Assert.Equal("ZDB", repo.LastSearchBySpecQuery.CategoryCode);
    }

    [Fact]
    public async Task Create_sets_spec_field_error_on_duplicate()
    {
        var repo = new FakeMaterialRepository();
        repo.SpecExists = true;
        var app = new MaterialApplicationService(new NoopUnitOfWork(), repo);
        var vm = new CreateMaterialViewModel(
            app,
            new SynchronousDebouncer(),
            new WpfUiRenderer(),
            new WpfUiDispatcher(),
            _ => Task.CompletedTask,
            () => Task.CompletedTask);

        await Task.Delay(150);
        vm.SelectedCategory = vm.Categories.First(c => c.Code == "ZDA");
        vm.Spec = "DUP";
        vm.Name = "n";
        vm.Description = "d";
        vm.CreateCommand.Execute(null);
        await Task.Delay(400);

        Assert.Equal(UiResources.Get(UiResourceKeys.Error.SpecDuplicate), vm.SpecFieldError);
    }
}
