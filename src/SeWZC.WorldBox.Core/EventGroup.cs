namespace SeWZC.WorldBox.Core;

/// <summary>按时间排列的一组相关编年史记录，供界面聚合展示。</summary>
/// <param name="Entries">按时间排列的非空事件集合。</param>
public sealed record EventGroup(IReadOnlyList<WorldEvent> Entries)
{
    /// <summary>本组按时间排列的最后一条记录。</summary>
    public WorldEvent Latest => Entries[^1];
    /// <summary>本组包含的事件数量。</summary>
    public int Count => Entries.Count;
}
