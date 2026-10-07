using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

public sealed partial class Tile
{
    // 用 255 区分尚未计算与有效的空物种结果，避免空地反复计算。
    private byte _edibleLandAnimal = byte.MaxValue, _edibleWaterAnimal = byte.MaxValue;
    private WildlifePopulations _otherWildlife;
    private WildlifeKind _wildlife;
    private double _wildlifePopulation;

    /// <summary>单独存储的主种群物种。</summary>
    [JsonRequired]
    public WildlifeKind Wildlife
    {
        get => _wildlife;
        set
        {
            if (_wildlife != value) InvalidateEdibleAnimals();
            _wildlife = value;
        }
    }

    /// <summary>主种群数量。</summary>
    [JsonRequired]
    public double WildlifePopulation
    {
        get => _wildlifePopulation;
        set
        {
            if (_wildlifePopulation != value) InvalidateEdibleAnimals();
            _wildlifePopulation = value;
        }
    }

    /// <summary>除主种群外，按物种存储的动物数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public WildlifePopulations OtherWildlife
    {
        get => _otherWildlife;
        set
        {
            _otherWildlife = value;
            InvalidateEdibleAnimals();
        }
    }

    /// <summary>当前数量大于零的物种位掩码，位序对应物种编号。</summary>
    [JsonIgnore]
    public int WildlifeMask => _otherWildlife.ActiveMask | (_wildlifePopulation > 0 ? 1 << (int)_wildlife : 0);

    private void InvalidateEdibleAnimals()
    {
        _edibleLandAnimal = _edibleWaterAnimal = byte.MaxValue;
    }

    internal WildlifeKind EdibleAnimal(bool aquatic)
    {
        ref var cached = ref aquatic ? ref _edibleWaterAnimal : ref _edibleLandAnimal;
        if (cached != byte.MaxValue) return (WildlifeKind)cached;
        cached = (byte)WildlifeKind.None;
        var largest = 0d;
        foreach (var kind in AnimalRules.EdibleAnimals(aquatic))
        {
            var population = AnimalPopulation(kind);
            var biomass = population * AnimalRules.For(kind).BodyMass;
            if (population >= .05 && biomass > largest)
            {
                cached = (byte)kind;
                largest = biomass;
            }
        }

        return (WildlifeKind)cached;
    }

    /// <summary>查询此格指定物种的数量，同时覆盖主种群和其他种群。</summary>
    /// <param name="kind">动物物种。</param>
    public double AnimalPopulation(WildlifeKind kind)
    {
        return kind == _wildlife ? _wildlifePopulation : _otherWildlife.Get(kind);
    }

    /// <summary>复制此格所有物种的数量。</summary>
    internal void CopyAnimalPopulations(Span<double> destination)
    {
        _otherWildlife.CopyTo(destination);
        destination[(int)_wildlife] = _wildlifePopulation;
    }

    internal void SetAnimalPopulation(WildlifeKind kind, double population)
    {
        if (kind == Wildlife) WildlifePopulation = population;
        else if (Wildlife == WildlifeKind.None && population > 0)
        {
            var others = OtherWildlife;
            others.Set(kind, 0);
            OtherWildlife = others;
            Wildlife = kind;
            WildlifePopulation = population;
        }
        else
        {
            var others = OtherWildlife;
            others.Set(kind, population);
            OtherWildlife = others;
        }
    }
}
