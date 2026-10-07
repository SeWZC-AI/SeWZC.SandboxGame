namespace SeWZC.WorldBox.Core;

/// <summary>配方和费用所用的不可变资源数量。</summary>
public sealed record ResourceAmounts
{
    /// <summary>水数量。</summary>
    public double Water { get; init; }

    /// <summary>食物数量。</summary>
    public double Food { get; init; }

    /// <summary>木材数量。</summary>
    public double Wood { get; init; }

    /// <summary>石料数量。</summary>
    public double Stone { get; init; }

    /// <summary>矿石数量。</summary>
    public double Ore { get; init; }

    /// <summary>合金数量。</summary>
    public double Alloy { get; init; }

    /// <summary>能源电池数量。</summary>
    public double EnergyCells { get; init; }

    /// <summary>晶石数量。</summary>
    public double Crystals { get; init; }

    /// <summary>煤数量。</summary>
    public double Coal { get; init; }

    /// <summary>石油数量。</summary>
    public double Oil { get; init; }

    /// <summary>稀土数量。</summary>
    public double RareEarth { get; init; }

    /// <summary>舟船数量。</summary>
    public double Boats { get; init; }

    /// <summary>飞机数量。</summary>
    public double Aircraft { get; init; }

    /// <summary>工具数量。</summary>
    public double Tools { get; init; }

    /// <summary>药品数量。</summary>
    public double Medicine { get; init; }

    /// <summary>弹药数量。</summary>
    public double Ammunition { get; init; }

    /// <summary>读取指定资源的数量；未知类别抛出参数异常。</summary>
    public double Get(ResourceKind kind)
    {
        return kind switch
        {
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
            ResourceKind.Water => Water,
            ResourceKind.Tools => Tools,
            ResourceKind.Medicine => Medicine,
            ResourceKind.Ammunition => Ammunition,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }

    /// <summary>将费用或配方转换为库存值。</summary>
    public ResourceStock ToStock()
    {
        return new ResourceStock
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
}
