namespace SeWZC.WorldBox.Core;

/// <summary>科技与魔法路线的生产技术目录。</summary>
public static class ProductionRules
{
    private static readonly IReadOnlyDictionary<Advancement, ProductionRecipe> ByResearch;

    private static readonly IReadOnlyDictionary<BuildingKind, ProductionRecipe> ByBuilding;

    private static readonly ProductionRecipe ShipyardRecipe = new(Advancement.Logistics,
        BuildingKind.Shipyard,
        "船坞",
        new ResourceAmounts { Wood = 35, Stone = 20 },
        new ResourceAmounts { Wood = 4 },
        ResourceKind.Boats,
        1);

    private static readonly ProductionRecipe DwarvenForgeRecipe = new(Advancement.Industry,
        BuildingKind.DwarvenForge,
        "矮人锻炉",
        new ResourceAmounts { Wood = 30, Stone = 40, Ore = 15 },
        new ResourceAmounts { Wood = 2, Ore = 2 },
        ResourceKind.Alloy,
        1.5);

    static ProductionRules()
    {
        // 所有字段初始化完成后再建立索引，避免读取尚未赋值的 All。
        ByResearch = All.ToDictionary(recipe => recipe.Research);
        ByBuilding = All.Append(ShipyardRecipe).Append(DwarvenForgeRecipe)
            .ToDictionary(recipe => recipe.Facility);
    }

    /// <summary>科技与魔法路线的生产技术定义。</summary>
    public static IReadOnlyList<ProductionRecipe> All { get; } = Array.AsReadOnly(new ProductionRecipe[]
    {
        new(Advancement.Industry,
            BuildingKind.Foundry,
            "冶炼厂",
            new ResourceAmounts { Wood = 25, Stone = 30, Ore = 15 },
            new ResourceAmounts { Coal = 1, Ore = 2 },
            ResourceKind.Alloy,
            1),
        new(Advancement.Electrification,
            BuildingKind.PowerPlant,
            "动力工厂",
            new ResourceAmounts { Stone = 25, Alloy = 10, Ore = 10 },
            new ResourceAmounts { Coal = 1, Oil = 1 },
            ResourceKind.EnergyCells,
            3),
        new(Advancement.Aviation,
            BuildingKind.Airfield,
            "航空工场",
            new ResourceAmounts { Stone = 30, Alloy = 20, EnergyCells = 10 },
            new ResourceAmounts { Alloy = 3, Oil = 2, EnergyCells = 2 },
            ResourceKind.Aircraft,
            1),
        new(Advancement.Automation,
            BuildingKind.AutomatedFarm,
            "自动化农场",
            new ResourceAmounts { Stone = 20, Alloy = 15, EnergyCells = 10 },
            new ResourceAmounts { EnergyCells = 1 },
            ResourceKind.Food,
            4),
        new(Advancement.AdvancedComputing,
            BuildingKind.Fabricator,
            "精密制造中心",
            new ResourceAmounts { Stone = 30, Alloy = 25, EnergyCells = 20 },
            new ResourceAmounts { RareEarth = 1, EnergyCells = 2 },
            ResourceKind.Alloy,
            3),
        new(Advancement.Crystalcraft,
            BuildingKind.Crystallizer,
            "魔晶凝炼室",
            new ResourceAmounts { Wood = 15, Stone = 25, Ore = 12 },
            new ResourceAmounts { Ore = 1 },
            ResourceKind.Crystals,
            1,
            4),
        new(Advancement.RunicEngineering,
            BuildingKind.RunicGarden,
            "符文温室",
            new ResourceAmounts { Wood = 20, Stone = 20, Crystals = 10 },
            new ResourceAmounts { Crystals = 1 },
            ResourceKind.Food,
            4,
            2),
        new(Advancement.AetherMastery,
            BuildingKind.AetherForge,
            "以太转化炉",
            new ResourceAmounts { Stone = 40, Ore = 15, Crystals = 20 },
            new ResourceAmounts { Stone = 2, Crystals = 2 },
            ResourceKind.Ore,
            4,
            8),
        new(Advancement.Pharmacology,
            BuildingKind.Apothecary,
            "药房",
            new ResourceAmounts { Wood = 20, Stone = 15 },
            new ResourceAmounts { Food = 1, Wood = 1 },
            ResourceKind.Medicine,
            3),
        new(Advancement.Toolmaking,
            BuildingKind.MachineWorkshop,
            "机械工场",
            new ResourceAmounts { Stone = 20, Alloy = 10 },
            new ResourceAmounts { Alloy = 1, Wood = 1 },
            ResourceKind.Tools,
            4),
        new(Advancement.Ballistics,
            BuildingKind.Arsenal,
            "军械厂",
            new ResourceAmounts { Wood = 20, Stone = 20, Alloy = 8 },
            new ResourceAmounts { Alloy = 1, Coal = 1 },
            ResourceKind.Ammunition,
            8),
        new(Advancement.Alchemy,
            BuildingKind.AlchemyLab,
            "炼金实验室",
            new ResourceAmounts { Stone = 25, Crystals = 8 },
            new ResourceAmounts { Food = 1, Crystals = .5 },
            ResourceKind.Medicine,
            6,
            4),
    });

    /// <summary>查找指定研究对应的生产配方，没有配方时返回空值。</summary>
    /// <param name="research">研究项目。</param>
    public static ProductionRecipe? For(Advancement research)
    {
        return ByResearch.GetValueOrDefault(research);
    }

    /// <summary>查找指定设施对应的生产配方，没有配方时返回空值。</summary>
    /// <param name="kind">设施类别。</param>
    public static ProductionRecipe? For(BuildingKind kind)
    {
        return ByBuilding.GetValueOrDefault(kind);
    }
}
