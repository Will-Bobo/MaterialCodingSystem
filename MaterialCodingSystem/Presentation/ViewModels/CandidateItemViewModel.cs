using MaterialCodingSystem.Application.Contracts;

namespace MaterialCodingSystem.Presentation.ViewModels;

public sealed class CandidateItemViewModel : ViewModelBase
{
    public MaterialItemSpecHit Source { get; }

    public string Code => Source.Code;
    public string Spec => Source.Spec;
    public string Description => Source.Description;
    public string Name => Source.Name;
    public string? Brand => Source.Brand;
    public long Status => Source.Status;
    public long GroupId => Source.GroupId;

    private string _specPrefix = "";
    public string SpecPrefix { get => _specPrefix; private set => SetProperty(ref _specPrefix, value); }

    private string _specMatch = "";
    public string SpecMatch { get => _specMatch; private set => SetProperty(ref _specMatch, value); }

    private string _specSuffix = "";
    public string SpecSuffix { get => _specSuffix; private set => SetProperty(ref _specSuffix, value); }

    private bool _hasMatch;
    public bool HasMatch { get => _hasMatch; private set => SetProperty(ref _hasMatch, value); }

    public CandidateItemViewModel(MaterialItemSpecHit source, string? currentKeyword)
    {
        Source = source;
        RebuildSpecHighlight(currentKeyword);
    }

    public void RebuildSpecHighlight(string? currentKeyword)
    {
        var r = HighlightHelper.Build(currentKeyword, Spec);
        SpecPrefix = r.Prefix;
        SpecMatch = r.Match;
        SpecSuffix = r.Suffix;
        HasMatch = r.HasMatch;
    }
}

