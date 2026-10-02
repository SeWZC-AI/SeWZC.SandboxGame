using System.Reflection;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using SeWZC.WorldBox.Core;
using SeWZC.WorldBox.UI;
using SeWZC.WorldBox.UI.Controls;

AppBuilder.Configure<TestApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions()).SetupWithoutStarting();

var tests = new (string Name, Action Test)[]
{
    ("Infrastructure refresh recovers when its town disappears", () => TownDisappears("infrastructure", false)),
    ("Closed communication inspector recovers when its town disappears", () => TownDisappears("communication", true)),
    ("Nation rename preserves exact local stocks", () => RenamePreservesStocks(false)),
    ("Nation rename preserves totals above the editable resource limit", () => RenamePreservesStocks(true)),
    ("Editing one national resource preserves other local stocks", EditOneResource),
    ("Partial national resource commands validate atomically", ResourceValidation),
    ("Archived identity edits retain historical home and army", ArchivedIdentity),
    ("Replacing and undoing a world cancel pending touch placement", ReplaceWorldPlacement),
    ("Out-of-bounds placement cannot mutate a world", PlacementBounds),
    ("Abandoned map picker cannot reopen an empty modal", MapPickerLifecycle)
};
var failures = 0;
foreach (var (name, test) in tests)
{
    try { test(); Console.WriteLine($"PASS {name}"); }
    catch (Exception error)
    {
        while (error is TargetInvocationException { InnerException: { } inner }) error = inner;
        failures++; Console.Error.WriteLine($"FAIL {name}: {error}");
    }
}
Console.WriteLine($"{tests.Length - failures}/{tests.Length} UI checks passed");
return failures == 0 ? 0 : 1;

static void TownDisappears(string mode, bool closeInspector)
{
    var engine = TwoTownWorld();
    var first = engine.State.Settlements[0];
    var survivor = engine.State.Settlements[1];
    var resident = engine.State.Residents.Single(r => r.SettlementId == first.Id);
    resident.Health = .1; resident.Age = 91;
    engine.State.Rules.Births = false; engine.State.Rules.Hunger = false; engine.State.Rules.Aging = true;
    var view = View(engine);
    Call(view, "OpenInspector", mode, true);
    Call(view, "RefreshUi", true);
    var initialContent = Field<ScrollViewer>(view, "_inspectorScroll").Content;
    Call(view, "RefreshUi", true);
    Assert(ReferenceEquals(initialContent, Field<ScrollViewer>(view, "_inspectorScroll").Content), "Ordinary refresh must preserve existing controls");
    if (closeInspector) Call(view, "CloseInspector");

    engine.Step();
    Assert(engine.State.Settlements.Count == 1, "The selected town must disappear during simulation");
    Call(view, "RefreshUi", true);
    Assert(Field<int>(view, "_inspectorSettlementId") == survivor.Id, "Inspector must select a surviving town");
    var replacement = Field<ScrollViewer>(view, "_inspectorScroll").Content;
    Call(view, "RefreshUi", true);
    Assert(ReferenceEquals(replacement, Field<ScrollViewer>(view, "_inspectorScroll").Content), "Recovery must not cause repeated control rebuilding");

    var last = engine.State.Residents.Single(); last.Health = .1; last.Age = 91;
    engine.Step();
    Assert(engine.State.Settlements.Count == 0, "Last town must also disappear");
    Call(view, "RefreshUi", true);
    Assert(Field<int>(view, "_inspectorSettlementId") == 0, "Empty worlds must clear the selected town");
    var emptyContent = Field<ScrollViewer>(view, "_inspectorScroll").Content;
    Call(view, "RefreshUi", true);
    Assert(ReferenceEquals(emptyContent, Field<ScrollViewer>(view, "_inspectorScroll").Content), "Empty state must also retain stable controls");
}

static void RenamePreservesStocks(bool largeTotal)
{
    var engine = TwoTownWorld(largeTotal);
    var before = Stocks(engine);
    var view = View(engine);
    Call(view, "ShowNationEditor", engine.State.Nations.Single().Id);
    Control<TextBox>(view, "nation-name").Text = "Rename only";
    Click(view, "nation-apply");
    Assert(engine.State.Nations.Single().Name == "Rename only", "Name must be applied");
    Assert(Stocks(engine).SequenceEqual(before), "Name-only changes must preserve every exact local amount");
}

static void EditOneResource()
{
    var engine = TwoTownWorld();
    var before = Stocks(engine);
    var view = View(engine);
    Call(view, "ShowNationEditor", engine.State.Nations.Single().Id);
    Control<NumericUpDown>(view, "nation-food").Value = 300.5m;
    Click(view, "nation-apply");
    var after = Stocks(engine);
    for (var i = 0; i < after.Length; i++)
    {
        Assert(after[i].Food == 150.25, "Edited total must be distributed across the nation's towns");
        Assert((after[i].Wood, after[i].Stone, after[i].Ore) == (before[i].Wood, before[i].Stone, before[i].Ore), "Untouched resources must retain their local distribution and fractions");
    }
}

static void ResourceValidation()
{
    var engine = TwoTownWorld();
    var id = engine.State.Nations.Single().Id;
    var before = engine.ExportJson();
    engine.SetNationResources(id);
    Assert(engine.ExportJson() == before, "An empty resource patch must not create changes or events");
    try { engine.SetNationResources(id, food: 10, wood: double.NaN); throw new Exception("Invalid resource was accepted"); }
    catch (ArgumentOutOfRangeException) { }
    Assert(engine.ExportJson() == before, "All supplied amounts must be validated before modifying any stock");
}

static void ArchivedIdentity()
{
    var engine = TwoTownWorld();
    var resident = engine.State.Residents[0];
    var homeId = resident.SettlementId;
    resident.Health = .1; resident.Age = 91;
    engine.State.Rules.Births = false; engine.State.Rules.Aging = true;
    engine.Step();
    var archived = engine.State.ArchivedResidents.Single(person => person.Id == resident.Id);
    // Historical snapshots may legitimately retain an army that has since disbanded.
    archived.ArmyId = 123456;
    Assert(engine.State.Settlements.All(town => town.Id != homeId), "Fixture needs a vanished home");
    engine.PaintTerrain(archived.X, archived.Y, TerrainType.Water, 0);
    var view = View(engine);
    Call(view, "ShowResidentEditor", archived.Id);
    // These headless controls have no theme/template, so tab content is not realized
    // in the logical tree. Use the actual identity form supplied to the tab instead.
    var tabs = Control<TabControl>(view, "resident-editor-tabs");
    var identity = (StackPanel)tabs.ItemsSource!.Cast<TabItem>()
        .Single(tab => AutomationProperties.GetAutomationId(tab) == "resident-tab-identity").Content!;
    identity.Children.OfType<TextBox>().Single(input => AutomationProperties.GetAutomationId(input) == "resident-name").Text = "Remembered resident";
    Click(view, "resident-apply");
    var edited = engine.State.ArchivedResidents.Single(person => person.Id == resident.Id);
    Assert(edited.Name == "Remembered resident", "Historical identity edit must succeed without a living home or army");
    Assert(edited.SettlementId == homeId && edited.ArmyId == 123456, "Name-only edits must retain historical references");
    Assert(!Field<Border>(view, "_modal").IsVisible, "Successful archive edit must close the form");
}

static void ReplaceWorldPlacement()
{
    var big = WorldEngine.Create(73921, 256, 256, false);
    big.PaintTerrain(80, 200, TerrainType.Grass, 2);
    var view = View(big);
    var map = Map(view);
    Preview(map, 80, 200);
    var small = WorldEngine.Create(42, 128, 128, false);
    Call(view, "ReplaceWorld", small);
    Assert(!map.HasPendingPlacement, "Replacing the world must remove old pending placement");
    Assert(!Field<Border>(view, "_placementBar").IsVisible, "The old confirmation bar must disappear");
    var before = small.ExportJson();
    map.ConfirmPlacement();
    Assert(small.ExportJson() == before, "A late confirmation must not affect the replacement world");

    Preview(map, 10, 10);
    Call(view, "RestoreCheckpoint");
    Assert(map.Engine!.State.Width == 256 && !map.HasPendingPlacement, "Undoing world replacement must also clear pending placement");
}

static void PlacementBounds()
{
    var engine = WorldEngine.Create(42, 128, 128, false);
    var map = Map(View(engine));
    map.ActiveTool = "Human";
    var before = engine.ExportJson();
    foreach (var tile in new[] { (128, 0), (0, 128), (-1, 0), (0, -1) })
    {
        // Model a late confirmation carrying stale coordinates; no input path may index them.
        typeof(WorldMapControl).GetField("_pendingPlacement", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(map, tile);
        map.ConfirmPlacement();
    }
    Assert(engine.ExportJson() == before, "Invalid placement must leave the entire world unchanged");
}

static void MapPickerLifecycle()
{
    var engine = TwoTownWorld();
    var view = View(engine);
    var resident = engine.State.Residents[0];
    Call(view, "ShowGoalEditor", resident.Id);
    Click(view, "map-pick-resident_goal_x");
    Assert(Map(view).PickingLocation && !Field<Border>(view, "_modal").IsVisible, "Map picking must temporarily hide its form");
    Call(view, "FinishMapPick", 20, 21);
    Assert(Control<NumericUpDown>(view, "resident-goal-x").Value == 20 && Field<Border>(view, "_modal").IsVisible, "Normal picking must restore the form with its selected location");
    Click(view, "map-pick-resident_goal_x");
    Call(view, "ShowNewWorld");
    Assert(!Map(view).PickingLocation, "Opening another modal must abandon the old map picker");
    Call(view, "ReplaceWorld", WorldEngine.Create(42, 128, 128, false));
    Call(view, "CloseModal");
    Call(view, "FinishMapPick", 10, 10);
    Assert(!Field<Border>(view, "_modal").IsVisible, "A stale map pick must not reopen an empty modal");
}

static WorldEngine TwoTownWorld(bool largeTotal = false)
{
    var engine = WorldEngine.Create(42, 128, 128, false);
    engine.PaintTerrain(32, 32, TerrainType.Grass, 5);
    engine.PaintTerrain(96, 96, TerrainType.Grass, 5);
    engine.SpawnResidents(32, 32, RaceKind.Human, 1);
    engine.SpawnResidents(96, 96, RaceKind.Human, 1);
    var towns = engine.State.Settlements.ToArray();
    Assert(towns.Length == 2, "Fixture requires two settlements");
    towns[0].Resources = new ResourceStock { Food = largeTotal ? 900_000.25 : 100.2, Wood = 70.125, Stone = 8.3, Ore = 10.4 };
    towns[1].Resources = new ResourceStock { Food = largeTotal ? 900_000.5 : .2, Wood = 1.75, Stone = .1, Ore = .375 };
    engine.TransferTerritory(towns[1].X, towns[1].Y, towns[0].NationId, 0);
    return engine;
}

static (double Food, double Wood, double Stone, double Ore)[] Stocks(WorldEngine engine) => engine.State.Settlements
    .Select(town => (town.Resources.Food, town.Resources.Wood, town.Resources.Stone, town.Resources.Ore)).ToArray();
static MainView View(WorldEngine engine) { var view = new MainView(); Call(view, "ReplaceWorld", engine); return view; }
static WorldMapControl Map(MainView view) { var map = Field<WorldMapControl>(view, "_map"); map.Measure(new Size(800, 800)); map.Arrange(new Rect(0, 0, 800, 800)); return map; }
static void Preview(WorldMapControl map, int x, int y) { map.ActiveTool = "Human"; Call(map, "PreviewPlacement", map.GetTileScreenPosition(x, y), true); Assert(map.HasPendingPlacement, "Fixture must create a touch preview"); }
static object? Call(object target, string name, params object?[] args) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, args);
static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;
static T Control<T>(MainView view, string id) where T : Avalonia.Controls.Control => view.GetLogicalDescendants().OfType<T>().Single(control => AutomationProperties.GetAutomationId(control) == id);
static void Click(MainView view, string id) => Control<Button>(view, id).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }

public sealed class TestApp : Application;
