using System.Collections.Immutable;

namespace SeWZC.WorldBox.Core;

/// <summary>研究节点的前置条件、成本、所需工作量、分支及解锁的玩法入口。</summary>
/// <param name="kind">研究项目类别。</param>
/// <param name="name">研究显示名称。</param>
/// <param name="branch">研究所属分支。</param>
/// <param name="stage">研究所属发展阶段名称。</param>
/// <param name="magic">该研究是否属于魔法路线。</param>
/// <param name="prerequisites">开始或使用该研究所需的前置知识。</param>
/// <param name="cost">开始研究时投入的资源成本。</param>
/// <param name="work">完成研究需要的总工作量。</param>
/// <param name="effect">实际研究效果的说明文字。</param>
/// <param name="Buildings">解锁的建筑类型，空值表示没有建筑解锁。</param>
/// <param name="Professions">解锁的职业，空值表示没有职业解锁。</param>
/// <param name="Spells">解锁的法术，空值表示没有法术解锁。</param>
/// <param name="Action">解锁的操作入口，空值表示没有额外入口。</param>
public sealed class ResearchDefinition(
    ResearchKind kind,
    string name,
    ResearchBranch branch,
    string stage,
    bool magic,
    IReadOnlyList<ResearchKind> prerequisites,
    ResourceAmounts cost,
    double work,
    string effect,
    IReadOnlyList<BuildingKind>? Buildings = null,
    IReadOnlyList<Profession>? Professions = null,
    IReadOnlyList<SpellKind>? Spells = null,
    ResearchAction? Action = null)
{
    /// <summary>研究项目类别。</summary>
    public ResearchKind Kind { get; } = kind;
    /// <summary>研究显示名称。</summary>
    public string Name { get; } = name;
    /// <summary>研究所属分支。</summary>
    public ResearchBranch Branch { get; } = branch;
    /// <summary>研究所属发展阶段名称。</summary>
    public string Stage { get; } = stage;
    /// <summary>该研究是否属于魔法路线。</summary>
    public bool Magic { get; } = magic;
    /// <summary>开始或使用该研究所需的前置知识。</summary>
    public ImmutableArray<ResearchKind> Prerequisites { get; } = prerequisites.ToImmutableArray();
    /// <summary>开始研究时投入的资源成本。</summary>
    public ResourceAmounts Cost { get; } = cost;
    /// <summary>完成研究需要的总工作量。</summary>
    public double Work { get; } = work;
    /// <summary>实际研究效果的说明文字。</summary>
    public string Effect { get; } = effect;
    /// <summary>解锁的操作入口，空值表示没有额外入口。</summary>
    public ResearchAction? Action { get; } = Action;

    /// <summary>是否属于两条路线共同使用的基础分支。</summary>
    public bool Shared => Branch.Shared;

    /// <summary>此研究解锁的建筑类型。</summary>
    public ImmutableArray<BuildingKind> UnlockedBuildings { get; } = Buildings?.ToImmutableArray() ?? [];

    /// <summary>此研究解锁的职业。</summary>
    public ImmutableArray<Profession> UnlockedProfessions { get; } = Professions?.ToImmutableArray() ?? [];

    /// <summary>此研究解锁的法术。</summary>
    public ImmutableArray<SpellKind> UnlockedSpells { get; } = Spells?.ToImmutableArray() ?? [];
}
