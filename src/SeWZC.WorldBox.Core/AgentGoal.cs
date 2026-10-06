namespace SeWZC.WorldBox.Core;

/// <summary>当前任务、目标及依据，以及为避免反复受阻而保存的导航进度。</summary>
public sealed class AgentGoal
{
    /// <summary>当前导航目标地格的数组索引，-1 表示尚未设置。</summary>
    public int NavigationTarget { get; set; } = -1;
    /// <summary>当前导航已走过的地格索引，用于避免反复绕路。</summary>
    public List<int> NavigationVisited { get; set; } = [];
    /// <summary>当前导航曾达到的最短目标距离，以地格计。</summary>
    public int NavigationBestDistance { get; set; }
    /// <summary>连续未缩短目标距离的导航尝试次数。</summary>
    public int NavigationWithoutProgress { get; set; }
    /// <summary>受阻后允许重新尝试导航的模拟日序。</summary>
    public long NavigationRetryTick { get; set; }
    /// <summary>选择该目标使用的信息依据 ID。</summary>
    public int EvidenceFactId { get; set; }
    /// <summary>该行动关联的前因事件 ID。</summary>
    public int CauseEventId { get; set; }
    /// <summary>当前行动目标类别。</summary>
    public AgentGoalKind Kind { get; set; }
    /// <summary>目标位置的横向地格坐标。</summary>
    public int TargetX { get; set; }
    /// <summary>目标位置的纵向地格坐标。</summary>
    public int TargetY { get; set; }
    /// <summary>目标关联的聚落 ID。</summary>
    public int TargetSettlementId { get; set; }
    /// <summary>任务对象编号；设施任务使用建筑 ID，取水和狩猎捕鱼使用资源地格索引加 1。</summary>
    public int TargetEntityId { get; set; }
    /// <summary>该目标开始执行的模拟日序。</summary>
    public long StartedTick { get; set; }
    /// <summary>该目标已累计的劳动或驻留日数。</summary>
    public int WorkTicks { get; set; }
    /// <summary>下次重新评估该目标的模拟日序。</summary>
    public long ReviewTick { get; set; }
    /// <summary>该目标是否由玩家直接安排。</summary>
    public bool PlayerDirected { get; set; }
    /// <summary>选择该目标的理由。</summary>
    public string Reason { get; set; } = "";
}
