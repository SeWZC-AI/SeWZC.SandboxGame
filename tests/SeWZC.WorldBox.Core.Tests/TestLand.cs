using SeWZC.WorldBox.Core;

internal static class TestLand
{
    // Command/form fixtures do not need the generated food web in every save.
    // Ecology and simulation fixtures retain their own actual populations.
    public static void ClearWildlife(WorldEngine engine)
    {
        foreach (var tile in engine.State.Tiles)
        { tile.Wildlife = WildlifeKind.None; tile.WildlifePopulation = 0; tile.OtherWildlife = default; }
    }

    // Controlled component fixtures start with an explicitly edited, connected town footprint.
    // Tests of claiming itself do not call this helper.
    public static void ClaimAllTowns(WorldEngine engine, int radius = 7)
    {
        foreach (var town in engine.State.Settlements.ToArray())
            engine.TransferTerritory(town.X, town.Y, town.NationId, radius);
    }
}
