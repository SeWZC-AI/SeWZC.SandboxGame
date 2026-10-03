using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

public enum ResourceKind { Food, Wood, Stone, Ore, Alloy, EnergyCells, Crystals, Coal, Oil, RareEarth, Boats, Aircraft }

public sealed partial class ResourceStock
{
    [JsonRequired] public double Alloy { get; set; }
    [JsonRequired] public double EnergyCells { get; set; }
    [JsonRequired] public double Crystals { get; set; }

    [JsonRequired] public double Coal { get; set; }
    [JsonRequired] public double Oil { get; set; }
    [JsonRequired] public double RareEarth { get; set; }
    [JsonRequired] public double Boats { get; set; }
    [JsonRequired] public double Aircraft { get; set; }

    public double Get(ResourceKind kind) => kind switch
    {
        ResourceKind.Food => Food, ResourceKind.Wood => Wood, ResourceKind.Stone => Stone,
        ResourceKind.Ore => Ore, ResourceKind.Alloy => Alloy, ResourceKind.EnergyCells => EnergyCells,
        ResourceKind.Crystals => Crystals, ResourceKind.Coal => Coal, ResourceKind.Oil => Oil,
        ResourceKind.RareEarth => RareEarth, ResourceKind.Boats => Boats, ResourceKind.Aircraft => Aircraft, _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
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
            default: throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }
    public ResourceStock Copy() => new() { Food = Food, Wood = Wood, Stone = Stone, Ore = Ore,
        Alloy = Alloy, EnergyCells = EnergyCells, Crystals = Crystals, Coal = Coal, Oil = Oil, RareEarth = RareEarth, Boats = Boats, Aircraft = Aircraft };
    public static string Name(ResourceKind kind) => kind switch
    {
        ResourceKind.Food => "粮食", ResourceKind.Wood => "木材", ResourceKind.Stone => "石材",
        ResourceKind.Ore => "矿石", ResourceKind.Alloy => "合金", ResourceKind.EnergyCells => "动力单元",
        ResourceKind.Crystals => "魔晶", ResourceKind.Coal => "煤", ResourceKind.Oil => "石油",
        ResourceKind.RareEarth => "稀土", ResourceKind.Boats => "舟船", ResourceKind.Aircraft => "运输机", _ => kind.ToString()
    };
}

public sealed record Advancement(ResearchKind Research, string Name, string Stage, bool Magic,
    ResearchKind[] Prerequisites, ResourceStock ResearchCost, BuildingKind Facility, string FacilityName,
    ResourceStock BuildingCost, ResourceStock Input, ResourceKind Output, double Yield, double Mana = 0);

/// <summary>Both routes share physical production and research rules, but neither requires the other route.</summary>
public static class AdvancementRules
{
    public static IReadOnlyList<ResourceKind> Resources { get; } = Array.AsReadOnly(Enum.GetValues<ResourceKind>());
    public static IReadOnlyList<Advancement> All { get; } = Array.AsReadOnly(new Advancement[]
    {
        new(ResearchKind.Industry, "工业冶炼", "工业", false, [ResearchKind.Agriculture, ResearchKind.Logistics],
            new() { Food = 35, Wood = 25, Ore = 15 }, BuildingKind.Foundry, "冶炼厂",
            new() { Wood = 25, Stone = 30, Ore = 15 }, new() { Coal = 1, Ore = 2 }, ResourceKind.Alloy, 1),
        new(ResearchKind.Electrification, "电气化", "近现代", false, [ResearchKind.Industry],
            new() { Food = 40, Alloy = 12, Ore = 15 }, BuildingKind.PowerPlant, "动力工厂",
            new() { Stone = 25, Alloy = 10, Ore = 10 }, new() { Coal = 1, Oil = 1 }, ResourceKind.EnergyCells, 3),
        new(ResearchKind.Aviation, "航空运输", "航空", false, [ResearchKind.Electrification, ResearchKind.SignalNetwork],
            new() { Food = 40, Alloy = 15, EnergyCells = 10 }, BuildingKind.Airfield, "航空工场",
            new() { Stone = 30, Alloy = 20, EnergyCells = 10 }, new() { Alloy = 3, Oil = 2, EnergyCells = 2 }, ResourceKind.Aircraft, 1),
        new(ResearchKind.Automation, "自动化农业", "自动化", false, [ResearchKind.Electrification],
            new() { Food = 50, Alloy = 20, EnergyCells = 20 }, BuildingKind.AutomatedFarm, "自动化农场",
            new() { Stone = 20, Alloy = 15, EnergyCells = 10 }, new() { EnergyCells = 1 }, ResourceKind.Food, 8),
        new(ResearchKind.AdvancedComputing, "先进计算与制造", "未来制造", false, [ResearchKind.Automation],
            new() { Food = 60, Alloy = 30, EnergyCells = 30 }, BuildingKind.Fabricator, "精密制造中心",
            new() { Stone = 30, Alloy = 25, EnergyCells = 20 }, new() { RareEarth = 1, EnergyCells = 2 }, ResourceKind.Alloy, 3),
        new(ResearchKind.Crystalcraft, "晶体凝炼", "晶体魔法", true, [ResearchKind.ArcaneArts],
            new() { Food = 30, Stone = 15, Ore = 12 }, BuildingKind.Crystallizer, "魔晶凝炼室",
            new() { Wood = 15, Stone = 25, Ore = 12 }, new() { Ore = 1 }, ResourceKind.Crystals, 1, 4),
        new(ResearchKind.RunicEngineering, "符文生产", "符文文明", true, [ResearchKind.Crystalcraft, ResearchKind.Logistics],
            new() { Food = 40, Stone = 20, Crystals = 12 }, BuildingKind.RunicGarden, "符文温室",
            new() { Wood = 20, Stone = 20, Crystals = 10 }, new() { Crystals = 1 }, ResourceKind.Food, 8, 2),
        new(ResearchKind.AetherMastery, "高阶以太工艺", "以太文明", true, [ResearchKind.RunicEngineering],
            new() { Food = 60, Stone = 30, Crystals = 25 }, BuildingKind.AetherForge, "以太转化炉",
            new() { Stone = 40, Ore = 15, Crystals = 20 }, new() { Stone = 2, Crystals = 2 }, ResourceKind.Ore, 4, 8)
    });
    private static readonly IReadOnlyDictionary<ResearchKind, Advancement> ByResearch = All.ToDictionary(a => a.Research);
    private static readonly IReadOnlyDictionary<BuildingKind, Advancement> ByBuilding = All.ToDictionary(a => a.Facility);
    public static Advancement? For(ResearchKind kind) => ByResearch.GetValueOrDefault(kind);
    private static readonly Advancement DockRecipe = new(ResearchKind.Logistics, "造船", "古代", false, [], new(),
        BuildingKind.Dock, "船坞码头", new() { Wood = 25, Stone = 10 }, new() { Wood = 4 }, ResourceKind.Boats, 1);
    public static Advancement? For(BuildingKind kind) => kind == BuildingKind.Dock ? DockRecipe : ByBuilding.GetValueOrDefault(kind);
    public static string Stock(ResourceStock stock) => string.Join("   ", Resources.Where(k => stock.Get(k) > 0)
        .Select(k => $"{ResourceStock.Name(k)} {stock.Get(k):0.#}"));
}
