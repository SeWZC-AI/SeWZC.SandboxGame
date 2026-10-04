namespace SeWZC.WorldBox.Core;

public sealed record ResearchDefinition(ResearchKind Kind, string Name, string Branch, string Stage,
    bool Magic, ResearchKind[] Prerequisites, ResourceStock Cost, double Work, string Effect);

/// <summary>The tree, commands and autonomous planner use the same knowledge graph.</summary>
public static class ResearchRules
{
    public static IReadOnlyList<ResearchDefinition> All { get; } = Array.AsReadOnly(new[]
    {
        new ResearchDefinition(ResearchKind.Agriculture, "农业改良", "民生与资源", "基础", false, [], new() { Food = 20, Wood = 15 }, 60, "农场实际采收增加 35%。"),
        new(ResearchKind.Logistics, "驿路运输", "民生与资源", "基础", false, [], new() { Food = 20, Wood = 20, Stone = 10 }, 60, "解锁驿站、桥梁、山路、船坞与码头。"),
        new(ResearchKind.Irrigation, "灌溉农学", "民生与资源", "专业", false, [ResearchKind.Agriculture], new() { Food = 25, Wood = 15, Stone = 10 }, 90, "农场及进阶粮食设施实际产量增加 25%；仍受当地肥力与干旱约束。"),
        new(ResearchKind.Forestry, "林业与勘采", "民生与资源", "专业", false, [ResearchKind.Logistics], new() { Food = 25, Wood = 10, Stone = 10 }, 90, "实地采集木材、石材、矿石与阶段矿藏的效率增加 25%。"),
        new(ResearchKind.Medicine, "公共医学", "民生与资源", "专业", false, [ResearchKind.Agriculture], new() { Food = 30, Wood = 10, Ore = 5 }, 90, "医务所现场治疗效率增加 50%。"),
        new(ResearchKind.ScientificMethod, "实验科学", "工业与能源", "启蒙", false, [ResearchKind.Agriculture, ResearchKind.Logistics], new() { Food = 30, Wood = 15, Ore = 8 }, 100, "当地学舍实际研究效率增加 25%。"),
        From(ResearchKind.Industry, "工业与能源"),
        new(ResearchKind.EfficientSmelting, "精炼冶金", "工业与能源", "工业", false, [ResearchKind.Industry], new() { Food = 35, Alloy = 5, Ore = 10 }, 120, "冶炼厂与矮人锻炉每批合金产量增加 25%，原料仍须实际运输。"),
        From(ResearchKind.Electrification, "工业与能源"),
        new(ResearchKind.EnergyRecycling, "能源循环", "工业与能源", "近现代", false, [ResearchKind.Electrification], new() { Food = 35, Alloy = 8, EnergyCells = 8 }, 120, "动力工厂每批动力单元产量增加 50%。"),
        new(ResearchKind.SignalNetwork, "信号网络", "运输与计算", "近现代", false, [ResearchKind.Electrification], new() { Food = 25, Alloy = 8, EnergyCells = 6 }, 100, "解锁无线信号塔通信，仍须实际值守与视线连通。"),
        From(ResearchKind.Aviation, "运输与计算"),
        From(ResearchKind.Automation, "运输与计算"),
        From(ResearchKind.AdvancedComputing, "运输与计算"),
        new(ResearchKind.ArcaneArts, "奥术基础", "奥术与修复", "基础奥术", true, [], new() { Food = 25, Wood = 15, Ore = 8 }, 100, "解锁奥术圣所训练，合格施法者可使用治疗、丰饶、护盾与火花。"),
        new(ResearchKind.ManaAttunement, "魔力调谐", "奥术与修复", "奥术", true, [ResearchKind.ArcaneArts], new() { Food = 30, Stone = 10, Ore = 5 }, 100, "当地居民实际魔力恢复速度增加 50%。"),
        new(ResearchKind.Restoration, "生命修复", "奥术与修复", "奥术", true, [ResearchKind.ManaAttunement, ResearchKind.Medicine], new() { Food = 35, Stone = 15, Ore = 8 }, 120, "治疗法术的实际生命恢复增加 50%；消耗个人魔力并受距离约束。"),
        From(ResearchKind.Crystalcraft, "符文与以太"),
        new(ResearchKind.ArcaneScholarship, "奥术传承", "奥术与修复", "晶体魔法", true, [ResearchKind.Crystalcraft], new() { Food = 35, Stone = 15, Crystals = 5 }, 120, "当地研究效率增加 25%，圣所训练效率增加 50%。"),
        From(ResearchKind.RunicEngineering, "符文与以太"),
        new(ResearchKind.Leylines, "地脉共鸣", "符文与以太", "符文文明", true, [ResearchKind.RunicEngineering, ResearchKind.ManaAttunement], new() { Food = 40, Stone = 20, Crystals = 10 }, 150, "魔晶凝炼室及以太转化炉每批实际产量增加 25%。"),
        From(ResearchKind.AetherMastery, "符文与以太"),
        new(ResearchKind.TechnologicalEmpire, "科技帝国", "帝国成果", "科技帝国", false,
            [ResearchKind.AdvancedComputing, ResearchKind.Aviation, ResearchKind.EfficientSmelting, ResearchKind.EnergyRecycling, ResearchKind.ScientificMethod, ResearchKind.Irrigation, ResearchKind.Forestry, ResearchKind.Medicine],
            new() { Food = 60, Alloy = 30, EnergyCells = 30, RareEarth = 5 }, 240, "科技生产设施每批实际产量增加 25%；文明达到科技帝国阶段。"),
        new(ResearchKind.MagicalEmpire, "魔法帝国", "帝国成果", "魔法帝国", true,
            [ResearchKind.AetherMastery, ResearchKind.Leylines, ResearchKind.ArcaneScholarship, ResearchKind.Restoration, ResearchKind.Irrigation, ResearchKind.Forestry],
            new() { Food = 60, Stone = 30, Crystals = 30 }, 240, "魔法生产设施每批实际产量增加 25%；文明达到魔法帝国阶段。")
    });
    private static readonly IReadOnlyDictionary<ResearchKind, ResearchDefinition> ByKind = All.ToDictionary(r => r.Kind);
    public static ResearchDefinition For(ResearchKind kind) => ByKind.TryGetValue(kind, out var value) ? value : throw new ArgumentOutOfRangeException(nameof(kind));
    public static IReadOnlyList<ResearchKind> Route(bool magic) => All.Where(r => r.Magic == magic || r.Branch == "民生与资源").Select(r => r.Kind).ToArray();
    private static ResearchDefinition From(ResearchKind kind, string branch)
    {
        var a = AdvancementRules.For(kind)!;
        return new(kind, a.Name, branch, a.Stage, a.Magic, a.Prerequisites, a.ResearchCost, 180,
            "解锁" + a.FacilityName + "。\n" + WorldEngine.ProductionRecipe(a.Facility));
    }
}
