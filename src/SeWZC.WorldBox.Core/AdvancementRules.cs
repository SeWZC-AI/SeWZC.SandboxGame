using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

/// <summary>仓库、随身货物和生产配方使用的资源种类。</summary>
public enum ResourceKind
{
    /// <summary>粮食。</summary>
    Food,
    /// <summary>木材。</summary>
    Wood,
    /// <summary>石材。</summary>
    Stone,
    /// <summary>矿石。</summary>
    Ore,
    /// <summary>合金。</summary>
    Alloy,
    /// <summary>动力单元。</summary>
    EnergyCells,
    /// <summary>魔晶。</summary>
    Crystals,
    /// <summary>煤。</summary>
    Coal,
    /// <summary>石油。</summary>
    Oil,
    /// <summary>稀土。</summary>
    RareEarth,
    /// <summary>舟船。</summary>
    Boats,
    /// <summary>运输机。</summary>
    Aircraft,
    /// <summary>饮水。</summary>
    Water,
    /// <summary>工具。</summary>
    Tools,
    /// <summary>药品。</summary>
    Medicine,
    /// <summary>弹药。</summary>
    Ammunition,
}

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
            ResourceKind.Water => Water, ResourceKind.Food => Food, ResourceKind.Wood => Wood,
            ResourceKind.Stone => Stone,
            ResourceKind.Ore => Ore, ResourceKind.Alloy => Alloy, ResourceKind.EnergyCells => EnergyCells,
            ResourceKind.Crystals => Crystals, ResourceKind.Coal => Coal, ResourceKind.Oil => Oil,
            ResourceKind.RareEarth => RareEarth, ResourceKind.Boats => Boats, ResourceKind.Aircraft => Aircraft,
            ResourceKind.Tools => Tools, ResourceKind.Medicine => Medicine, ResourceKind.Ammunition => Ammunition,
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
            case ResourceKind.Water: Water = value; break;
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
            Water = Water, Food = Food, Wood = Wood, Stone = Stone, Ore = Ore,
            Alloy = Alloy, EnergyCells = EnergyCells, Crystals = Crystals, Coal = Coal, Oil = Oil,
            RareEarth = RareEarth, Boats = Boats, Aircraft = Aircraft,
            Tools = Tools, Medicine = Medicine, Ammunition = Ammunition,
        };
    }

    /// <summary>返回资源种类的中文名称。</summary>
    /// <param name="kind">资源种类。</param>
    public static string Name(ResourceKind kind)
    {
        return kind switch
        {
            ResourceKind.Water => "饮水", ResourceKind.Food => "粮食", ResourceKind.Wood => "木材",
            ResourceKind.Stone => "石材",
            ResourceKind.Ore => "矿石", ResourceKind.Alloy => "合金", ResourceKind.EnergyCells => "动力单元",
            ResourceKind.Crystals => "魔晶", ResourceKind.Coal => "煤", ResourceKind.Oil => "石油",
            ResourceKind.RareEarth => "稀土", ResourceKind.Boats => "舟船", ResourceKind.Aircraft => "运输机",
            ResourceKind.Tools => "工具", ResourceKind.Medicine => "药品", ResourceKind.Ammunition => "弹药",
            _ => kind.ToString(),
        };
    }
}

/// <summary>生产技术的研究要求、设施成本、每批原料与产出，以及可选的魔力消耗。</summary>
/// <param name="Research">解锁该生产技术的研究项目。</param>
/// <param name="Name">生产技术名称。</param>
/// <param name="Stage">所属发展阶段名称。</param>
/// <param name="Magic">该配方是否属于魔法路线。</param>
/// <param name="Prerequisites">除本研究外须掌握的前置研究。</param>
/// <param name="ResearchCost">启动对应研究所需的资源成本。</param>
/// <param name="Facility">执行该配方的设施类别。</param>
/// <param name="FacilityName">生产设施的显示名称。</param>
/// <param name="BuildingCost">建造该设施所需的资源成本。</param>
/// <param name="Input">每批生产实际消耗的原料数量。</param>
/// <param name="Output">生产得到的资源种类。</param>
/// <param name="Yield">每批生产的基础产出数量。</param>
/// <param name="Mana">每批生产需要消耗的魔力，0 表示不消耗魔力。</param>
public sealed record Advancement(
    ResearchKind Research,
    string Name,
    string Stage,
    bool Magic,
    ResearchKind[] Prerequisites,
    ResourceStock ResearchCost,
    BuildingKind Facility,
    string FacilityName,
    ResourceStock BuildingCost,
    ResourceStock Input,
    ResourceKind Output,
    double Yield,
    double Mana = 0)
{
    /// <summary>该配方需要数量大于零的原料种类。</summary>
    public IReadOnlyList<ResourceKind> InputResources { get; } =
        Array.AsReadOnly(Enum.GetValues<ResourceKind>().Where(k => Input.Get(k) > 0).ToArray());
}

/// <summary>查询科技与魔法路线的研究及实体生产配方，两条路线可以独立发展。</summary>
public static class AdvancementRules
{
    private static readonly IReadOnlyDictionary<ResearchKind, Advancement> ByResearch =
        All.ToDictionary(a => a.Research);

    private static readonly IReadOnlyDictionary<BuildingKind, Advancement> ByBuilding =
        All.ToDictionary(a => a.Facility);

    private static readonly Advancement DockRecipe = new(ResearchKind.Logistics, "造船", "古代", false, [],
        new ResourceStock(),
        BuildingKind.Shipyard, "船坞", new ResourceStock { Wood = 35, Stone = 20 }, new ResourceStock { Wood = 4 },
        ResourceKind.Boats, 1);

    private static readonly Advancement DwarvenRecipe = new(ResearchKind.Industry, "矮人工艺", "工业", false,
        [ResearchKind.Agriculture, ResearchKind.Logistics], new ResourceStock(),
        BuildingKind.DwarvenForge, "矮人锻炉", new ResourceStock { Wood = 30, Stone = 40, Ore = 15 },
        new ResourceStock { Wood = 2, Ore = 2 }, ResourceKind.Alloy, 1.5);

    /// <summary>配方和库存查询使用的全部资源种类。</summary>
    public static IReadOnlyList<ResourceKind> Resources { get; } = Array.AsReadOnly(Enum.GetValues<ResourceKind>());

    /// <summary>科技与魔法路线中的研究及设施生产配方。</summary>
    public static IReadOnlyList<Advancement> All { get; } = Array.AsReadOnly(new Advancement[]
    {
        new(ResearchKind.Industry, "工业冶炼", "工业", false, [ResearchKind.Agriculture, ResearchKind.Logistics],
            new ResourceStock { Food = 35, Wood = 25, Ore = 15 }, BuildingKind.Foundry, "冶炼厂",
            new ResourceStock { Wood = 25, Stone = 30, Ore = 15 }, new ResourceStock { Coal = 1, Ore = 2 },
            ResourceKind.Alloy, 1),
        new(ResearchKind.Electrification, "电气化", "近现代", false, [ResearchKind.Industry],
            new ResourceStock { Food = 40, Alloy = 12, Ore = 15 }, BuildingKind.PowerPlant, "动力工厂",
            new ResourceStock { Stone = 25, Alloy = 10, Ore = 10 }, new ResourceStock { Coal = 1, Oil = 1 },
            ResourceKind.EnergyCells, 3),
        new(ResearchKind.Aviation, "航空运输", "航空", false, [ResearchKind.Electrification, ResearchKind.SignalNetwork],
            new ResourceStock { Food = 40, Alloy = 15, EnergyCells = 10 }, BuildingKind.Airfield, "航空工场",
            new ResourceStock { Stone = 30, Alloy = 20, EnergyCells = 10 },
            new ResourceStock { Alloy = 3, Oil = 2, EnergyCells = 2 }, ResourceKind.Aircraft, 1),
        new(ResearchKind.Automation, "自动化农业", "自动化", false, [ResearchKind.Electrification],
            new ResourceStock { Food = 50, Alloy = 20, EnergyCells = 20 }, BuildingKind.AutomatedFarm, "自动化农场",
            new ResourceStock { Stone = 20, Alloy = 15, EnergyCells = 10 }, new ResourceStock { EnergyCells = 1 },
            ResourceKind.Food, 4),
        new(ResearchKind.AdvancedComputing, "先进计算与制造", "未来制造", false, [ResearchKind.Automation],
            new ResourceStock { Food = 60, Alloy = 30, EnergyCells = 30 }, BuildingKind.Fabricator, "精密制造中心",
            new ResourceStock { Stone = 30, Alloy = 25, EnergyCells = 20 },
            new ResourceStock { RareEarth = 1, EnergyCells = 2 }, ResourceKind.Alloy, 3),
        new(ResearchKind.Crystalcraft, "晶体凝炼", "晶体魔法", true, [ResearchKind.ArcaneArts],
            new ResourceStock { Food = 30, Stone = 15, Ore = 12 }, BuildingKind.Crystallizer, "魔晶凝炼室",
            new ResourceStock { Wood = 15, Stone = 25, Ore = 12 }, new ResourceStock { Ore = 1 }, ResourceKind.Crystals,
            1, 4),
        new(ResearchKind.RunicEngineering, "符文生产", "符文文明", true, [ResearchKind.Crystalcraft, ResearchKind.Logistics],
            new ResourceStock { Food = 40, Stone = 20, Crystals = 12 }, BuildingKind.RunicGarden, "符文温室",
            new ResourceStock { Wood = 20, Stone = 20, Crystals = 10 }, new ResourceStock { Crystals = 1 },
            ResourceKind.Food, 4, 2),
        new(ResearchKind.AetherMastery, "高阶以太工艺", "以太文明", true, [ResearchKind.RunicEngineering],
            new ResourceStock { Food = 60, Stone = 30, Crystals = 25 }, BuildingKind.AetherForge, "以太转化炉",
            new ResourceStock { Stone = 40, Ore = 15, Crystals = 20 }, new ResourceStock { Stone = 2, Crystals = 2 },
            ResourceKind.Ore, 4, 8),
        new(ResearchKind.Pharmacology, "药物制备", "公共卫生", false, [ResearchKind.Medicine, ResearchKind.Education],
            new ResourceStock { Food = 35, Wood = 15, Ore = 5 }, BuildingKind.Apothecary, "药房",
            new ResourceStock { Wood = 20, Stone = 15 }, new ResourceStock { Food = 1, Wood = 1 },
            ResourceKind.Medicine, 3),
        new(ResearchKind.Toolmaking, "机械工具", "机械工程", false,
            [ResearchKind.MechanicalEngineering, ResearchKind.EfficientSmelting],
            new ResourceStock { Food = 35, Alloy = 8 }, BuildingKind.MachineWorkshop, "机械工场",
            new ResourceStock { Stone = 20, Alloy = 10 }, new ResourceStock { Alloy = 1, Wood = 1 }, ResourceKind.Tools,
            4),
        new(ResearchKind.Ballistics, "弹道学", "工程军备", false, [ResearchKind.Industry, ResearchKind.Cartography],
            new ResourceStock { Food = 35, Alloy = 8, Coal = 5 }, BuildingKind.Arsenal, "军械厂",
            new ResourceStock { Wood = 20, Stone = 20, Alloy = 8 }, new ResourceStock { Alloy = 1, Coal = 1 },
            ResourceKind.Ammunition, 8),
        new(ResearchKind.Alchemy, "炼金药剂", "奥术与修复", true, [ResearchKind.Crystalcraft, ResearchKind.Pharmacology],
            new ResourceStock { Food = 35, Crystals = 8 }, BuildingKind.AlchemyLab, "炼金实验室",
            new ResourceStock { Stone = 25, Crystals = 8 }, new ResourceStock { Food = 1, Crystals = .5 },
            ResourceKind.Medicine, 6, 4),
    });

    /// <summary>查找指定研究对应的生产配方，没有配方时返回空值。</summary>
    /// <param name="kind">研究项目。</param>
    public static Advancement? For(ResearchKind kind)
    {
        return ByResearch.GetValueOrDefault(kind);
    }

    /// <summary>查找指定设施对应的生产配方，没有配方时返回空值。</summary>
    /// <param name="kind">设施类别。</param>
    public static Advancement? For(BuildingKind kind)
    {
        return kind == BuildingKind.DwarvenForge ? DwarvenRecipe :
            kind == BuildingKind.Shipyard ? DockRecipe : ByBuilding.GetValueOrDefault(kind);
    }

    /// <summary>将库存中数量大于零的资源格式化为摘要。</summary>
    /// <param name="stock">当前资源库存。</param>
    public static string Stock(ResourceStock stock)
    {
        return string.Join("   ", Resources.Where(k => stock.Get(k) > 0)
            .Select(k => $"{ResourceStock.Name(k)} {stock.Get(k):0.#}"));
    }
}
