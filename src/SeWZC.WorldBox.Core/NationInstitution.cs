namespace SeWZC.WorldBox.Core;

/// <summary>国家的制度与议事决策状态。</summary>
public sealed record NationInstitution
{
    /// <summary>实行此制度的国家 ID。</summary>
    public int NationId { get; init; }

    /// <summary>当前国家制度形式。</summary>
    public InstitutionKind Kind { get; init; }

    /// <summary>玩家指定的政策，空值表示由机构自主决策。</summary>
    public PolicyKind? PlayerPolicy { get; init; }

    /// <summary>最近一次制度决策的说明。</summary>
    public string LastDecision { get; init; } = "等待本地议事及代表送达的报告";

    /// <summary>最近一次制度决策的模拟 tick 序。</summary>
    public long LastDecisionTick { get; init; }
}
