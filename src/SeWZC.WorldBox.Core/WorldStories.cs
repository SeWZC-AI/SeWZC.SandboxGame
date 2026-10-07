namespace SeWZC.WorldBox.Core;

/// <summary>编年史的只读查询工具。</summary>
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
            if (group is null)
                groups.Add([entry]);
            else
                group.Add(entry);
        }

        return groups.Select(g => new EventGroup(g)).OrderByDescending(g => g.Latest.Tick)
            .ThenByDescending(g => g.Latest.Id).ToArray();
    }
}
