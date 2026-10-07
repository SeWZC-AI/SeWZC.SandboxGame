namespace SeWZC.WorldBox.Core;

/// <summary>各类地形的基础环境参数目录。</summary>
public static class TerrainRules
{
    /// <summary>返回地形的基础通行、肥力、采集和魔力参数。</summary>
    /// <param name="terrain">地形类别。</param>
    public static TerrainParameters For(TerrainType terrain)
    {
        return terrain switch
        {
            TerrainType.DeepWater => new TerrainParameters(double.PositiveInfinity, 0, 0, 0, 0, 0, 0.5),
            TerrainType.Lake => new TerrainParameters(double.PositiveInfinity, 80, 0, 0, 0, 0, 1.7),
            TerrainType.Water => new TerrainParameters(double.PositiveInfinity, 5, 0, 0, 0, 0, 0.8),
            TerrainType.Sand => new TerrainParameters(1.4, 20, 0.12, 0, 0.25, 0.01, 0.4),
            TerrainType.DryFertile => new TerrainParameters(1.1, 85, 0.5, 0.04, 0.05, 0, .8),
            TerrainType.Grass => new TerrainParameters(1, 85, 0.5, 0.08, 0.05, 0, 1),
            TerrainType.Forest => new TerrainParameters(1.5, 75, 0.35, 0.7, 0.04, 0, 1.8),
            TerrainType.Mountain => new TerrainParameters(double.PositiveInfinity, 5, 0, 0, 0.8, 0.5, 1.4),
            TerrainType.Snow => new TerrainParameters(1.8, 8, 0.08, 0, 0.4, 0.25, 0.8),
            TerrainType.Hills => new TerrainParameters(1.6, 45, 0.2, 0.15, 0.6, 0.35, 1.3),
            TerrainType.Wetland => new TerrainParameters(2, 95, 0.65, 0.22, 0.02, 0, 2),
            TerrainType.Desert => new TerrainParameters(1.7, 8, 0.05, 0, 0.3, 0.12, 0.35),
            TerrainType.River => new TerrainParameters(double.PositiveInfinity, 80, 0.7, 0, 0.2, 0.03, 1.7),
            TerrainType.Tundra => new TerrainParameters(1.8, 20, 0.13, 0.08, 0.3, 0.15, 0.7),
            TerrainType.Stream => new TerrainParameters(2.5, 75, .3, 0, .1, 0, 1.5),
            TerrainType.LargeRiver => new TerrainParameters(double.PositiveInfinity, 80, .7, 0, .2, .03, 1.7),
            TerrainType.Meadow => new TerrainParameters(1, 90, .65, .05, .04, 0, 1.1),
            TerrainType.Woodland => new TerrainParameters(1.3, 65, .4, .45, .08, 0, 1.5),
            TerrainType.Rainforest => new TerrainParameters(2, 85, .5, .9, .03, 0, 2.2),
            TerrainType.Savanna => new TerrainParameters(1.2, 45, .28, .08, .08, 0, .8),
            TerrainType.Scrub => new TerrainParameters(1.4, 25, .12, .12, .25, .08, .6),
            TerrainType.Floodplain => new TerrainParameters(1.2, 95, .7, .12, .03, 0, 1.4),
            TerrainType.AlpineMeadow => new TerrainParameters(1.7, 50, .3, .08, .45, .2, 1.1),
            _ => new TerrainParameters(1, 50, 0.2, 0.1, 0.1, 0, 1),
        };
    }

    /// <summary>返回地形的基础肥力。</summary>
    /// <param name="terrain">地形类别。</param>
    public static byte Fertility(TerrainType terrain)
    {
        return For(terrain).Fertility;
    }

    /// <summary>返回地形基础步行成本，不可步行时为正无穷。</summary>
    /// <param name="terrain">地形类别。</param>
    public static double MovementCost(TerrainType terrain)
    {
        return For(terrain).MovementCost;
    }
}
