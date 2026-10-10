namespace SeWZC.WorldBox.Core;

/// <summary>居民当前选择的行动目标类别。</summary>
public enum AgentGoalKind
{
    /// <summary>空闲。</summary>
    Idle,

    /// <summary>进食。</summary>
    Eat,

    /// <summary>采集。</summary>
    Gather,

    /// <summary>劳动。</summary>
    Work,

    /// <summary>休息。</summary>
    Rest,

    /// <summary>逃离危险。</summary>
    Flee,

    /// <summary>社交。</summary>
    Socialize,

    /// <summary>递送消息。</summary>
    DeliverMessage,

    /// <summary>贸易。</summary>
    Trade,

    /// <summary>提交议题。</summary>
    Petition,

    /// <summary>学习研究。</summary>
    Study,

    /// <summary>训练魔法。</summary>
    TrainMagic,

    /// <summary>行军。</summary>
    March,

    /// <summary>返回家园。</summary>
    ReturnHome,

    /// <summary>迁徙。</summary>
    Migrate,

    /// <summary>探索。</summary>
    Explore,

    /// <summary>占领地块。</summary>
    ClaimLand,

    /// <summary>取水。</summary>
    FetchWater,

    /// <summary>狩猎。</summary>
    Hunt,

    /// <summary>捕鱼。</summary>
    Fish,

    /// <summary>扑灭火灾。</summary>
    ExtinguishFire,

    /// <summary>夜间返家或在途中睡眠。</summary>
    Sleep,

    /// <summary>把昏迷或尚不能自主出门的居民送回住所。</summary>
    Rescue,
}
