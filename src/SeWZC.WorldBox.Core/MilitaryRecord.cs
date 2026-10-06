namespace SeWZC.WorldBox.Core;

/// <summary>机构发布的军令及实际收到的战报记录，不直接反映实时前线状态。</summary>
public sealed class MilitaryRecord
{
    /// <summary>机构记录的本轮战役起始事件 ID。</summary>
    public int CampaignEventId { get; set; }
    /// <summary>机构记录的敌方国家 ID。</summary>
    public int EnemyNationId { get; set; }
    /// <summary>机构发布的战役目标。</summary>
    public WarObjective Objective { get; set; }
    /// <summary>机构军令指定的目标聚落 ID。</summary>
    public int TargetSettlementId { get; set; }
    /// <summary>机构军令指定的横向地格坐标。</summary>
    public int TargetX { get; set; }
    /// <summary>机构军令指定的纵向地格坐标。</summary>
    public int TargetY { get; set; }
    /// <summary>本轮战役军令发布的模拟日序。</summary>
    public long StartedTick { get; set; }
    /// <summary>最近一次已经执行动员的军令信息 ID。</summary>
    public int LastMobilizedOrderId { get; set; }
    /// <summary>国家军队恢复期的截止日序。</summary>
    public long RecoveryUntilTick { get; set; }
    /// <summary>最近实际收到的战报事件 ID。</summary>
    public int LastReportEventId { get; set; }
    /// <summary>最近战报在前线观察时的日序。</summary>
    public long LastReportObservedTick { get; set; }
    /// <summary>机构收到最近战报的日序。</summary>
    public long LastReportReceivedTick { get; set; }
    /// <summary>最近收到的战报所述战役结果。</summary>
    public WarOutcome ReportedOutcome { get; set; }
    /// <summary>最近收到的前线战报说明。</summary>
    public string Report { get; set; } = "尚未收到前线战报";
}
