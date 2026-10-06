namespace SeWZC.WorldBox.Core;

/// <summary>聚落选定的政策及其依据，也可由玩家覆盖选择。</summary>
public sealed class LocalPolicy
{
    /// <summary>关联或归属聚落的稳定 ID。</summary>
    public int SettlementId { get; set; }
    /// <summary>当前采用的本地政策。</summary>
    public PolicyKind Kind { get; set; }
    /// <summary>当前政策是否由玩家指定。</summary>
    public bool PlayerOverride { get; set; }
    /// <summary>本次政策决定的模拟日序。</summary>
    public long DecidedTick { get; set; }
    /// <summary>选择此政策的实际理由。</summary>
    public string Reason { get; set; } = "尚无递送议题，维持均衡政策";
    /// <summary>政策决定采用的信息依据 ID。</summary>
    public int EvidenceFactId { get; set; }
    /// <summary>该信息依据最初观察发生的日序。</summary>
    public long EvidenceObservedTick { get; set; }
}
