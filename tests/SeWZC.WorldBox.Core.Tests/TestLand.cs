using SeWZC.WorldBox.Core;

internal static class TestLand
{
    // Controlled component fixtures start with an explicitly edited, connected town footprint.
    // Tests of claiming itself do not call this helper.
    public static void ClaimAllTowns(WorldEngine engine, int radius = 7)
    {
        foreach (var town in engine.State.Settlements.ToArray())
            engine.TransferTerritory(town.X, town.Y, town.NationId, radius);
    }
}
