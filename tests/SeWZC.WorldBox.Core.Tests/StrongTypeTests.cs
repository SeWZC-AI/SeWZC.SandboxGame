using SeWZC.WorldBox.Core;

internal static class StrongTypeTests
{
    public static IEnumerable<(string Name, Action Run)> Cases =>
    [
        ("rule definitions isolate caller-owned arrays and editable cost copies", ImmutableRules),
        ("research actions dispatch the correct editor and settlement without label matching", ActionDispatch),
        ("immutable resource queries allocate no per-read storage", ResourceQueries),
    ];

    [UnitTest]
    private static void ImmutableRules()
    {
        ResearchKind[] prerequisites = [ResearchKind.Agriculture];
        BuildingKind[] buildings = [BuildingKind.Farm];
        Profession[] professions = [Profession.Farmer];
        SpellKind[] spells = [SpellKind.FrostBolt];
        var cost = new ResourceAmounts { Food = 20, Wood = 15 };
        var definition = new ResearchDefinition(ResearchKind.Irrigation, "独立测试节点", ResearchBranch.Resources,
            "基础", false, prerequisites, cost, 60, "测试", buildings, professions, spells);
        var advancement = new Advancement(ResearchKind.Industry, "独立测试配方", "工业", false, prerequisites,
            cost, BuildingKind.Foundry, "冶炼厂", cost, new ResourceAmounts { Ore = 2 }, ResourceKind.Alloy, 1);
        prerequisites[0] = ResearchKind.SpatialMagic;
        buildings[0] = BuildingKind.Waygate;
        professions[0] = Profession.Surveyor;
        spells[0] = SpellKind.RuneWard;
        Require(definition.Prerequisites.Single() == ResearchKind.Agriculture
                && advancement.Prerequisites.Single() == ResearchKind.Agriculture
                && definition.UnlockedBuildings.Single() == BuildingKind.Farm
                && definition.UnlockedProfessions.Single() == Profession.Farmer
                && definition.UnlockedSpells.Single() == SpellKind.FrostBolt,
            "Caller-owned arrays changed the shared rules");
        RejectMutation(definition.Prerequisites, ResearchKind.SpatialMagic);
        RejectMutation(advancement.Prerequisites, ResearchKind.SpatialMagic);
        RejectMutation(definition.UnlockedBuildings, BuildingKind.Waygate);
        RejectMutation(definition.UnlockedProfessions, Profession.Surveyor);
        RejectMutation(definition.UnlockedSpells, SpellKind.RuneWard);
        RejectMutation(advancement.InputResources, ResourceKind.Food);

        var researchCost = WorldEngine.GetResearchCost(ResearchKind.Agriculture);
        var expectedFood = researchCost.Food;
        researchCost.Food = 0;
        Require(WorldEngine.GetResearchCost(ResearchKind.Agriculture).Food == expectedFood,
            "An editor modified the shared research cost");
        var buildingCost = WorldEngine.GetBuildingCost(BuildingKind.Foundry);
        var expectedWood = buildingCost.Wood;
        buildingCost.Wood = 0;
        Require(WorldEngine.GetBuildingCost(BuildingKind.Foundry).Wood == expectedWood,
            "An editor modified the shared building cost");

        foreach (var route in new[] { ResearchRoute.Technology, ResearchRoute.Magic })
            Require(ResearchRules.All.Where(route.Includes).Select(d => d.Kind)
                .SequenceEqual(ResearchRules.Route(route.IsMagic)), "Typed routes disagree with the planner");
        Require(ResearchRules.All.Where(ResearchRoute.Common.Includes).All(d => d.Shared),
            "The common route includes exclusive research");
    }

    private static void RejectMutation<T>(IReadOnlyList<T> values, T replacement)
    {
        if (values is not IList<T> list) return;
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
        ResearchRules.For(ResearchKind.RailTransport).Action!.Invoke(handler, 37);
        Require(handler.RailSettlement == 37 && handler.WaygateVisits == 0,
            "Rail research dispatched to the wrong editor or lost its settlement");
        ResearchRules.For(ResearchKind.SpatialMagic).Action!.Invoke(handler, 51);
        Require(handler.RailSettlement == 37 && handler.WaygateVisits == 1,
            "Waygate research changed the rail editor context");
    }

    [UnitTest]
    private static void ResourceQueries()
    {
        var amounts = new ResourceAmounts { Food = 20, Water = 5, Ammunition = 8 };
        var stock = amounts.Copy();
        foreach (var kind in AdvancementRules.Resources)
            Require(stock.Get(kind) == amounts.Get(kind), "A resource was lost in the editable copy");
        stock.Ammunition = 0;
        Require(amounts.Ammunition == 8, "Editing a copy changed immutable amounts");
        var sum = 0d;
        for (var i = 0; i < 1000; i++) sum += amounts.Get(ResourceKind.Food);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++) sum += amounts.Get(ResourceKind.Food);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Require(allocated == 0 && sum == 40_000, "Immutable queries allocate per-read storage");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
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
