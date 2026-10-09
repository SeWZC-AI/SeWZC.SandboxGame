using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

public sealed partial record Army
{
    /// <summary>当前收到的有限占领或家园防御目标。</summary>
    public WarObjective Objective { get; init; }

    /// <summary>本轮战役的起始事件 ID。</summary>
    public int CampaignEventId { get; init; }

    /// <summary>最近一次军队行动关联的事件 ID。</summary>
    public int LastEventId { get; init; }

    /// <summary>本轮战役开始时的士兵数量。</summary>
    public int InitialSoldiers { get; init; }

    /// <summary>本轮战役开始的模拟 tick 序。</summary>
    public long StartedTick { get; init; }

    /// <summary>连续无法沿路线前进的模拟 tick 数。</summary>
    public int BlockedTicks { get; init; }

    /// <summary>当前记录的战役结果或撤退原因。</summary>
    public WarOutcome Outcome { get; init; }

    /// <summary>本轮战役是否已经记录过交战事件。</summary>
    public bool BattleRecorded { get; init; }

    /// <summary>军队指挥居民的 ID。</summary>
    public int CommanderId { get; init; }

    /// <summary>军队根据已收到消息获知的外交状态。</summary>
    public DiplomaticStatus KnownDiplomacy { get; init; } = DiplomaticStatus.War;

    /// <summary>最近收到军令的观察 tick 序。</summary>
    public long LastOrderTick { get; init; }

    /// <summary>最近收到军令的信息记录 ID。</summary>
    [JsonRequired]
    public int LastOrderFactId { get; init; }

    /// <summary>当前移动区段起点的横向地格坐标。</summary>
    public int FromX { get; init; }

    /// <summary>当前移动区段起点的纵向地格坐标。</summary>
    public int FromY { get; init; }

    /// <summary>当前移动区段开始的模拟 tick 序。</summary>
    public long MoveStartedTick { get; init; }

    /// <summary>当前移动区段所需的模拟 tick 数。</summary>
    public int MoveDurationTicks { get; init; } = 2;

    /// <summary>是否仍在集结士兵。</summary>
    public bool Gathering { get; init; } = true;

    /// <summary>是否正在撤回家园。</summary>
    public bool Retreating { get; init; }

    /// <summary>军队目标的横向地格坐标。</summary>
    public int TargetX { get; init; }

    /// <summary>军队目标的纵向地格坐标。</summary>
    public int TargetY { get; init; }

    /// <summary>当前军事目标聚落的 ID。</summary>
    public int TargetSettlementId { get; init; }
}
