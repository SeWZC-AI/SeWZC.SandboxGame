using SeWZC.WorldBox.Core;

internal static class StrongTypeTests
{
    public static IEnumerable<(string Name, Action Run)> Cases =>
    [
        ("rule catalogs resist mutation and isolate editable cost copies", ImmutableRules),
        ("research actions dispatch the correct editor and settlement without label matching", ActionDispatch),
        ("immutable resource queries allocate no per-read storage", ResourceQueries),
    ];

    [UnitTest]
    private static void ImmutableRules()
    {
        foreach (var research in Advancement.All)
        {
            RejectMutation(research.Prerequisites, Advancement.SpatialMagic);
            RejectMutation(research.UnlockedBuildings, BuildingKind.Waygate);
            RejectMutation(research.UnlockedProfessions, Profession.Surveyor);
            RejectMutation(research.UnlockedSpells, SpellKind.RuneWard);
        }

        foreach (var recipe in ProductionRules.All)
            RejectMutation(recipe.InputResources, ResourceKind.Food);
        RejectMutation(Advancement.All, Advancement.SpatialMagic);

        var researchCost = WorldEngine.GetResearchCost(Advancement.Agriculture);
        var expectedFood = researchCost.Food;
        researchCost.Food = 0;
        Require(WorldEngine.GetResearchCost(Advancement.Agriculture).Food == expectedFood,
            "An editor modified the shared research cost");
        var buildingCost = WorldEngine.GetBuildingCost(BuildingKind.Foundry);
        var expectedWood = buildingCost.Wood;
        buildingCost.Wood = 0;
        Require(WorldEngine.GetBuildingCost(BuildingKind.Foundry).Wood == expectedWood,
            "An editor modified the shared building cost");

        foreach (var route in new[] { ResearchRoute.Technology, ResearchRoute.Magic })
            Require(ResearchRules.All.Where(route.Includes)
                .SequenceEqual(ResearchRules.Route(route.IsMagic)), "Typed routes disagree with the planner");
        Require(ResearchRules.All.Where(ResearchRoute.Common.Includes).All(d => d.Shared),
            "The common route includes exclusive research");
    }

    private static void RejectMutation<T>(IReadOnlyList<T> values, T replacement)
    {
        if (values.Count == 0 || values is not IList<T> list)
            return;
        try
        {
            list[0] = replacement;
        }
        catch (NotSupportedException)
        {
            return;
        }

        throw new InvalidOperationException("A rule collection is writable through IList");
    }

    [UnitTest]
    private static void ActionDispatch()
    {
        var handler = new ActionHandler();
        Advancement.RailTransport.Action!.Invoke(handler, 37);
        Require(handler.RailSettlement == 37 && handler.WaygateVisits == 0,
            "Rail research dispatched to the wrong editor or lost its settlement");
        Advancement.SpatialMagic.Action!.Invoke(handler, 51);
        Require(handler.RailSettlement == 37 && handler.WaygateVisits == 1,
            "Waygate research changed the rail editor context");
    }

    [UnitTest]
    private static void ResourceQueries()
    {
        var amounts = new ResourceAmounts { Food = 20, Water = 5, Ammunition = 8 };
        var stock = amounts.Copy();
        foreach (var kind in ResourceStock.Kinds)
            Require(stock.Get(kind) == amounts.Get(kind), "A resource was lost in the editable copy");
        stock.Ammunition = 0;
        Require(amounts.Ammunition == 8, "Editing a copy changed immutable amounts");
        var sum = 0d;
        for (var i = 0; i < 1000; i++)
            sum += amounts.Get(ResourceKind.Food);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
            sum += amounts.Get(ResourceKind.Food);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Require(allocated == 0 && sum == 40_000, "Immutable queries allocate per-read storage");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class ActionHandler : IResearchActionHandler
    {
        public int RailSettlement { get; private set; }
        public int WaygateVisits { get; private set; }

        public void ShowRailEditor(int settlementId)
        {
            RailSettlement = settlementId;
        }

        public void ShowWaygateEditor()
        {
            WaygateVisits++;
        }
    }
}
