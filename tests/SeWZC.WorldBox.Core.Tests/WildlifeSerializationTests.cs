using System.Text.Json;
using SeWZC.WorldBox.Core;

namespace SeWZC.WorldBox.Core.Tests;

/// <summary>动物种群稀疏格式的配对、精度与非法输入检查。</summary>
public sealed class WildlifeSerializationTests
{
    /// <summary>逐一检查现行物种，不固定物种总数。</summary>
    public static TheoryData<WildlifeKind> Species => new(
        Enum.GetValues<WildlifeKind>().Where(kind => kind != WildlifeKind.None));

    /// <summary>每种动物恢复到对应的物种字段。</summary>
    [Theory]
    [MemberData(nameof(Species))]
    public void Read_assigns_the_count_to_the_requested_species(WildlifeKind species)
    {
        var populations = JsonSerializer.Deserialize<WildlifePopulations>($"[{(int)species},0.125]");

        Assert.Equal(.125, populations.Get(species));
        Assert.All(Enum.GetValues<WildlifeKind>().Where(kind => kind != species),
            kind => Assert.Equal(0, populations.Get(kind)));
    }

    /// <summary>很小的非零种群也保持完整精度。</summary>
    [Theory]
    [InlineData(.000000001)]
    [InlineData(.125)]
    [InlineData(1000)]
    public void Round_trip_preserves_exact_population(double count)
    {
        var populations = new WildlifePopulations { Rabbit = count };

        var json = JsonSerializer.Serialize(populations);
        var restored = JsonSerializer.Deserialize<WildlifePopulations>(json);

        Assert.Equal(count, restored.Rabbit);
    }

    /// <summary>零种群不占用稀疏数组空间。</summary>
    [Fact]
    public void Write_omits_zero_populations()
    {
        Assert.Equal("[]", JsonSerializer.Serialize(new WildlifePopulations()));
    }

    /// <summary>稀疏输入拒绝无效物种、配对和数量。</summary>
    [Theory]
    [InlineData("{}")]
    [InlineData("[0,1]")]
    [InlineData("[-1,1]")]
    [InlineData("[2147483647,1]")]
    [InlineData("[1.5,1]")]
    [InlineData("[1]")]
    [InlineData("[1,1,1,2]")]
    [InlineData("[1,-0.1]")]
    [InlineData("[1,1000.1]")]
    [InlineData("[1,1e999]")]
    [InlineData("[1,null]")]
    [InlineData("[\"1\",1]")]
    [InlineData("[1,\"1\"]")]
    [InlineData("[1,1")]
    public void Read_rejects_invalid_sparse_arrays(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<WildlifePopulations>(json));
    }

    /// <summary>稀疏格式接受无种群的合法配对。</summary>
    [Fact]
    public void Read_accepts_an_explicit_zero_count()
    {
        var populations = JsonSerializer.Deserialize<WildlifePopulations>($"[{(int)WildlifeKind.Rabbit},0]");

        Assert.Equal(default, populations);
    }

    /// <summary>种群转换返回新值，原种群和其他物种数量保持不变。</summary>
    [Fact]
    public void WithPopulation_preserves_the_original_and_other_species()
    {
        var original = new WildlifePopulations { Rabbit = 2, Wolf = .5 };

        var changed = original.WithPopulation(WildlifeKind.Rabbit, 4);

        Assert.Equal(2, original.Rabbit);
        Assert.Equal(4, changed.Rabbit);
        Assert.Equal(.5, changed.Wolf);
    }
}
