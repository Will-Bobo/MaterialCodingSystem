using MaterialCodingSystem.Application;
using MaterialCodingSystem.Application.Contracts;

namespace MaterialCodingSystem.Presentation.ViewModels;

public sealed class CategoryDialogViewModel : ViewModelBase
{
    private readonly MaterialApplicationService _app;

    private string _code = "";
    public string Code { get => _code; set => SetProperty(ref _code, value); }

    private string _name = "";
    public string Name { get => _name; set => SetProperty(ref _name, value); }

    private string _error = "";
    public string Error { get => _error; set => SetProperty(ref _error, value); }

    public RelayCommand SaveCommand { get; }

    public CategoryDialogViewModel(MaterialApplicationService app, Action closeWithSuccess)
    {
        _app = app;
        SaveCommand = new RelayCommand(async () => await SaveAsync(closeWithSuccess));
    }

    private async Task SaveAsync(Action closeWithSuccess)
    {
        Error = "";
        var res = await _app.CreateCategory(new CreateCategoryRequest(Code, Name));
        if (!res.IsSuccess)
        {
            Error = $"{res.Error!.Code}: {res.Error.Message}";
            return;
        }

        closeWithSuccess();
    }
}
