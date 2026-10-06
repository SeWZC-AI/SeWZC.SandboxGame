namespace SeWZC.WorldBox.Core;

/// <summary>居民观察、记忆或转述的信息议题类别。</summary>
public enum AgentFactKind
{
    /// <summary>食物供给。</summary>
    FoodSupply,
    /// <summary>危险。</summary>
    Danger,
    /// <summary>聚落位置。</summary>
    SettlementLocation,
    /// <summary>求援。</summary>
    ReliefRequest,
    /// <summary>政策。</summary>
    Policy,
    /// <summary>战争命令。</summary>
    WarOrder,
    /// <summary>停战命令。</summary>
    PeaceOrder,
    /// <summary>文化。</summary>
    Culture,
    /// <summary>研究知识。</summary>
    Research,
    /// <summary>个人消息。</summary>
    Personal,
    /// <summary>贸易交换。</summary>
    TradeExchange,
    /// <summary>外交通知。</summary>
    DiplomaticNotice,
    /// <summary>战报。</summary>
    WarReport,
    /// <summary>水源。</summary>
    WaterSource,
    /// <summary>建村地点。</summary>
    FoundingSite,
}
