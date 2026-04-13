using MaterialCodingSystem.Application;
using MaterialCodingSystem.Application.Contracts;
using MaterialCodingSystem.Presentation.UiSemantics;

namespace MaterialCodingSystem.Presentation.ViewModels;

public sealed class DeprecateViewModel : ViewModelBase
{
    private readonly MaterialApplicationService _app;
    private readonly IUiRenderer _uiRenderer;
    private readonly IUiDispatcher _uiDispatcher;

    private string _code = "";
    public string Code { get => _code; set => SetProperty(ref _code, value); }

    private string _result = "";
    public string Result { get => _result; set => SetProperty(ref _result, value); }

    public RelayCommand DeprecateCommand { get; }

    public DeprecateViewModel(MaterialApplicationService app, IUiRenderer uiRenderer, IUiDispatcher uiDispatcher)
    {
        _app = app;
        _uiRenderer = uiRenderer;
        _uiDispatcher = uiDispatcher;
        DeprecateCommand = new RelayCommand(async () => await DeprecateAsync());
    }

    private async Task DeprecateAsync()
    {
        Result = UiResources.Get(UiResourceKeys.Info.DeprecateProcessing);
        var res = await _app.DeprecateMaterialItem(new DeprecateRequest(Code));
        if (res.IsSuccess)
            Result = UiResources.Format(UiResourceKeys.Info.DeprecateDone, res.Data!.Code);
        else
        {
            var plan = _uiRenderer.BuildRenderPlan(res.Error!, ContextType.Deprecate);
            _uiDispatcher.Apply(plan, this);
        }
    }
}
