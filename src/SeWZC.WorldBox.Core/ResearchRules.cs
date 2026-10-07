namespace SeWZC.WorldBox.Core;

/// <summary>研究项目目录及知识解锁规则。</summary>
public static class ResearchRules
{
    private static readonly IReadOnlyList<Advancement> TechnologyRoute;
    private static readonly IReadOnlyList<Advancement> MagicRoute;
    private static readonly IReadOnlyDictionary<BuildingKind, Advancement> ByBuilding;
    private static readonly IReadOnlyDictionary<Profession, Advancement> ByProfession;
    private static readonly IReadOnlyDictionary<SpellKind, Advancement> BySpell;

    static ResearchRules()
    {
        // 索引与路线必须等待知识图谱初始化完成。
        TechnologyRoute = Array.AsReadOnly(All.Where(r => !r.Magic || r.Shared).ToArray());
        MagicRoute = Array.AsReadOnly(All.Where(r => r.Magic || r.Shared).ToArray());
        ByBuilding = All.SelectMany(r => r.UnlockedBuildings.Select(k => (Kind: k, Definition: r)))
            .ToDictionary(p => p.Kind, p => p.Definition);
        ByProfession = All.SelectMany(r => r.UnlockedProfessions.Select(k => (Kind: k, Definition: r)))
            .ToDictionary(p => p.Kind, p => p.Definition);
        BySpell = All.SelectMany(r => r.UnlockedSpells.Select(k => (Kind: k, Definition: r)))
            .ToDictionary(p => p.Kind, p => p.Definition);
    }

    /// <summary>全部研究节点的定义。</summary>
    public static IReadOnlyList<Advancement> All => Advancement.All;

    /// <summary>返回科技或魔法路线的全部研究，包含共同基础项目。</summary>
    /// <param name="magic">是否查询魔法路线；关闭时查询科技路线。</param>
    public static IReadOnlyList<Advancement> Route(bool magic)
    {
        return magic ? MagicRoute : TechnologyRoute;
    }

    /// <summary>查找解锁指定建筑的研究，没有对应研究时返回空值。</summary>
    /// <param name="kind">设施类别。</param>
    public static Advancement? Unlocking(BuildingKind kind)
    {
        return ByBuilding.GetValueOrDefault(kind);
    }

    /// <summary>查找解锁指定职业的研究，没有对应研究时返回空值。</summary>
    /// <param name="kind">职业类别。</param>
    public static Advancement? Unlocking(Profession kind)
    {
        return ByProfession.GetValueOrDefault(kind);
    }

    /// <summary>查找解锁指定法术的研究，没有对应研究时返回空值。</summary>
    /// <param name="kind">法术类别。</param>
    public static Advancement? Unlocking(SpellKind kind)
    {
        return BySpell.GetValueOrDefault(kind);
    }
}
