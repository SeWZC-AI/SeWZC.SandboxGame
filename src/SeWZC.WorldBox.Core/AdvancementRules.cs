namespace SeWZC.WorldBox.Core;

/// <summary>科技与魔法路线的生产技术目录。</summary>
public static class AdvancementRules
{
    private static readonly IReadOnlyDictionary<ResearchKind, Advancement> ByResearch;

    private static readonly IReadOnlyDictionary<BuildingKind, Advancement> ByBuilding;

    private static readonly Advancement DockRecipe = new(ResearchKind.Logistics, "造船", "古代", false, [],
        new ResourceAmounts(),
        BuildingKind.Shipyard, "船坞", new ResourceAmounts { Wood = 35, Stone = 20 }, new ResourceAmounts { Wood = 4 },
        ResourceKind.Boats, 1);

    private static readonly Advancement DwarvenRecipe = new(ResearchKind.Industry, "矮人工艺", "工业", false,
        [ResearchKind.Agriculture, ResearchKind.Logistics], new ResourceAmounts(),
        BuildingKind.DwarvenForge, "矮人锻炉", new ResourceAmounts { Wood = 30, Stone = 40, Ore = 15 },
        new ResourceAmounts { Wood = 2, Ore = 2 }, ResourceKind.Alloy, 1.5);

    static AdvancementRules()
    {
        // 所有字段初始化完成后再建立索引，避免读取尚未赋值的 All。
        ByResearch = All.ToDictionary(a => a.Research);
        ByBuilding = All.ToDictionary(a => a.Facility);
    }

    /// <summary>配方和库存查询使用的全部资源种类。</summary>
    public static IReadOnlyList<ResourceKind> Resources { get; } = Array.AsReadOnly(Enum.GetValues<ResourceKind>());

    /// <summary>科技与魔法路线的生产技术定义。</summary>
    public static IReadOnlyList<Advancement> All { get; } = Array.AsReadOnly(new Advancement[]
    {
        new(ResearchKind.Industry, "工业冶炼", "工业", false, [ResearchKind.Agriculture, ResearchKind.Logistics],
            new ResourceAmounts { Food = 35, Wood = 25, Ore = 15 }, BuildingKind.Foundry, "冶炼厂",
            new ResourceAmounts { Wood = 25, Stone = 30, Ore = 15 }, new ResourceAmounts { Coal = 1, Ore = 2 },
            ResourceKind.Alloy, 1),
        new(ResearchKind.Electrification, "电气化", "近现代", false, [ResearchKind.Industry],
            new ResourceAmounts { Food = 40, Alloy = 12, Ore = 15 }, BuildingKind.PowerPlant, "动力工厂",
            new ResourceAmounts { Stone = 25, Alloy = 10, Ore = 10 }, new ResourceAmounts { Coal = 1, Oil = 1 },
            ResourceKind.EnergyCells, 3),
        new(ResearchKind.Aviation, "航空运输", "航空", false, [ResearchKind.Electrification, ResearchKind.SignalNetwork],
            new ResourceAmounts { Food = 40, Alloy = 15, EnergyCells = 10 }, BuildingKind.Airfield, "航空工场",
            new ResourceAmounts { Stone = 30, Alloy = 20, EnergyCells = 10 },
            new ResourceAmounts { Alloy = 3, Oil = 2, EnergyCells = 2 }, ResourceKind.Aircraft, 1),
        new(ResearchKind.Automation, "自动化农业", "自动化", false, [ResearchKind.Electrification],
            new ResourceAmounts { Food = 50, Alloy = 20, EnergyCells = 20 }, BuildingKind.AutomatedFarm, "自动化农场",
            new ResourceAmounts { Stone = 20, Alloy = 15, EnergyCells = 10 }, new ResourceAmounts { EnergyCells = 1 },
            ResourceKind.Food, 4),
        new(ResearchKind.AdvancedComputing, "先进计算与制造", "未来制造", false, [ResearchKind.Automation],
            new ResourceAmounts { Food = 60, Alloy = 30, EnergyCells = 30 }, BuildingKind.Fabricator, "精密制造中心",
            new ResourceAmounts { Stone = 30, Alloy = 25, EnergyCells = 20 },
            new ResourceAmounts { RareEarth = 1, EnergyCells = 2 }, ResourceKind.Alloy, 3),
        new(ResearchKind.Crystalcraft, "晶体凝炼", "晶体魔法", true, [ResearchKind.ArcaneArts],
            new ResourceAmounts { Food = 30, Stone = 15, Ore = 12 }, BuildingKind.Crystallizer, "魔晶凝炼室",
            new ResourceAmounts { Wood = 15, Stone = 25, Ore = 12 }, new ResourceAmounts { Ore = 1 },
            ResourceKind.Crystals,
            1, 4),
        new(ResearchKind.RunicEngineering, "符文生产", "符文文明", true, [ResearchKind.Crystalcraft, ResearchKind.Logistics],
            new ResourceAmounts { Food = 40, Stone = 20, Crystals = 12 }, BuildingKind.RunicGarden, "符文温室",
            new ResourceAmounts { Wood = 20, Stone = 20, Crystals = 10 }, new ResourceAmounts { Crystals = 1 },
            ResourceKind.Food, 4, 2),
        new(ResearchKind.AetherMastery, "高阶以太工艺", "以太文明", true, [ResearchKind.RunicEngineering],
            new ResourceAmounts { Food = 60, Stone = 30, Crystals = 25 }, BuildingKind.AetherForge, "以太转化炉",
            new ResourceAmounts { Stone = 40, Ore = 15, Crystals = 20 },
            new ResourceAmounts { Stone = 2, Crystals = 2 },
            ResourceKind.Ore, 4, 8),
        new(ResearchKind.Pharmacology, "药物制备", "公共卫生", false, [ResearchKind.Medicine, ResearchKind.Education],
            new ResourceAmounts { Food = 35, Wood = 15, Ore = 5 }, BuildingKind.Apothecary, "药房",
            new ResourceAmounts { Wood = 20, Stone = 15 }, new ResourceAmounts { Food = 1, Wood = 1 },
            ResourceKind.Medicine, 3),
        new(ResearchKind.Toolmaking, "机械工具", "机械工程", false,
            [ResearchKind.MechanicalEngineering, ResearchKind.EfficientSmelting],
            new ResourceAmounts { Food = 35, Alloy = 8 }, BuildingKind.MachineWorkshop, "机械工场",
            new ResourceAmounts { Stone = 20, Alloy = 10 }, new ResourceAmounts { Alloy = 1, Wood = 1 },
            ResourceKind.Tools,
            4),
        new(ResearchKind.Ballistics, "弹道学", "工程军备", false, [ResearchKind.Industry, ResearchKind.Cartography],
            new ResourceAmounts { Food = 35, Alloy = 8, Coal = 5 }, BuildingKind.Arsenal, "军械厂",
            new ResourceAmounts { Wood = 20, Stone = 20, Alloy = 8 }, new ResourceAmounts { Alloy = 1, Coal = 1 },
            ResourceKind.Ammunition, 8),
        new(ResearchKind.Alchemy, "炼金药剂", "奥术与修复", true, [ResearchKind.Crystalcraft, ResearchKind.Pharmacology],
            new ResourceAmounts { Food = 35, Crystals = 8 }, BuildingKind.AlchemyLab, "炼金实验室",
            new ResourceAmounts { Stone = 25, Crystals = 8 }, new ResourceAmounts { Food = 1, Crystals = .5 },
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

    /// <summary>将资源数量格式化为摘要。</summary>
    /// <param name="stock">配方或成本所需的资源数量。</param>
    public static string Stock(ResourceAmounts stock)
    {
        return string.Join("   ", Resources.Where(k => stock.Get(k) > 0)
            .Select(k => $"{ResourceStock.Name(k)} {stock.Get(k):0.#}"));
    }

    /// <summary>将库存中数量大于零的资源格式化为摘要。</summary>
    /// <param name="stock">当前资源库存。</param>
    public static string Stock(ResourceStock stock)
    {
        return string.Join("   ", Resources.Where(k => stock.Get(k) > 0)
            .Select(k => $"{ResourceStock.Name(k)} {stock.Get(k):0.#}"));
    }
}
