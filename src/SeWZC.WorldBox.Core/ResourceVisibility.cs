namespace SeWZC.WorldBox.Core;

/// <summary>观察者查看矿藏的显示策略，不改变居民掌握的知识。</summary>
public enum ResourceVisibility
{
    /// <summary>显示已发现或已有聚落掌握开采技术的矿藏。</summary>
    Researched,

    /// <summary>显示全部矿藏。</summary>
    All,

    /// <summary>隐藏矿藏。</summary>
    None,
}
