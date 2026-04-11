using MaterialCodingSystem.Domain.Services;
using MaterialCodingSystem.Domain.ValueObjects;

namespace MaterialCodingSystem.Domain.Entities;

public sealed class MaterialGroup
{
    public CategoryCode CategoryCode { get; }
    public int SerialNo { get; }

    private readonly List<MaterialItem> _items = new();
    public IReadOnlyList<MaterialItem> Items => _items;

    private MaterialGroup(CategoryCode categoryCode, int serialNo)
    {
        CategoryCode = categoryCode;
        SerialNo = serialNo;
    }

    public static MaterialGroup CreateNew(
        CategoryCode categoryCode,
        int serialNo,
        Spec spec,
        string name,
        string description,
        string? brand
    )
    {
        var group = new MaterialGroup(categoryCode, serialNo);

        var normalized = new SpecNormalized(SpecNormalizer.NormalizeV1(description));
        var code = CodeGenerator.GenerateItemCode(categoryCode.Value, serialNo, 'A');

        group._items.Add(new MaterialItem(
            code: code,
            suffix: new Suffix('A'),
            spec: spec,
            name: name,
            description: description,
            specNormalized: normalized,
            brand: brand
        ));

        return group;
    }

    public MaterialItem AddReplacement(Spec spec, string name, string description, string? brand)
    {
        var next = SuffixAllocator.AllocateNextSuffix(_items.Select(i => i.Suffix.Value).ToArray());
        var normalized = new SpecNormalized(SpecNormalizer.NormalizeV1(description));
        var code = CodeGenerator.GenerateItemCode(CategoryCode.Value, SerialNo, next);

        var item = new MaterialItem(
            code: code,
            suffix: new Suffix(next),
            spec: spec,
            name: name,
            description: description,
            specNormalized: normalized,
            brand: brand
        );

        _items.Add(item);
        return item;
    }

    // 仅用于测试制造异常数据（模拟 DB 中已有坏数据），业务代码不得调用
    internal void DebugAddItemForTestOnly(MaterialItem item) => _items.Add(item);
}

