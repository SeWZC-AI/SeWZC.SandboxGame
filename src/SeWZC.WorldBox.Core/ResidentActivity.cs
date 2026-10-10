namespace SeWZC.WorldBox.Core;

/// <summary>居民当前身体状态或正在进行的活动。</summary>
public enum ResidentActivity
{
    /// <summary>漫游。</summary>
    Wandering,

    /// <summary>劳动。</summary>
    Working,

    /// <summary>饥饿。</summary>
    Hungry,

    /// <summary>行军。</summary>
    Marching,

    /// <summary>患病。</summary>
    Sick,

    /// <summary>进食。</summary>
    Eating,

    /// <summary>休息。</summary>
    Resting,

    /// <summary>交谈。</summary>
    Talking,

    /// <summary>递送。</summary>
    Delivering,

    /// <summary>学习。</summary>
    Studying,

    /// <summary>施法。</summary>
    Casting,

    /// <summary>逃离危险。</summary>
    Fleeing,

    /// <summary>睡眠。</summary>
    Sleeping,

    /// <summary>睡眠或体力耗尽导致的强制昏迷。</summary>
    Unconscious,
}
