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

/// <summary>军队接到的有限占领或家园防御目标。</summary>
public enum WarObjective
{
    /// <summary>有限占领目标聚落。</summary>
    OccupySettlement,
    /// <summary>防御家园。</summary>
    DefendHomeland,
}

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

public sealed partial class Nation
{
    /// <summary>机构发布的军令及实际收到的战报。</summary>
    public MilitaryRecord Military { get; set; } = new();
}

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

public sealed partial class Army
{
    /// <summary>当前收到的有限占领或家园防御目标。</summary>
    public WarObjective Objective { get; set; }
    /// <summary>本轮战役的起始事件 ID。</summary>
    public int CampaignEventId { get; set; }
    /// <summary>最近一次军队行动关联的事件 ID。</summary>
    public int LastEventId { get; set; }
    /// <summary>本轮战役开始时的士兵数量。</summary>
    public int InitialSoldiers { get; set; }
    /// <summary>本轮战役开始的模拟日序。</summary>
    public long StartedTick { get; set; }
    /// <summary>连续无法沿路线前进的模拟日数。</summary>
    public int BlockedTicks { get; set; }
    /// <summary>当前记录的战役结果或撤退原因。</summary>
    public WarOutcome Outcome { get; set; }
    /// <summary>本轮战役是否已经记录过交战事件。</summary>
    public bool BattleRecorded { get; set; }
}

public sealed partial class WorldEvent
{
    /// <summary>事件记录的具体行动。</summary>
    public EventAction Action { get; set; }
    /// <summary>关联或归属聚落的稳定 ID。</summary>
    public int SettlementId { get; set; }
    /// <summary>事件涉及的另一聚落 ID。</summary>
    public int SecondSettlementId { get; set; }
    /// <summary>事件使用的信息依据 ID。</summary>
    public int EvidenceFactId { get; set; }
    /// <summary>除主要前因外关联的其他前因事件 ID。</summary>
    public List<int> AdditionalCauseEventIds { get; set; } = [];
}

/// <summary>某一模拟日观测到的项目累计进度。</summary>
public sealed class ProgressSample
{
    /// <summary>采样时的模拟日序。</summary>
    public long Tick { get; set; }
    /// <summary>采样时项目已经累计的工作量。</summary>
    public double Progress { get; set; }
}

/// <summary>项目开工事件、贡献者和近期进度样本，用于估算完工时间。</summary>
public sealed class ProjectObservation
{
    /// <summary>当前项目开始时关联的事件 ID。</summary>
    public int StartEventId { get; set; }
    /// <summary>已经为项目提供劳动的居民 ID。</summary>
    public List<int> Contributors { get; set; } = [];
    /// <summary>采样时采用的发展速率倍率，用于判定样本是否仍适用。</summary>
    public double DevelopmentRate { get; set; } = 1;
    /// <summary>按时间保存的近期进度样本。</summary>
    public List<ProgressSample> Samples { get; set; } = [];
}

/// <summary>项目预计剩余模拟日数及估算依据；无法估算时日数为空。</summary>
/// <param name="RemainingTicks">预计剩余模拟日数，空值表示当前无法可靠估算。</param>
/// <param name="Explanation">估算依据或无法估算的原因。</param>
public readonly record struct CompletionEstimate(long? RemainingTicks, string Explanation);

/// <summary>按时间排列的一组相关编年史记录，供界面聚合展示。</summary>
/// <param name="Entries">按时间排列的非空事件集合。</param>
public sealed record EventGroup(IReadOnlyList<WorldEvent> Entries)
{
    /// <summary>本组按时间排列的最后一条记录。</summary>
    public WorldEvent Latest => Entries[^1];
    /// <summary>本组包含的事件数量。</summary>
    public int Count => Entries.Count;
}

/// <summary>可以关注编年史变化的对象类别。</summary>
public enum ObservedObjectKind
{
    /// <summary>国家。</summary>
    Nation,
    /// <summary>聚落。</summary>
    Settlement,
    /// <summary>居民。</summary>
    Resident,
}

/// <summary>以类别和稳定 ID 标识的关注对象。</summary>
/// <param name="Kind">关注对象的类别。</param>
/// <param name="Id">该类别下对象的稳定 ID。</param>
public readonly record struct ObservedObject(ObservedObjectKind Kind, int Id);

/// <summary>提供不改变事件、模拟时间或随机状态的编年史关联和聚合查询。</summary>
public static class WorldStories
{
    /// <summary>判断事件是否关联指定的国家、聚落或居民。</summary>
    /// <param name="item">待查询的世界事件。</param>
    /// <param name="target">待关注的对象。</param>
    public static bool Involves(WorldEvent item, ObservedObject target)
    {
        return target.Kind switch
        {
            ObservedObjectKind.Nation => item.NationId == target.Id || item.SecondNationId == target.Id,
            ObservedObjectKind.Settlement => item.SettlementId == target.Id || item.SecondSettlementId == target.Id,
            _ => item.ResidentId == target.Id,
        };
    }

    /// <summary>枚举事件已记录的正数前因 ID，并去重。</summary>
    /// <param name="item">待查询的世界事件。</param>
    public static IEnumerable<int> Causes(WorldEvent item)
    {
        return new[] { item.CauseEventId }
            .Concat(item.AdditionalCauseEventIds).Where(id => id > 0).Distinct();
    }

    /// <summary>按主体和行动聚合六十日内的普通事件，重大事件单独保留，按最近事件倒序返回。</summary>
    /// <param name="events">要聚合的世界事件集合。</param>
    public static IReadOnlyList<EventGroup> Group(IEnumerable<WorldEvent> events)
    {
        var groups = new List<List<WorldEvent>>();
        foreach (var entry in events.OrderBy(e => e.Tick).ThenBy(e => e.Id))
        {
            var group = entry.Importance < EventImportance.Major && entry.Action != EventAction.General
                ? groups.LastOrDefault(g => g[0].Importance < EventImportance.Major && g[0].Kind == entry.Kind
                    && g[0].Action == entry.Action && g[0].NationId == entry.NationId &&
                    g[0].SecondNationId == entry.SecondNationId
                    && g[0].SettlementId == entry.SettlementId && g[0].SecondSettlementId == entry.SecondSettlementId
                    && g[0].ResidentId == entry.ResidentId && entry.Tick - g[0].Tick <= 60)
                : null;
            if (group is null) groups.Add([entry]);
            else group.Add(entry);
        }

        return groups.Select(g => new EventGroup(g)).OrderByDescending(g => g.Latest.Tick)
            .ThenByDescending(g => g.Latest.Id).ToArray();
    }
}
