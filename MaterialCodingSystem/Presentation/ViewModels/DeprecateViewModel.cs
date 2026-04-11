using MaterialCodingSystem.Application;
using MaterialCodingSystem.Application.Contracts;

namespace MaterialCodingSystem.Presentation.ViewModels;

public sealed class DeprecateViewModel : ViewModelBase
{
    private readonly MaterialApplicationService _app;

    private string _code = "";
    public string Code { get => _code; set => SetProperty(ref _code, value); }

    private string _result = "";
    public string Result { get => _result; set => SetProperty(ref _result, value); }

    public RelayCommand DeprecateCommand { get; }

    public DeprecateViewModel(MaterialApplicationService app)
    {
        _app = app;
        DeprecateCommand = new RelayCommand(async () => await DeprecateAsync());
    }

    private async Task DeprecateAsync()
    {
        Result = "处理中...";
        var res = await _app.DeprecateMaterialItem(new DeprecateRequest(Code));
        Result = res.IsSuccess
            ? $"已废弃：{res.Data!.Code}"
            : $"失败：{res.Error!.Code} - {res.Error.Message}";
    }
}

