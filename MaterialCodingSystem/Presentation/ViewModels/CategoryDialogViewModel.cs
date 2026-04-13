using MaterialCodingSystem.Application;
using MaterialCodingSystem.Application.Contracts;
using MaterialCodingSystem.Presentation.UiSemantics;

namespace MaterialCodingSystem.Presentation.ViewModels;

public sealed class CategoryDialogViewModel : ViewModelBase
{
    private readonly MaterialApplicationService _app;
    private readonly IUiRenderer _uiRenderer;
    private readonly IUiDispatcher _uiDispatcher;

    private string _code = "";
    public string Code { get => _code; set => SetProperty(ref _code, value); }

    private string _name = "";
    public string Name { get => _name; set => SetProperty(ref _name, value); }

    private string _error = "";
    public string Error { get => _error; set => SetProperty(ref _error, value); }

    public RelayCommand SaveCommand { get; }

    public CategoryDialogViewModel(MaterialApplicationService app, IUiRenderer uiRenderer, IUiDispatcher uiDispatcher, Action closeWithSuccess)
    {
        _app = app;
        _uiRenderer = uiRenderer;
        _uiDispatcher = uiDispatcher;
        SaveCommand = new RelayCommand(async () => await SaveAsync(closeWithSuccess));
    }

    private async Task SaveAsync(Action closeWithSuccess)
    {
        Error = "";
        var res = await _app.CreateCategory(new CreateCategoryRequest(Code, Name));
        if (!res.IsSuccess)
        {
            var plan = _uiRenderer.BuildRenderPlan(res.Error!, ContextType.CategoryDialogSave);
            _uiDispatcher.Apply(plan, this);
            return;
        }

        closeWithSuccess();
    }
}
