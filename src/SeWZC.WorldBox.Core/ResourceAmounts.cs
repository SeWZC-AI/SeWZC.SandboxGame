namespace SeWZC.WorldBox.Core;

/// <summary>配方和费用所用的不可变资源数量。</summary>
public sealed class ResourceAmounts
{
    public double Water { get; init; }
    public double Food { get; init; }
    public double Wood { get; init; }
    public double Stone { get; init; }
    public double Ore { get; init; }
    public double Alloy { get; init; }
    public double EnergyCells { get; init; }
    public double Crystals { get; init; }
    public double Coal { get; init; }
    public double Oil { get; init; }
    public double RareEarth { get; init; }
    public double Boats { get; init; }
    public double Aircraft { get; init; }
    public double Tools { get; init; }
    public double Medicine { get; init; }
    public double Ammunition { get; init; }

    public double Get(ResourceKind kind) => kind switch
    {
        ResourceKind.Water => Water,
        ResourceKind.Food => Food,
        ResourceKind.Wood => Wood,
        ResourceKind.Stone => Stone,
        ResourceKind.Ore => Ore,
        ResourceKind.Alloy => Alloy,
        ResourceKind.EnergyCells => EnergyCells,
        ResourceKind.Crystals => Crystals,
        ResourceKind.Coal => Coal,
        ResourceKind.Oil => Oil,
        ResourceKind.RareEarth => RareEarth,
        ResourceKind.Boats => Boats,
        ResourceKind.Aircraft => Aircraft,
        ResourceKind.Tools => Tools,
        ResourceKind.Medicine => Medicine,
        ResourceKind.Ammunition => Ammunition,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>创建可编辑的资源副本。</summary>
    public ResourceStock Copy() => new()
    {
        Water = Water,
        Food = Food,
        Wood = Wood,
        Stone = Stone,
        Ore = Ore,
        Alloy = Alloy,
        EnergyCells = EnergyCells,
        Crystals = Crystals,
        Coal = Coal,
        Oil = Oil,
        RareEarth = RareEarth,
        Boats = Boats,
        Aircraft = Aircraft,
        Tools = Tools,
        Medicine = Medicine,
        Ammunition = Ammunition,
    };
}
