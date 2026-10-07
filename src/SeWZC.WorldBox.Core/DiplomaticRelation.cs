using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

/// <summary>两个国家之间的外交关系。</summary>
public sealed record DiplomaticRelation
{
    /// <summary>最近一次外交状态变化的模拟日序。</summary>
    public long LastChangedTick { get; init; }

    /// <summary>最近一次实际接触的模拟日序。</summary>
    public long LastContactTick { get; init; }

    /// <summary>第一国首次进入持续敌意的日序，0 表示尚未开始。</summary>
    [JsonRequired]
    public long FirstEscalationTick { get; init; }

    /// <summary>第二国首次进入持续敌意的日序，0 表示尚未开始。</summary>
    [JsonRequired]
    public long SecondEscalationTick { get; init; }

    /// <summary>最近一次评估外交关系的模拟日序。</summary>
    public long LastEvaluatedTick { get; init; }

    /// <summary>最近一次外交变化关联的事件 ID。</summary>
    public int LastEventId { get; init; }

    /// <summary>当前发起结盟提议的国家 ID。</summary>
    public int AllianceOfferNationId { get; init; }

    /// <summary>当前结盟提议发起的模拟日序。</summary>
    public long AllianceOfferTick { get; init; }

    /// <summary>当前外交关系或协商决定的理由。</summary>
    public string Reason { get; init; } = "等待实际接触与递送的消息";

    /// <summary>关系中的第一国 ID。</summary>
    public int FirstNationId { get; init; }

    /// <summary>关系中的第二国 ID。</summary>
    public int SecondNationId { get; init; }

    /// <summary>两国共同的外交状态。</summary>
    public DiplomaticStatus Status { get; init; }

    /// <summary>第一国对第二国的态度分值。</summary>
    [JsonRequired]
    public int FirstOpinion { get; init; }

    /// <summary>第二国对第一国的态度分值。</summary>
    [JsonRequired]
    public int SecondOpinion { get; init; }

    /// <summary>双方态度的舍入均值，用于展示。</summary>
    public int Opinion { get; init; }
}
