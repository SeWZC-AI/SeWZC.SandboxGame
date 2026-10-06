namespace SeWZC.WorldBox.Core;

/// <summary>军队结束战役或撤退时记录的结果。</summary>
public enum WarOutcome
{
    /// <summary>尚无结果。</summary>
    None,
    /// <summary>达到目标。</summary>
    ObjectiveReached,
    /// <summary>补给不足。</summary>
    SupplyShortage,
    /// <summary>损失过重。</summary>
    HeavyLosses,
    /// <summary>目标变化。</summary>
    TargetChanged,
    /// <summary>收到新命令。</summary>
    OrdersReceived,
    /// <summary>道路受阻。</summary>
    RouteBlocked,
    /// <summary>行动耗尽。</summary>
    Exhausted,
}
