namespace SeWZC.WorldBox.Core;

/// <summary>居民死亡时记录的直接原因。</summary>
public enum DeathCause
{
    /// <summary>尚未死亡。</summary>
    None,
    /// <summary>饥饿。</summary>
    Starvation,
    /// <summary>衰老。</summary>
    OldAge,
    /// <summary>火灾。</summary>
    Fire,
    /// <summary>疫病。</summary>
    Disease,
    /// <summary>战斗。</summary>
    Battle,
    /// <summary>战斗法术。</summary>
    Magic,
    /// <summary>陨石。</summary>
    Meteor,
    /// <summary>溺水。</summary>
    Drowning,
    /// <summary>地形变化。</summary>
    TerrainChange,
    /// <summary>局部冲突。</summary>
    Conflict,
    /// <summary>玩家干预。</summary>
    PlayerIntervention,
    /// <summary>脱水。</summary>
    Dehydration,
}
