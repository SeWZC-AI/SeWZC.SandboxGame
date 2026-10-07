using System.Text.Json;
using SeWZC.WorldBox.Core;

namespace SeWZC.WorldBox.Core.Tests;

/// <summary>研究目录身份、保存编号与路线选择检查。</summary>
public sealed class AdvancementTests
{
    /// <summary>目录编号用于逐项测试共享对象的查找。</summary>
    public static TheoryData<int> ResearchIds => new(Advancement.All.Select(research => research.Id));

    /// <summary>目录能在首次访问时完成初始化。</summary>
    [Fact]
    public void Catalog_initializes_before_building_its_index()
    {
        Assert.NotEmpty(Advancement.All);
        Assert.Same(Advancement.Agriculture, Advancement.Find(Advancement.Agriculture.Id));
    }

    /// <summary>编号查找返回目录共享对象。</summary>
    [Theory]
    [MemberData(nameof(ResearchIds))]
    public void Find_returns_the_catalog_instance(int id)
    {
        Assert.Same(Advancement.All.Single(research => research.Id == id), Advancement.Find(id));
    }

    /// <summary>保存仅记录编号并恢复共享研究节点。</summary>
    [Fact]
    public void Serialization_restores_the_canonical_research_instance()
    {
        var research = Advancement.Electrification;

        var json = JsonSerializer.Serialize(research);
        var restored = JsonSerializer.Deserialize<Advancement>(json);

        Assert.Equal(research.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), json);
        Assert.Same(research, restored);
    }

    /// <summary>非法编号或类型不能恢复为研究知识。</summary>
    [Theory]
    [InlineData("-1")]
    [InlineData("2147483647")]
    [InlineData("1.5")]
    [InlineData("\"Agriculture\"")]
    [InlineData("{}")]
    [InlineData("[]")]
    public void Serialization_rejects_unknown_ids_and_wrong_types(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Advancement>(json));
    }

    /// <summary>共同基础进入两条路线，专属知识只进入自己的路线。</summary>
    [Theory]
    [InlineData("Agriculture", true, true, true)]
    [InlineData("Electrification", true, false, false)]
    [InlineData("SpatialMagic", false, true, false)]
    public void Routes_include_only_their_knowledge(string key, bool technology, bool magic, bool common)
    {
        var research = Advancement.All.Single(value => value.Key == key);

        Assert.Equal(technology, ResearchRoute.Technology.Includes(research));
        Assert.Equal(magic, ResearchRoute.Magic.Includes(research));
        Assert.Equal(common, ResearchRoute.Common.Includes(research));
    }

    /// <summary>研究费用的可编辑副本不会污染共享目录。</summary>
    [Fact]
    public void Editable_research_cost_is_isolated_from_the_catalog()
    {
        var cost = WorldEngine.GetResearchCost(Advancement.Agriculture);

        cost = cost with { Food = 0 };

        Assert.Equal(Advancement.Agriculture.Cost.Food,
            WorldEngine.GetResearchCost(Advancement.Agriculture).Food);
        Assert.True(Advancement.Agriculture.Cost.Food > 0);
    }
}
