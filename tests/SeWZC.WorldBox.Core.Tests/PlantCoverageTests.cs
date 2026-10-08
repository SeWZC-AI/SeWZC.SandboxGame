namespace SeWZC.WorldBox.Core.Tests;

/// <summary>自然植物份额与作物分离的检查。</summary>
public sealed class PlantCoverageTests
{
    /// <summary>植物份额读取对应类别。</summary>
    [Theory]
    [InlineData(PlantKind.Trees, .1)]
    [InlineData(PlantKind.Shrubs, .2)]
    [InlineData(PlantKind.Grass, .3)]
    [InlineData(PlantKind.Reeds, .4)]
    [InlineData(PlantKind.Crops, 0)]
    public void Get_returns_the_natural_plant_share(PlantKind kind, double expected)
    {
        var coverage = new PlantCoverage { Trees = .1, Shrubs = .2, Grass = .3, Reeds = .4 };

        Assert.Equal(expected, coverage.Get(kind));
    }

    /// <summary>作物不覆盖任何自然植物份额。</summary>
    [Fact]
    public void WithCoverage_ignores_crops()
    {
        var coverage = new PlantCoverage { Trees = .1, Shrubs = .2, Grass = .3, Reeds = .4 };
        var before = coverage;

        coverage = coverage.WithCoverage(PlantKind.Crops, .9);

        Assert.Equal(before, coverage);
    }

    /// <summary>同一总量但组成不同的植物覆盖不相等。</summary>
    [Fact]
    public void Equality_compares_composition_instead_of_total()
    {
        var trees = new PlantCoverage { Trees = .5 };
        var grass = new PlantCoverage { Grass = .5 };

        Assert.Equal(trees.Total, grass.Total);
        Assert.NotEqual(trees, grass);
    }

    /// <summary>替换自然植物份额产生新值，旧覆盖保留原组成。</summary>
    [Fact]
    public void WithCoverage_preserves_original_shares()
    {
        var original = new PlantCoverage { Trees = .1, Shrubs = .2 };

        var changed = original.WithCoverage(PlantKind.Trees, .7);

        Assert.Equal(.1, original.Trees);
        Assert.Equal(.7, changed.Trees);
        Assert.Equal(.2, changed.Shrubs);
    }
}
