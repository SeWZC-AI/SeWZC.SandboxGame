using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

public enum ResourceKind { Food, Wood, Stone, Ore, Alloy, EnergyCells, Crystals }

public sealed partial class ResourceStock
{
    [JsonRequired] public double Alloy { get; set; }
    [JsonRequired] public double EnergyCells { get; set; }
    [JsonRequired] public double Crystals { get; set; }

    public double Get(ResourceKind kind) => kind switch
    {
        ResourceKind.Food => Food, ResourceKind.Wood => Wood, ResourceKind.Stone => Stone,
        ResourceKind.Ore => Ore, ResourceKind.Alloy => Alloy, ResourceKind.EnergyCells => EnergyCells,
        ResourceKind.Crystals => Crystals, _ => throw new ArgumentOutOfRangeException(nameof(kind))
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
            default: throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }
    public ResourceStock Copy() => new() { Food = Food, Wood = Wood, Stone = Stone, Ore = Ore,
        Alloy = Alloy, EnergyCells = EnergyCells, Crystals = Crystals };
    public static string Name(ResourceKind kind) => kind switch
    {
        ResourceKind.Food => "粮食", ResourceKind.Wood => "木材", ResourceKind.Stone => "石材",
        ResourceKind.Ore => "矿石", ResourceKind.Alloy => "合金", ResourceKind.EnergyCells => "动力单元",
        ResourceKind.Crystals => "魔晶", _ => kind.ToString()
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
            new() { Wood = 25, Stone = 30, Ore = 15 }, new() { Wood = 1, Ore = 2 }, ResourceKind.Alloy, 1),
        new(ResearchKind.Electrification, "电气化", "近现代", false, [ResearchKind.Industry, ResearchKind.SignalNetwork],
            new() { Food = 40, Alloy = 12, Ore = 15 }, BuildingKind.PowerPlant, "动力工厂",
            new() { Stone = 25, Alloy = 10, Ore = 10 }, new() { Wood = 2, Ore = 1 }, ResourceKind.EnergyCells, 3),
        new(ResearchKind.Automation, "自动化农业", "自动化", false, [ResearchKind.Electrification],
            new() { Food = 50, Alloy = 20, EnergyCells = 20 }, BuildingKind.AutomatedFarm, "自动化农场",
            new() { Stone = 20, Alloy = 15, EnergyCells = 10 }, new() { EnergyCells = 1 }, ResourceKind.Food, 8),
        new(ResearchKind.AdvancedComputing, "先进计算与制造", "未来制造", false, [ResearchKind.Automation],
            new() { Food = 60, Alloy = 30, EnergyCells = 30 }, BuildingKind.Fabricator, "精密制造中心",
            new() { Stone = 30, Alloy = 25, EnergyCells = 20 }, new() { Ore = 2, EnergyCells = 2 }, ResourceKind.Alloy, 3),
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
    public static Advancement? For(ResearchKind kind) => All.FirstOrDefault(a => a.Research == kind);
    public static Advancement? For(BuildingKind kind) => All.FirstOrDefault(a => a.Facility == kind);
    public static string Stock(ResourceStock stock) => string.Join(" · ", Resources.Where(k => stock.Get(k) > 0)
        .Select(k => $"{ResourceStock.Name(k)} {stock.Get(k):0.#}"));
}
