using System.Collections.Immutable;

namespace SeWZC.WorldBox.Core;

/// <summary>尚未送达接收者的消息。</summary>
public sealed record PendingMessage
{
    /// <summary>发送消息的居民 ID。</summary>
    public int SenderId { get; init; }

    /// <summary>接收消息的居民 ID。</summary>
    public int RecipientId { get; init; }

    /// <summary>作为中继接收地点的聚落 ID。</summary>
    public int TargetSettlementId { get; init; }

    /// <summary>计划送达的模拟日序。</summary>
    public long DeliverTick { get; init; }

    /// <summary>待递送的信息副本。</summary>
    public ImmutableArray<AgentFact> Facts { get; init; } = [];
}
