namespace SeWZC.WorldBox.Core;

/// <summary>等待按计划送达居民的信息，也可指定中继聚落作为接收地点。</summary>
public sealed class PendingMessage
{
    /// <summary>发送消息的居民 ID。</summary>
    public int SenderId { get; set; }

    /// <summary>接收消息的居民 ID。</summary>
    public int RecipientId { get; set; }

    /// <summary>作为中继接收地点的聚落 ID。</summary>
    public int TargetSettlementId { get; set; }

    /// <summary>计划送达的模拟日序。</summary>
    public long DeliverTick { get; set; }

    /// <summary>待递送的信息副本。</summary>
    public List<AgentFact> Facts { get; set; } = [];
}
