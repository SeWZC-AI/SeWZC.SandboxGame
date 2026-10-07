using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

/// <summary>聚落可掌握的研究知识，包含前置依赖、成本和解锁效果。</summary>
[JsonConverter(typeof(AdvancementJsonConverter))]
public sealed partial class Advancement
{
    private readonly string? _effect;

    private Advancement(
        int id,
        string key,
        string name,
        ResearchBranch branch,
        string stage,
        bool magic,
        IReadOnlyList<Advancement> prerequisites,
        ResourceAmounts cost,
        double work,
        string? effect,
        IReadOnlyList<BuildingKind>? buildings = null,
        IReadOnlyList<Profession>? professions = null,
        IReadOnlyList<SpellKind>? spells = null,
        ResearchAction? action = null)
    {
        Id = id;
        Key = key;
        Name = name;
        Branch = branch;
        Stage = stage;
        Magic = magic;
        Prerequisites = prerequisites.ToImmutableArray();
        Cost = cost;
        Work = work;
        _effect = effect;
        UnlockedBuildings = buildings?.ToImmutableArray() ?? [];
        UnlockedProfessions = professions?.ToImmutableArray() ?? [];
        UnlockedSpells = spells?.ToImmutableArray() ?? [];
        Action = action;
    }

    /// <summary>研究在存档、知识消息和位索引中的编号。</summary>
    public int Id { get; }

    /// <summary>界面自动化和诊断输出使用的标识。</summary>
    public string Key { get; }

    /// <summary>研究显示名称。</summary>
    public string Name { get; }

    /// <summary>研究所属分支。</summary>
    public ResearchBranch Branch { get; }

    /// <summary>所属发展阶段名称。</summary>
    public string Stage { get; }

    /// <summary>是否属于魔法路线。</summary>
    public bool Magic { get; }

    /// <summary>开始或使用该研究所需的前置知识。</summary>
    public ImmutableArray<Advancement> Prerequisites { get; }

    /// <summary>开始研究时投入的资源成本。</summary>
    public ResourceAmounts Cost { get; }

    /// <summary>完成研究需要的总工作量。</summary>
    public double Work { get; }

    /// <summary>解锁的操作入口。</summary>
    public ResearchAction? Action { get; }

    /// <summary>解锁的建筑类型。</summary>
    public ImmutableArray<BuildingKind> UnlockedBuildings { get; }

    /// <summary>解锁的职业。</summary>
    public ImmutableArray<Profession> UnlockedProfessions { get; }

    /// <summary>解锁的法术。</summary>
    public ImmutableArray<SpellKind> UnlockedSpells { get; }

    /// <summary>是否属于两条路线共用的基础知识。</summary>
    public bool Shared => Branch.Shared;

    /// <summary>研究对基础工具等级的贡献。</summary>
    public bool ImprovesBasicTechnology =>
        this == Agriculture || this == Logistics || this == SignalNetwork || this == ArcaneArts;

    /// <summary>实际研究效果的说明文字。</summary>
    public string Effect
    {
        get
        {
            if (_effect is not null) return _effect;
            var recipe = ProductionRules.For(this)!;
            return "解锁" + recipe.FacilityName + "。\n" + recipe.Description;
        }
    }

    /// <summary>研究分支、阶段、前置知识和实际效果的说明。</summary>
    public string Description =>
        $"分支：{Branch}\n阶段：{Stage}\n前置：{(Prerequisites.Length == 0 ? "无" : string.Join("、", Prerequisites.Select(research => research.Name)))}\n{Effect}";

    /// <summary>返回研究显示名称。</summary>
    public override string ToString()
    {
        return Name;
    }
}
