namespace SeWZC.WorldBox.Core;

/// <summary>存档保留的粮食运输记录；当前居民贸易使用随身货物和行动目标。</summary>
public sealed class TradeRoute
{
    /// <summary>粮食运输记录的出发聚落 ID。</summary>
    public int FromSettlementId { get; set; }

    /// <summary>粮食运输记录的目的聚落 ID。</summary>
    public int ToSettlementId { get; set; }

    /// <summary>记录中的全程运输模拟日数。</summary>
    public int TravelTicks { get; set; }

    /// <summary>记录中的剩余运输模拟日数。</summary>
    public int RemainingTicks { get; set; }

    /// <summary>记录中的粮食货物数量。</summary>
    public double FoodCargo { get; set; }
}
