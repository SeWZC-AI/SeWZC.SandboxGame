namespace SeWZC.WorldBox.Core;

/// <summary>聚落当前采用的政策。</summary>
public sealed record LocalPolicy
{
    /// <summary>采用此政策的聚落 ID。</summary>
    public int SettlementId { get; init; }

    /// <summary>当前采用的本地政策。</summary>
    public PolicyKind Kind { get; init; }

    /// <summary>当前政策是否由玩家指定。</summary>
    public bool PlayerOverride { get; init; }

    /// <summary>本次政策决定的模拟日序。</summary>
    public long DecidedTick { get; init; }

    /// <summary>选择此政策的实际理由。</summary>
    public string Reason { get; init; } = "尚无递送议题，维持均衡政策";

    /// <summary>政策决定采用的信息依据 ID。</summary>
    public int EvidenceFactId { get; init; }

    /// <summary>该信息依据最初观察发生的日序。</summary>
    public long EvidenceObservedTick { get; init; }
}
