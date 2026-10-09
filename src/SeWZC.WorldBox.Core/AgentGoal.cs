using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

/// <summary>居民当前行动目标与导航进度的不可变值。</summary>
public sealed record AgentGoal
{
    /// <summary>登记领地、交付建村物资和当面递送所需的最长驻留 tick 数。</summary>
    public const int MaximumResidenceTicks = 3;

    /// <summary>创建尚未开始导航的空闲目标。</summary>
    public AgentGoal() { }

    /// <summary>当前导航目标地格的数组索引，-1 表示尚未设置。</summary>
    public int NavigationTarget { get; init; } = -1;

    /// <summary>当前绕路阶段访问过的地格索引，用于避免循环；取得新的最短目标距离后清空。</summary>
    public ImmutableArray<int> NavigationVisited { get; init; } = [];

    /// <summary>依据当时六格视野规划的短路线，包含起点和至多六个后续地格。</summary>
    [JsonRequired]
    public ImmutableArray<int> NavigationRoute { get; init; } = [];

    /// <summary>短路线中下次要走的地格位置；空路线为零。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int NavigationRouteOffset { get; init; }

    /// <summary>规划短路线时使用的交通方式，方式改变后重新规划。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public TravelMode NavigationRouteMode { get; init; }

    /// <summary>当前导航曾达到的最短目标距离，以地格计。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int NavigationBestDistance { get; init; }

    /// <summary>连续未缩短目标距离的导航尝试次数。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int NavigationWithoutProgress { get; init; }

    /// <summary>受阻后允许重新尝试导航的模拟 tick 序。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public long NavigationRetryTick { get; init; }

    /// <summary>选择该目标使用的信息依据 ID。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int EvidenceFactId { get; init; }

    /// <summary>该行动关联的前因事件 ID。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int CauseEventId { get; init; }

    /// <summary>当前行动目标类别。</summary>
    public AgentGoalKind Kind { get; init; }

    /// <summary>目标位置的横向地格坐标。</summary>
    public int TargetX { get; init; }

    /// <summary>目标位置的纵向地格坐标。</summary>
    public int TargetY { get; init; }

    /// <summary>目标关联的聚落 ID。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int TargetSettlementId { get; init; }

    /// <summary>任务对象编号；设施任务使用建筑 ID，取水和狩猎捕鱼使用资源地格索引加 1。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int TargetEntityId { get; init; }

    /// <summary>该目标开始执行的模拟 tick 序。</summary>
    public long StartedTick { get; init; }

    /// <summary>需要到场驻留的目标已完成的等待 tick 数，达到三 tick后不再累积。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int WorkTicks { get; init; }

    /// <summary>下次重新评估该目标的模拟 tick 序。</summary>
    public long ReviewTick { get; init; }

    /// <summary>该目标是否由玩家直接安排。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool PlayerDirected { get; init; }

    /// <summary>选择该目标的理由。</summary>
    public string Reason { get; init; } = "";

    internal bool NeedsResidence => Kind is AgentGoalKind.ReturnHome or AgentGoalKind.ClaimLand
        or AgentGoalKind.FetchWater or AgentGoalKind.DeliverMessage or AgentGoalKind.Trade or AgentGoalKind.Petition;

    /// <summary>到达且未被冻结后登记一天驻留；无需驻留的目标保持零，完成等待后保留原值。</summary>
    public AgentGoal Attend()
    {
        return NeedsResidence
            ? WorkTicks < MaximumResidenceTicks ? this with { WorkTicks = WorkTicks + 1 } : this
            : WorkTicks == 0
                ? this
                : this with { WorkTicks = 0 };
    }

    /// <summary>更换导航目的地时建立新的导航记录；目标未改变时继续使用原进度。</summary>
    /// <param name="target">目标地格索引。</param>
    /// <param name="distance">当前位置到目标的距离。</param>
    public AgentGoal BeginNavigation(int target, int distance)
    {
        return NavigationTarget == target
            ? this
            : this with
            {
                NavigationTarget = target,
                NavigationVisited = [],
                NavigationRoute = [],
                NavigationRouteOffset = 0,
                NavigationBestDistance = distance,
                NavigationWithoutProgress = 0,
                NavigationRetryTick = 0,
            };
    }

    /// <summary>记下经过的地格，不修改旧目标持有的路线记录。</summary>
    /// <param name="index">经过的地格索引。</param>
    public AgentGoal Visit(int index)
    {
        return NavigationVisited.Contains(index) || NavigationVisited.Length >= 256
            ? this
            : this with { NavigationVisited = NavigationVisited.Add(index) };
    }

    /// <summary>重试原任务时清除受阻导航记录，保留任务和先前最佳距离。</summary>
    public AgentGoal ResetNavigation()
    {
        return this with
        {
            NavigationTarget = -1,
            NavigationVisited = [],
            NavigationRoute = [],
            NavigationRouteOffset = 0,
            NavigationWithoutProgress = 0,
            NavigationRetryTick = 0,
        };
    }
}
