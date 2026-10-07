using SeWZC.WorldBox.Core;

internal static class TestLand
{
    // 命令和表单夹具省略动物种群。
    public static void ClearWildlife(WorldEngine engine)
    {
        foreach (var tile in engine.State.Tiles)
        {
            tile.Wildlife = WildlifeKind.None;
            tile.WildlifePopulation = 0;
            tile.OtherWildlife = default;
        }
    }

    // 预先登记连通领地；领地登记测试不使用此辅助方法。
    public static void ClaimAllTowns(WorldEngine engine, int radius = 7)
    {
        foreach (var town in engine.State.Settlements.ToArray())
            engine.TransferTerritory(town.X, town.Y, town.NationId, radius);
    }
}
