using MaterialCodingSystem.Domain.ValueObjects;

namespace MaterialCodingSystem.Domain.Entities;

public sealed class MaterialItem
{
    public string Code { get; }
    public Suffix Suffix { get; }
    public Spec Spec { get; }
    public string Name { get; }
    public string Description { get; }
    public SpecNormalized SpecNormalized { get; }
    public string? Brand { get; }

    public MaterialItem(
        string code,
        Suffix suffix,
        Spec spec,
        string name,
        string description,
        SpecNormalized specNormalized,
        string? brand
    )
    {
        Code = code;
        Suffix = suffix;
        Spec = spec;
        Name = name;
        Description = description;
        SpecNormalized = specNormalized;
        Brand = brand;
    }
}

