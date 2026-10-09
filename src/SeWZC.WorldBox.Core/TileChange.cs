namespace SeWZC.WorldBox.Core;

/// <summary>一次地格转换影响的派生查询类别，只比较相关输入。</summary>
internal readonly record struct TileChange
{
    internal bool Plants { get; init; }
    internal bool Population { get; init; }
    internal bool Habitat { get; init; }
    internal bool Nation { get; init; }
    internal bool Ownership { get; init; }
    internal bool Claims { get; init; }
    internal bool Traversal { get; init; }

    internal static TileChange Between(Tile before, Tile after)
    {
        var terrain = before.Terrain != after.Terrain;
        var improvement = before.Improvement != after.Improvement;
        var fertility = before.Fertility != after.Fertility;
        var plants = before.Plants != after.Plants;
        var drought = before.DroughtTicks > 0 != after.DroughtTicks > 0;
        var fire = before.FireTicks > 0 != after.FireTicks > 0;
        var nation = before.NationId != after.NationId;
        var claims = before.ClaimedSettlementId != after.ClaimedSettlementId;
        return new TileChange
        {
            Plants = terrain || improvement || fertility || plants || drought
                     || before.ResourceAmount != after.ResourceAmount,
            Population = before.Wildlife != after.Wildlife || before.WildlifePopulation != after.WildlifePopulation
                         || !before.SameOtherWildlife(after),
            Habitat = terrain || improvement || fertility || plants || drought || fire
                      || before.NaturalWaterYield != after.NaturalWaterYield
                      || before.SettlementId != 0 != (after.SettlementId != 0)
                      || Math.Min(100, before.ResourceAmount) != Math.Min(100, after.ResourceAmount),
            Nation = nation,
            Ownership = nation || claims,
            Claims = claims || WorldEngine.IsWaterTerrain(before.Terrain) != WorldEngine.IsWaterTerrain(after.Terrain),
            Traversal = terrain || improvement || fire || before.BridgeDirection != after.BridgeDirection,
        };
    }
}
