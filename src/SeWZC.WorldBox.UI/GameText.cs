using SeWZC.WorldBox.Core;

namespace SeWZC.WorldBox.UI;

internal static class GameText
{
    internal static string Date(long tick)
    {
        return tick < 0
            ? "尚无记录"
            : $"第 {1 + tick / SimulationTime.TicksPerYear} 年 {1 + tick / SimulationTime.TicksPerMonth % SimulationTime.MonthsPerYear} 月 {1 + tick / SimulationTime.TicksPerDay % SimulationTime.DaysPerMonth} 日   第 {SimulationTime.TimeOfDay(tick)} 刻";
    }

    internal static string Terrain(TerrainType terrain)
    {
        return terrain switch
        {
            TerrainType.DeepWater => "深海",
            TerrainType.Water => "浅海",
            TerrainType.Sand => "沙地",
            TerrainType.Grass => "草地",
            TerrainType.Forest => "森林",
            TerrainType.Mountain => "山脉",
            TerrainType.Snow => "雪原",
            TerrainType.Hills => "丘陵",
            TerrainType.Wetland => "湿地",
            TerrainType.Desert => "荒漠",
            TerrainType.River => "河流",
            TerrainType.Lake => "湖泊",
            TerrainType.DryFertile => "旱原",
            TerrainType.Stream => "小溪",
            TerrainType.LargeRiver => "江",
            TerrainType.Meadow => "草甸",
            TerrainType.Woodland => "疏林",
            TerrainType.Rainforest => "雨林",
            TerrainType.Savanna => "稀树草原",
            TerrainType.Scrub => "灌丛",
            TerrainType.Floodplain => "河漫滩",
            TerrainType.AlpineMeadow => "高山草甸",
            _ => "苔原",
        };
    }
}
