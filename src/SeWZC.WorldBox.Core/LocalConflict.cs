namespace SeWZC.WorldBox.Core;

/// <summary>居民之间的一场局部资源冲突。</summary>
public sealed class LocalConflict
{
    /// <summary>局部冲突的稳定 ID。</summary>
    public int Id { get; set; }

    /// <summary>最初参与冲突的第一位居民 ID。</summary>
    public int FirstResidentId { get; set; }

    /// <summary>最初参与冲突的第二位居民 ID。</summary>
    public int SecondResidentId { get; set; }

    /// <summary>冲突发生的聚落 ID。</summary>
    public int SettlementId { get; set; }

    /// <summary>冲突地点的横向地格坐标。</summary>
    public int X { get; set; }

    /// <summary>冲突地点的纵向地格坐标。</summary>
    public int Y { get; set; }

    /// <summary>冲突目前影响的参与者范围。</summary>
    public ConflictScope Scope { get; set; }

    /// <summary>当前冲突阶段。</summary>
    public ConflictStage Stage { get; set; }

    /// <summary>当前紧张程度，影响升级或缓和。</summary>
    public double Tension { get; set; }

    /// <summary>冲突开始的模拟日序。</summary>
    public long StartedTick { get; set; }

    /// <summary>进入当前阶段的模拟日序。</summary>
    public long StageStartedTick { get; set; }

    /// <summary>最近一次冲突状态变化的模拟日序。</summary>
    public long LastChangedTick { get; set; }

    /// <summary>最近一次冲突关联的事件 ID。</summary>
    public int LastEventId { get; set; }

    /// <summary>当前参与冲突的居民 ID 集合。</summary>
    public List<int> Participants { get; set; } = [];
}
