using MaterialCodingSystem.Application;
using MaterialCodingSystem.Application.Contracts;
using MaterialCodingSystem.Presentation.Models;
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

        public bool ConfirmCreateMaterial(CreateMaterialConfirmModel model) => true;

        public bool ConfirmCreateReplacement(CreateReplacementConfirmModel model) => true;

        public Task<bool> ConfirmDeprecateAsync(DeprecateConfirmModel model) => Task.FromResult(true);
    }

    private sealed class NoopUiDispatcher : IUiDispatcher
    {
        public void Apply(UiRenderPlan plan, object host)
        {
        }
    }

    private static CreateMaterialViewModel CreateVm(FakeMaterialRepository repo)
    {
        var app = new MaterialApplicationService(new NoopUnitOfWork(), repo);
        return new CreateMaterialViewModel(
            app,
            new SynchronousDebouncer(),
            new NoopUiRenderer(),
            new NoopUiDispatcher(),
            _ => Task.CompletedTask,
            () => Task.CompletedTask);
    }

    [Fact]
    public async Task When_spec_field_active_uses_spec_as_search_keyword()
    {
        var repo = new FakeMaterialRepository();
        repo.SpecSearchHits.Add(new MaterialItemSpecHit("ZDA0000001A", "PART", "DESC", "N", null, 1, 1));
        var vm = CreateVm(repo);

        await Task.Delay(150);
        vm.SelectedCategory = vm.Categories.First(c => c.Code == "ZDA");
        vm.NotifySpecFieldFocused();
        vm.Spec = "PART";

        Assert.Single(vm.CandidateItems);
        Assert.Equal("PART", repo.LastSearchCandidatesBySpecOnlyKeyword);
        Assert.Equal("ZDA", repo.LastSearchCandidatesBySpecOnlyCategoryCode);
        Assert.Equal("PART", vm.CandidateItems[0].SpecMatch);
    }

    [Fact]
    public async Task When_description_field_active_does_not_trigger_candidate_search()
    {
        var repo = new FakeMaterialRepository();
        repo.CategoryRows.Add(("ZDB", "电容"));
        repo.SpecSearchHits.Add(new MaterialItemSpecHit("ZDA0000001A", "P", "10UF 16V", "N", null, 1, 1));
        var vm = CreateVm(repo);

        await Task.Delay(150);
        vm.SelectedCategory = vm.Categories.First(c => c.Code == "ZDB");
        vm.NotifyDescriptionFieldFocused();
        vm.Description = "10UF 16V";

        Assert.Empty(vm.CandidateItems);
        Assert.Null(repo.LastSearchCandidatesBySpecOnlyKeyword);
    }

    [Fact]
    public async Task Create_success_clears_input_fields_for_next_entry()
    {
        var repo = new FakeMaterialRepository();
        var vm = CreateVm(repo);

        await Task.Delay(150);
        vm.SelectedCategory = vm.Categories.First(c => c.Code == "ZDA");
        vm.Code = "ZDA0000001A";
        vm.Spec = "S1";
        vm.Name = "n";
        vm.Description = "D1";
        vm.Brand = "B1";

        vm.CreateCommand.Execute(null);
        await Task.Delay(400);

        Assert.Equal("", vm.Spec);
        Assert.Equal("", vm.Description);
        Assert.Equal("", vm.Brand);
    }

    [Fact]
    public async Task UiState_WhenDescriptionMissing_IsMissingRequiredFields()
    {
        var repo = new FakeMaterialRepository();
        var vm = CreateVm(repo);

        await Task.Delay(150);
        vm.SelectedCategory = vm.Categories.First(c => c.Code == "ZDA");
        vm.Code = "ZDA0000001A";
        vm.Spec = "S1";
        vm.Description = "   ";

        Assert.Equal(CreateMaterialState.MissingRequiredFields, vm.State);
    }

    [Fact]
    public async Task UiState_WhenBrandMissing_IsMissingRequiredFields()
    {
        var repo = new FakeMaterialRepository();
        var vm = CreateVm(repo);

        await Task.Delay(150);
        vm.SelectedCategory = vm.Categories.First(c => c.Code == "ZDA");
        vm.Code = "ZDA0000001A";
        vm.Spec = "S1";
        vm.Description = "D";
        vm.Brand = "   ";

        Assert.Equal(CreateMaterialState.MissingRequiredFields, vm.State);
    }

    [Fact]
    public async Task UiState_WhenForceAllowedButDescriptionMissing_StillMissingRequiredFields()
    {
        var repo = new FakeMaterialRepository();
        // similar candidates exist but not exact
        repo.SpecSearchHits.Add(new MaterialItemSpecHit("ZDA0000001A", "S1X", "D", "N", null, 1, 1));
        var vm = CreateVm(repo);

        await Task.Delay(150);
        vm.SelectedCategory = vm.Categories.First(c => c.Code == "ZDA");
        vm.Code = "ZDA0000001A";
        vm.NotifySpecFieldFocused();
        vm.Spec = "S1";
        vm.Description = "   ";

        vm.ForceCreateWithConfirmCommand.Execute(null);

        Assert.Equal(CreateMaterialState.MissingRequiredFields, vm.State);
    }

    [Fact]
    public async Task UiState_WhenExactSpecMatch_IsCandidateConflict()
    {
        var repo = new FakeMaterialRepository();
        repo.SpecSearchHits.Add(new MaterialItemSpecHit("ZDA0000001A", "S1", "D", "N", null, 1, 1));
        var vm = CreateVm(repo);

        await Task.Delay(150);
        vm.SelectedCategory = vm.Categories.First(c => c.Code == "ZDA");
        vm.Code = "ZDA0000001A";
        vm.NotifySpecFieldFocused();
        vm.Spec = "S1";
        vm.Description = "D";
        vm.Brand = "B";

        Assert.True(vm.HasExactSpecMatch);
        Assert.Equal(CreateMaterialState.CandidateConflict, vm.State);
    }

    [Fact]
    public async Task UiState_WhenHasCandidatesAndNoExactAndDescriptionPresent_IsCandidateNone_ThenReadyAfterForceAllowed()
    {
        var repo = new FakeMaterialRepository();
        repo.SpecSearchHits.Add(new MaterialItemSpecHit("ZDA0000001A", "S1X", "D", "N", null, 1, 1));
        var vm = CreateVm(repo);

        await Task.Delay(150);
        vm.SelectedCategory = vm.Categories.First(c => c.Code == "ZDA");
        vm.Code = "ZDA0000001A";
        vm.NotifySpecFieldFocused();
        vm.Spec = "S1";
        vm.Description = "D";
        vm.Brand = "B";

        Assert.False(vm.HasExactSpecMatch);
        Assert.NotEmpty(vm.CandidateItems);
        Assert.Equal(CreateMaterialState.CandidateConflict, vm.State);

        vm.ForceCreateWithConfirmCommand.Execute(null);
        Assert.Equal(CreateMaterialState.ReadyToCreate, vm.State);
    }

    [Fact]
    public async Task AllowedKey_WhenSpecChanges_AutoInvalidates_AndRequiresAllowAgain()
    {
        var repo = new FakeMaterialRepository();
        repo.SpecSearchHits.Add(new MaterialItemSpecHit("ZDA0000001A", "12340", "D", "N", null, 1, 1));
        var vm = CreateVm(repo);

        await Task.Delay(150);
        vm.SelectedCategory = vm.Categories.First(c => c.Code == "ZDA");
        vm.Code = "ZDA0000001A";
        vm.NotifySpecFieldFocused();

        vm.Spec = "1234";
        vm.Description = "D";
        vm.Brand = "B";
        Assert.Equal(CreateMaterialState.CandidateConflict, vm.State); // soft conflict

        vm.ForceCreateWithConfirmCommand.Execute(null);
        Assert.True(vm.IsForceCreateAllowed);
        Assert.Equal(CreateMaterialState.ReadyToCreate, vm.State);

        // change spec -> allowed auto invalidates
        vm.Spec = "123456";
        Assert.False(vm.IsForceCreateAllowed);
        Assert.Equal(CreateMaterialState.CandidateConflict, vm.State); // still soft conflict
    }

    [Fact]
    public async Task UiState_WhenCandidatesEmptyButDescriptionMissing_IsMissingRequiredFields()
    {
        var repo = new FakeMaterialRepository();
        // no hits -> CandidateItems empty
        var vm = CreateVm(repo);

        await Task.Delay(150);
        vm.SelectedCategory = vm.Categories.First(c => c.Code == "ZDA");
        vm.Code = "ZDA0000001A";
        vm.NotifySpecFieldFocused();
        vm.Spec = "S1";
        vm.Description = "";

        Assert.Empty(vm.CandidateItems);
        Assert.Equal(CreateMaterialState.MissingRequiredFields, vm.State);
    }
}
