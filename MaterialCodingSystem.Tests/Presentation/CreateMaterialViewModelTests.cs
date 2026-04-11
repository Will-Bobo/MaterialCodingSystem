using MaterialCodingSystem.Application;
using MaterialCodingSystem.Application.Contracts;
using MaterialCodingSystem.Presentation.Scheduling;
using MaterialCodingSystem.Presentation.Services;
using MaterialCodingSystem.Presentation.ViewModels;
using MaterialCodingSystem.Tests.Application;

namespace MaterialCodingSystem.Tests.Presentation;

public sealed class CreateMaterialViewModelTests
{
    private sealed class SynchronousDebouncer : IDebouncer
    {
        public void Debounce(object key, TimeSpan delay, Func<CancellationToken, Task> work)
        {
            work(default).GetAwaiter().GetResult();
        }
    }

    private sealed class NoopDialogService : IDialogService
    {
        public void ShowWarning(string title, string message)
        {
        }
    }

    [Fact]
    public async Task When_spec_field_active_uses_spec_as_search_keyword()
    {
        var repo = new FakeMaterialRepository();
        repo.SpecSearchHits.Add(new MaterialItemSpecHit("ZDA0000001A", "PART", "DESC", "N", null));
        var app = new MaterialApplicationService(new NoopUnitOfWork(), repo);
        var vm = new CreateMaterialViewModel(
            app,
            new SynchronousDebouncer(),
            new NoopDialogService(),
            _ => Task.CompletedTask,
            () => Task.CompletedTask);

        await Task.Delay(150);
        vm.CategoryCode = "ZDA";
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
        repo.SpecSearchHits.Add(new MaterialItemSpecHit("ZDA0000001A", "P", "10UF 16V", "N", null));
        var app = new MaterialApplicationService(new NoopUnitOfWork(), repo);
        var vm = new CreateMaterialViewModel(
            app,
            new SynchronousDebouncer(),
            new NoopDialogService(),
            _ => Task.CompletedTask,
            () => Task.CompletedTask);

        await Task.Delay(150);
        vm.CategoryCode = "ZDB";
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
            new NoopDialogService(),
            _ => Task.CompletedTask,
            () => Task.CompletedTask);

        await Task.Delay(150);
        vm.CategoryCode = "ZDA";
        vm.Spec = "DUP";
        vm.Name = "n";
        vm.Description = "d";
        vm.CreateCommand.Execute(null);
        await Task.Delay(400);

        Assert.Contains("重复", vm.SpecFieldError);
    }
}
