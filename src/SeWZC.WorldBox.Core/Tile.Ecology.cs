using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

public sealed partial record Tile
{

    /// <summary>单独存储的主种群物种。</summary>
    [JsonRequired]
    public WildlifeKind Wildlife { get; init; }

    /// <summary>主种群数量。</summary>
    [JsonRequired]
    public double WildlifePopulation { get; init; }

    /// <summary>除主种群外，按物种存储的动物数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public WildlifePopulations OtherWildlife { get; init; }

    /// <summary>当前数量大于零的物种位掩码，位序对应物种编号。</summary>
    [JsonIgnore]
    public int WildlifeMask => OtherWildlife.ActiveMask | (WildlifePopulation > 0 ? 1 << (int)Wildlife : 0);

    internal WildlifeKind EdibleAnimal(bool aquatic)
    {
        var result = WildlifeKind.None;
        var largest = 0d;
        foreach (var kind in AnimalRules.EdibleAnimals(aquatic))
        {
            var population = AnimalPopulation(kind);
            var biomass = population * AnimalRules.For(kind).BodyMass;
            if (population >= .05 && biomass > largest)
            {
                result = kind;
                largest = biomass;
            }
        }
        return result;
    }

    /// <summary>查询此格指定物种的数量，同时覆盖主种群和其他种群。</summary>
    /// <param name="kind">动物物种。</param>
    public double AnimalPopulation(WildlifeKind kind)
    {
        return kind == Wildlife ? WildlifePopulation : OtherWildlife.Get(kind);
    }

    /// <summary>复制此格所有物种的数量。</summary>
    internal void CopyAnimalPopulations(Span<double> destination)
    {
        OtherWildlife.CopyTo(destination);
        destination[(int)Wildlife] = WildlifePopulation;
    }

    /// <summary>返回更新指定种群后的地格，保留其他种群。</summary>
    /// <param name="kind">动物物种。</param>
    /// <param name="population">更新后的数量。</param>
    public Tile WithAnimalPopulation(WildlifeKind kind, double population)
    {
        if (kind == Wildlife)
            return this with { WildlifePopulation = population };
        if (Wildlife == WildlifeKind.None && population > 0)
            return this with { OtherWildlife = OtherWildlife.WithPopulation(kind, 0), Wildlife = kind, WildlifePopulation = population };
        return this with { OtherWildlife = OtherWildlife.WithPopulation(kind, population) };
    }
}
