using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

public readonly partial record struct ResourceStock
{
    /// <summary>配方和库存查询使用的全部资源种类。</summary>
    public static IReadOnlyList<ResourceKind> Kinds { get; } = Array.AsReadOnly(Enum.GetValues<ResourceKind>());

    /// <summary>读取指定种类数量。</summary>
    /// <param name="kind">资源种类。</param>
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

    /// <summary>返回替换指定资源数量后的库存，原库存保持不变。</summary>
    /// <param name="kind">资源种类。</param>
    /// <param name="value">新的资源数量。</param>
    public ResourceStock WithAmount(ResourceKind kind, double value) => kind switch
    {
        ResourceKind.Food => this with { Food = value },
        ResourceKind.Wood => this with { Wood = value },
        ResourceKind.Stone => this with { Stone = value },
        ResourceKind.Ore => this with { Ore = value },
        ResourceKind.Alloy => this with { Alloy = value },
        ResourceKind.EnergyCells => this with { EnergyCells = value },
        ResourceKind.Crystals => this with { Crystals = value },
        ResourceKind.Coal => this with { Coal = value },
        ResourceKind.Oil => this with { Oil = value },
        ResourceKind.RareEarth => this with { RareEarth = value },
        ResourceKind.Boats => this with { Boats = value },
        ResourceKind.Aircraft => this with { Aircraft = value },
        ResourceKind.Water => this with { Water = value },
        ResourceKind.Tools => this with { Tools = value },
        ResourceKind.Medicine => this with { Medicine = value },
        ResourceKind.Ammunition => this with { Ammunition = value },
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>返回所有资源同时按倍率缩放后的库存。</summary>
    /// <param name="factor">各类资源共同使用的数量倍率。</param>
    public ResourceStock Scale(double factor) => new()
    {
        Food = Food * factor,
        Wood = Wood * factor,
        Stone = Stone * factor,
        Ore = Ore * factor,
        Alloy = Alloy * factor,
        EnergyCells = EnergyCells * factor,
        Crystals = Crystals * factor,
        Coal = Coal * factor,
        Oil = Oil * factor,
        RareEarth = RareEarth * factor,
        Boats = Boats * factor,
        Aircraft = Aircraft * factor,
        Water = Water * factor,
        Tools = Tools * factor,
        Medicine = Medicine * factor,
        Ammunition = Ammunition * factor,
    };

    /// <summary>返回所有资源数量限制在零至指定上限内的库存。</summary>
    /// <param name="maximum">每类资源的数量上限。</param>
    public ResourceStock Clamp(double maximum) => new()
    {
        Food = Math.Clamp(Food, 0, maximum),
        Wood = Math.Clamp(Wood, 0, maximum),
        Stone = Math.Clamp(Stone, 0, maximum),
        Ore = Math.Clamp(Ore, 0, maximum),
        Alloy = Math.Clamp(Alloy, 0, maximum),
        EnergyCells = Math.Clamp(EnergyCells, 0, maximum),
        Crystals = Math.Clamp(Crystals, 0, maximum),
        Coal = Math.Clamp(Coal, 0, maximum),
        Oil = Math.Clamp(Oil, 0, maximum),
        RareEarth = Math.Clamp(RareEarth, 0, maximum),
        Boats = Math.Clamp(Boats, 0, maximum),
        Aircraft = Math.Clamp(Aircraft, 0, maximum),
        Water = Math.Clamp(Water, 0, maximum),
        Tools = Math.Clamp(Tools, 0, maximum),
        Medicine = Math.Clamp(Medicine, 0, maximum),
        Ammunition = Math.Clamp(Ammunition, 0, maximum),
    };

    /// <summary>返回资源种类的中文名称。</summary>
    /// <param name="kind">资源种类。</param>
    public static string Name(ResourceKind kind)
    {
        return kind switch
        {
            ResourceKind.Food => "粮食",
            ResourceKind.Wood => "木材",
            ResourceKind.Stone => "石材",
            ResourceKind.Ore => "矿石",
            ResourceKind.Alloy => "合金",
            ResourceKind.EnergyCells => "动力单元",
            ResourceKind.Crystals => "魔晶",
            ResourceKind.Coal => "煤",
            ResourceKind.Oil => "石油",
            ResourceKind.RareEarth => "稀土",
            ResourceKind.Boats => "舟船",
            ResourceKind.Aircraft => "运输机",
            ResourceKind.Water => "饮水",
            ResourceKind.Tools => "工具",
            ResourceKind.Medicine => "药品",
            ResourceKind.Ammunition => "弹药",
            _ => kind.ToString(),
        };
    }

    /// <summary>将资源数量格式化为摘要。</summary>
    /// <param name="stock">配方或成本所需的资源数量。</param>
    public static string Format(ResourceAmounts stock)
    {
        return string.Join("   ", Kinds.Where(k => stock.Get(k) > 0)
            .Select(k => $"{Name(k)} {stock.Get(k):0.#}"));
    }

    /// <summary>将库存中数量大于零的资源格式化为摘要。</summary>
    /// <param name="stock">当前资源库存。</param>
    public static string Format(ResourceStock stock)
    {
        return string.Join("   ", Kinds.Where(k => stock.Get(k) > 0)
            .Select(k => $"{Name(k)} {stock.Get(k):0.#}"));
    }
}
