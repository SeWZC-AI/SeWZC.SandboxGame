namespace SeWZC.WorldBox.Core;

/// <summary>国家机构掌握的战役记录，可能落后于前线实况。</summary>
public sealed record MilitaryRecord
{
    /// <summary>机构记录的本轮战役起始事件 ID。</summary>
    public int CampaignEventId { get; init; }

    /// <summary>机构记录的敌方国家 ID。</summary>
    public int EnemyNationId { get; init; }

    /// <summary>机构发布的战役目标。</summary>
    public WarObjective Objective { get; init; }

    /// <summary>机构军令指定的目标聚落 ID。</summary>
    public int TargetSettlementId { get; init; }

    /// <summary>机构军令指定的横向地格坐标。</summary>
    public int TargetX { get; init; }

    /// <summary>机构军令指定的纵向地格坐标。</summary>
    public int TargetY { get; init; }

    /// <summary>本轮战役军令发布的模拟日序。</summary>
    public long StartedTick { get; init; }

    /// <summary>最近一次已经执行动员的军令信息 ID。</summary>
    public int LastMobilizedOrderId { get; init; }

    /// <summary>国家军队恢复期的截止日序。</summary>
    public long RecoveryUntilTick { get; init; }

    /// <summary>最近实际收到的战报事件 ID。</summary>
    public int LastReportEventId { get; init; }

    /// <summary>最近战报在前线观察时的日序。</summary>
    public long LastReportObservedTick { get; init; }

    /// <summary>机构收到最近战报的日序。</summary>
    public long LastReportReceivedTick { get; init; }

    /// <summary>最近收到的战报所述战役结果。</summary>
    public WarOutcome ReportedOutcome { get; init; }

    /// <summary>最近收到的前线战报说明。</summary>
    public string Report { get; init; } = "尚未收到前线战报";
}
