using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

public sealed partial class ResourceStock
{
    /// <summary>饮水的资源数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Water { get; set; }

    /// <summary>合金的资源数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Alloy { get; set; }

    /// <summary>动力单元的资源数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double EnergyCells { get; set; }

    /// <summary>魔晶的资源数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Crystals { get; set; }

    /// <summary>煤的资源数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Coal { get; set; }

    /// <summary>石油的资源数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Oil { get; set; }

    /// <summary>稀土的资源数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double RareEarth { get; set; }

    /// <summary>舟船的资源数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Boats { get; set; }

    /// <summary>运输机的资源数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Aircraft { get; set; }

    /// <summary>工具的资源数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Tools { get; set; }

    /// <summary>药品的资源数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Medicine { get; set; }

    /// <summary>弹药的资源数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Ammunition { get; set; }

    /// <summary>读取指定种类的资源数量。</summary>
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

    /// <summary>替换指定种类的资源数量。</summary>
    /// <param name="kind">资源种类。</param>
    /// <param name="value">要替换的资源数量。</param>
    public void Set(ResourceKind kind, double value)
    {
        switch (kind)
        {
            case ResourceKind.Food: Food = value; break;
            case ResourceKind.Wood: Wood = value; break;
            case ResourceKind.Stone: Stone = value; break;
            case ResourceKind.Ore: Ore = value; break;
            case ResourceKind.Alloy: Alloy = value; break;
            case ResourceKind.EnergyCells: EnergyCells = value; break;
            case ResourceKind.Crystals: Crystals = value; break;
            case ResourceKind.Coal: Coal = value; break;
            case ResourceKind.Oil: Oil = value; break;
            case ResourceKind.RareEarth: RareEarth = value; break;
            case ResourceKind.Boats: Boats = value; break;
            case ResourceKind.Aircraft: Aircraft = value; break;
            case ResourceKind.Water: Water = value; break;
            case ResourceKind.Tools: Tools = value; break;
            case ResourceKind.Medicine: Medicine = value; break;
            case ResourceKind.Ammunition: Ammunition = value; break;
            default: throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }

    /// <summary>复制全部资源数量，返回独立的库存对象。</summary>
    public ResourceStock Copy()
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
    /// <summary>配方和库存查询使用的全部资源种类。</summary>
    public static IReadOnlyList<ResourceKind> Kinds { get; } = Array.AsReadOnly(Enum.GetValues<ResourceKind>());

    /// <summary>将资源数量格式化为摘要。</summary>
    /// <param name="stock">配方或成本所需的资源数量。</param>
    public static string Format(ResourceAmounts stock)
    {
        return string.Join("   ", Kinds.Where(k => stock.Get(k) > 0)
            .Select(k => $"{ResourceStock.Name(k)} {stock.Get(k):0.#}"));
    }

    /// <summary>将库存中数量大于零的资源格式化为摘要。</summary>
    /// <param name="stock">当前资源库存。</param>
    public static string Format(ResourceStock stock)
    {
        return string.Join("   ", Kinds.Where(k => stock.Get(k) > 0)
            .Select(k => $"{ResourceStock.Name(k)} {stock.Get(k):0.#}"));
    }
}
