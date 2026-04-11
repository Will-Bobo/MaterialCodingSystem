using MaterialCodingSystem.Domain.Entities;
using MaterialCodingSystem.Domain.Services;
using MaterialCodingSystem.Domain.ValueObjects;

namespace MaterialCodingSystem.Tests.Domain;

public class EntitiesTests
{
    [Fact]
    public void CreateGroupWithA_CreatesItemA_WithCorrectCode()
    {
        var group = MaterialGroup.CreateNew(
            categoryCode: new CategoryCode("ZDA"),
            serialNo: 1,
            spec: new Spec("CL10A106KP8NNNC"),
            name: "电容",
            description: "10uF 16V 0603 X5R",
            brand: "SAMSUNG"
        );

        Assert.Single(group.Items);
        Assert.Equal('A', group.Items[0].Suffix.Value);
        Assert.Equal("ZDA0000001A", group.Items[0].Code);
        Assert.Equal("10UF 16V 0603 X5R", group.Items[0].SpecNormalized.Value);
    }

    [Fact]
    public void AddReplacement_AppendsNextSuffix()
    {
        var group = MaterialGroup.CreateNew(new CategoryCode("ZDA"), 1, new Spec("S1"), "n", "d", "b");

        var itemB = group.AddReplacement(new Spec("S2"), "n2", "d2", "b2");

        Assert.Equal('B', itemB.Suffix.Value);
        Assert.Equal("ZDA0000001B", itemB.Code);
        Assert.Equal(2, group.Items.Count);
    }

    [Fact]
    public void AddReplacement_WhenSequenceBroken_Throws()
    {
        var group = MaterialGroup.CreateNew(new CategoryCode("ZDA"), 1, new Spec("S1"), "n", "d", "b");

        // 人为破坏：插入一个 C（模拟异常数据，PRD 要求发现缺口禁止新增）
        group.DebugAddItemForTestOnly(new MaterialItem(
            code: "ZDA0000001C",
            suffix: new Suffix('C'),
            spec: new Spec("S3"),
            name: "n3",
            description: "d3",
            specNormalized: new SpecNormalized(SpecNormalizer.NormalizeV1("d3")),
            brand: "b3"
        ));

        var ex = Assert.Throws<DomainException>(() => group.AddReplacement(new Spec("S4"), "n4", "d4", "b4"));
        Assert.Equal("SUFFIX_SEQUENCE_BROKEN", ex.Code);
    }
}

