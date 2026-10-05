using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

public enum ResourceKind
{
    Food,
    Wood,
    Stone,
    Ore,
    Alloy,
    EnergyCells,
    Crystals,
    Coal,
    Oil,
    RareEarth,
    Boats,
    Aircraft,
    Water,
    Tools,
    Medicine,
    Ammunition,
}

public sealed partial class ResourceStock
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Water { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Alloy { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double EnergyCells { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Crystals { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Coal { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Oil { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double RareEarth { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Boats { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Aircraft { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Tools { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Medicine { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Ammunition { get; set; }

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
    public IReadOnlyList<ResourceKind> InputResources { get; } =
        Array.AsReadOnly(Enum.GetValues<ResourceKind>().Where(k => Input.Get(k) > 0).ToArray());
}

/// <summary>Both routes share physical production and research rules, but neither requires the other route.</summary>
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

    public static IReadOnlyList<ResourceKind> Resources { get; } = Array.AsReadOnly(Enum.GetValues<ResourceKind>());

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

    public static Advancement? For(ResearchKind kind)
    {
        return ByResearch.GetValueOrDefault(kind);
    }

    public static Advancement? For(BuildingKind kind)
    {
        return kind == BuildingKind.DwarvenForge ? DwarvenRecipe :
            kind == BuildingKind.Shipyard ? DockRecipe : ByBuilding.GetValueOrDefault(kind);
    }

    public static string Stock(ResourceStock stock)
    {
        return string.Join("   ", Resources.Where(k => stock.Get(k) > 0)
            .Select(k => $"{ResourceStock.Name(k)} {stock.Get(k):0.#}"));
    }
}
