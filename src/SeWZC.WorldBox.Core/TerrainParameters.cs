namespace SeWZC.WorldBox.Core;

/// <summary>地形的基础通行成本、肥力、采集产量和魔力恢复参数。</summary>
/// <param name="MovementCost">基础步行成本，正无穷表示不可步行。</param>
/// <param name="Fertility">生成地形时的基础肥力。</param>
/// <param name="FoodYield">基础食物采集产量系数。</param>
/// <param name="WoodYield">基础木材采集产量系数。</param>
/// <param name="StoneYield">基础石材采集产量系数。</param>
/// <param name="OreYield">基础矿石采集产量系数。</param>
/// <param name="ManaRate">本地魔力恢复速率系数。</param>
public readonly record struct TerrainParameters(
    double MovementCost,
    byte Fertility,
    double FoodYield,
    double WoodYield,
    double StoneYield,
    double OreYield,
    double ManaRate);
