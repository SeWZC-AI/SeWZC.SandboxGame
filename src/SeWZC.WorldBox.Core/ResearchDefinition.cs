namespace SeWZC.WorldBox.Core;

/// <summary>研究节点的前置条件、成本、所需工作量、分支及解锁的玩法入口。</summary>
/// <param name="Kind">研究项目类别。</param>
/// <param name="Name">研究显示名称。</param>
/// <param name="Branch">研究所属分支名称。</param>
/// <param name="Stage">研究所属发展阶段名称。</param>
/// <param name="Magic">该研究是否属于魔法路线。</param>
/// <param name="Prerequisites">开始或使用该研究所需的前置知识。</param>
/// <param name="Cost">开始研究时投入的资源成本。</param>
/// <param name="Work">完成研究需要的总工作量。</param>
/// <param name="Effect">实际研究效果的说明文字。</param>
/// <param name="Buildings">解锁的建筑类型，空值表示没有建筑解锁。</param>
/// <param name="Professions">解锁的职业，空值表示没有职业解锁。</param>
/// <param name="Spells">解锁的法术，空值表示没有法术解锁。</param>
/// <param name="Action">解锁的操作入口名称，空值表示没有额外入口。</param>
public sealed record ResearchDefinition(
    ResearchKind Kind,
    string Name,
    string Branch,
    string Stage,
    bool Magic,
    ResearchKind[] Prerequisites,
    ResourceStock Cost,
    double Work,
    string Effect,
    BuildingKind[]? Buildings = null,
    Profession[]? Professions = null,
    SpellKind[]? Spells = null,
    string? Action = null)
{
    /// <summary>该研究是否属于科技与魔法路线共用的基础分支。</summary>
    public bool Shared => Branch is "民生与资源" or "城建与公共卫生" or "知识与勘察";
    /// <summary>此研究解锁的建筑类型；未指定时为空集合。</summary>
    public IReadOnlyList<BuildingKind> UnlockedBuildings => Buildings ?? [];
    /// <summary>此研究解锁的职业；未指定时为空集合。</summary>
    public IReadOnlyList<Profession> UnlockedProfessions => Professions ?? [];
    /// <summary>此研究解锁的法术；未指定时为空集合。</summary>
    public IReadOnlyList<SpellKind> UnlockedSpells => Spells ?? [];
}
