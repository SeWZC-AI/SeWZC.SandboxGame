namespace SeWZC.WorldBox.Core;

/// <summary>国家的制度形式、可选的玩家指定政策及最近一次制度决策。</summary>
public sealed class NationInstitution
{
    /// <summary>关联或归属国家的稳定 ID。</summary>
    public int NationId { get; set; }
    /// <summary>当前国家制度形式。</summary>
    public InstitutionKind Kind { get; set; }
    /// <summary>玩家指定的政策，空值表示由机构自主决策。</summary>
    public PolicyKind? PlayerPolicy { get; set; }
    /// <summary>最近一次制度决策的说明。</summary>
    public string LastDecision { get; set; } = "等待本地议事及代表送达的报告";
    /// <summary>最近一次制度决策的模拟日序。</summary>
    public long LastDecisionTick { get; set; }
}
