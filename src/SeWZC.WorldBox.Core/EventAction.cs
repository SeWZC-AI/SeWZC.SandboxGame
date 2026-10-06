namespace SeWZC.WorldBox.Core;

/// <summary>编年史记录的具体行动，用于关联和聚合同类事件。</summary>
public enum EventAction
{
    /// <summary>一般记录。</summary>
    General,
    /// <summary>开始。</summary>
    Started,
    /// <summary>完成。</summary>
    Completed,
    /// <summary>玩家赐予。</summary>
    Gifted,
    /// <summary>宣告。</summary>
    Declaration,
    /// <summary>集结。</summary>
    Muster,
    /// <summary>交战。</summary>
    Battle,
    /// <summary>占领。</summary>
    Capture,
    /// <summary>撤退。</summary>
    Retreat,
    /// <summary>报告。</summary>
    Report,
    /// <summary>返回家园。</summary>
    Homecoming,
    /// <summary>迁徙。</summary>
    Migration,
    /// <summary>地方分裂。</summary>
    Secession,
    /// <summary>递送。</summary>
    Delivery,
    /// <summary>政策变化。</summary>
    Policy,
    /// <summary>文化变化。</summary>
    Culture,
    /// <summary>职业变化。</summary>
    Career,
    /// <summary>死亡。</summary>
    Death,
}
