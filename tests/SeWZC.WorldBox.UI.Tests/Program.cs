using System.Reflection;
using System.Text.Json;
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
    ("Paused goal edits refresh the selected resident route immediately", GoalRouteRefresh),
    ("Empty rule numbers stay in the form without changing world state", EmptyRuleNumber),
    ("Resident detail folds survive refresh and keep targets accessible", FoldedDetails),
    ("Close zoom and route presentation preserve world state", CloseZoom),
    ("Tile form edits resources and rejects invalid road input atomically", TileForm),
    ("Infrastructure refresh recovers when its town disappears", () => TownDisappears("infrastructure", false)),
    ("Closed communication inspector recovers when its town disappears", () => TownDisappears("communication", true)),
    ("Nation rename preserves exact local stocks", () => RenamePreservesStocks(false)),
    ("Nation rename preserves totals above the editable resource limit", () => RenamePreservesStocks(true)),
    ("Editing one national resource preserves other local stocks", EditOneResource),
    ("Partial national resource commands validate atomically", ResourceValidation),
    ("Archived identity edits retain historical home and army", ArchivedIdentity),
    ("Personality edits preserve automatic building work targets", AutomaticWorkGoal),
    ("Study and magic goals select facilities of the appropriate kind", ResearchAndMagicGoals),
    ("Personality edits preserve goals whose historical targets disappeared", HistoricalGoals),
    ("Personality edits preserve active delivery tasks and supplies", ActiveMissionGoal),
    ("Invalid goal edits leave the complete world unchanged", InvalidGoalEdit),
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

static void GoalRouteRefresh()
{
    var engine = TwoTownWorld(); var view = View(engine); var resident = engine.State.Residents[0];
    var map = Map(view);
    Call(view, "OpenResident", resident.Id); Call(view, "ShowGoalEditor", resident.Id);
    Control<ComboBox>(view, "resident-goal").SelectedItem = AgentGoalKind.Work;
    Control<ComboBox>(view, "resident-goal-entity").SelectedIndex = 0;
    Control<NumericUpDown>(view, "resident-goal-x").Value = 35;
    Control<NumericUpDown>(view, "resident-goal-y").Value = 32;
    Click(view, "resident-goal-apply");
    Assert(Field<IReadOnlyList<RoutePoint>>(map, "_selectedRoute").Count > 1, "Paused edit left the cached route empty");
    Assert(engine.State.Tick == 0, "Refreshing the preview advanced simulation");
}

static void EmptyRuleNumber()
{
    var engine = TwoTownWorld(); var view = View(engine);
    Call(view, "ShowRules");
    var before = engine.ExportJson();
    Control<NumericUpDown>(view, "rule-gathering-rate").Value = null;
    Click(view, "world-rules-apply");
    Assert(engine.ExportJson() == before && Field<Border>(view, "_modal").IsVisible, "Empty input changed world or closed the form");
}

static void FoldedDetails()
{
    var engine = TwoTownWorld(); var view = View(engine);
    Call(view, "OpenResident", engine.State.Residents[0].Id);
    var fold = Control<Expander>(view, "resident-cognition");
    Assert(!fold.IsExpanded && !Control<Expander>(view, "resident-history").IsExpanded, "Secondary lists should start folded");
    fold.IsExpanded = true; Call(view, "RefreshUi", true);
    Assert(ReferenceEquals(fold, Control<Expander>(view, "resident-cognition")) && fold.IsExpanded, "Timed refresh rebuilt or collapsed content");
    Call(view, "OpenInspector", "overview", true);
    Call(view, "OpenResident", engine.State.Residents[0].Id);
    Assert(Control<Expander>(view, "resident-cognition").IsExpanded, "Navigation lost fold preference");
    Assert(Control<Button>(view, "resident-goal-edit").IsEnabled, "Goal actions unavailable");
}

static void CloseZoom()
{
    var engine = TwoTownWorld(); var map = Map(View(engine));
    var before = engine.ExportJson();
    map.SelectResident(engine.State.Residents[0].Id);
    for (var i = 0; i < 30; i++) map.ZoomIn();
    var size = map.GetTileScreenPosition(1, 0).X - map.GetTileScreenPosition(0, 0).X;
    Assert(Math.Abs(size - 24 * 8) < .001, "Close zoom should reach 24x");
    map.RefreshWorld();
    Assert(engine.ExportJson() == before, "Observation changed simulation");
}

static void TileForm()
{
    var engine = TwoTownWorld(); var view = View(engine);
    Call(view, "ShowTileEditor", 32, 32);
    Control<NumericUpDown>(view, "tile-resources").Value = 12.5m;
    Control<NumericUpDown>(view, "tile-fertility").Value = 0;
    Control<NumericUpDown>(view, "tile-road").Value = 3;
    Click(view, "tile-apply");
    var tile = engine.State.Tiles[32 * 128 + 32];
    Assert(tile.ResourceAmount == 12.5 && tile.Fertility == 0 && tile.RoadLevel == 3, "Tile form did not commit values");
    Call(view, "ShowTileEditor", 32, 32);
    var before = engine.ExportJson();
    Control<NumericUpDown>(view, "tile-resources").Value = 50;
    Control<NumericUpDown>(view, "tile-road").Value = 1.5m;
    Click(view, "tile-apply");
    Assert(engine.ExportJson() == before && Field<Border>(view, "_modal").IsVisible, "Invalid form applied partially or closed");
}

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

static void AutomaticWorkGoal()
{
    var (engine, id, building) = WorkingWorld(Profession.Farmer);
    Assert(engine.GetResident(id)!.Agent.Goal.Kind == AgentGoalKind.Work, "The farmer must autonomously choose building work");
    EditPersonalityOnly(engine, id, building.Id);
}

static void ResearchAndMagicGoals()
{
    foreach (var profession in new[] { Profession.Scholar, Profession.Mage })
    {
        var (engine, id, building) = WorkingWorld(profession);
        var kind = profession == Profession.Scholar ? AgentGoalKind.Study : AgentGoalKind.TrainMagic;
        Assert(engine.GetResident(id)!.Agent.Goal.Kind == kind, "Specialists must autonomously target the expected work kind");
        var view = EditPersonalityOnly(engine, id, building.Id);
        Click(view, "resident-goal-edit");
        var choices = Control<ComboBox>(view, "resident-goal-entity").ItemsSource!.Cast<object>().Select(ChoiceId).Where(value => value != 0).ToArray();
        Assert(choices.Length > 0 && choices.All(value => engine.State.Society.Buildings.Any(b => b.Id == value
            && b.Kind == (kind == AgentGoalKind.Study ? BuildingKind.Academy : BuildingKind.ArcaneSanctum))),
            "The goal picker offered residents or an inappropriate facility type");
        var before = engine.ExportJson();
        Control<ComboBox>(view, "resident-goal").SelectedItem = AgentGoalKind.Work;
        Assert(engine.ExportJson() == before, "Changing the form's goal type must not start a task before application");
        Assert(Control<ComboBox>(view, "resident-goal-entity").ItemsSource!.Cast<object>().Select(ChoiceId)
            .Any(value => engine.State.Society.Buildings.Any(b => b.Id == value && b.Kind == BuildingKind.Farm)),
            "Switching to work did not rebuild the facility choices");
        Call(view, "CloseModal");
    }
}

static void HistoricalGoals()
{
    var (engine, id, building) = WorkingWorld(Profession.Farmer);
    var home = engine.State.Settlements.Single();
    var returning = engine.State.Residents.First(person => person.Id != id);
    var mind = JsonSerializer.Deserialize<AgentState>(engine.ExportResidentMind(returning.Id))!;
    mind.Goal = new AgentGoal { Kind = AgentGoalKind.ReturnHome, TargetSettlementId = home.Id,
        TargetX = home.X, TargetY = home.Y, StartedTick = engine.State.Tick, ReviewTick = engine.State.Tick + 20 };
    engine.EditResident(returning.Id, new ResidentEdit { Agent = mind });
    engine.SpawnResidents(44, 32, RaceKind.Elf, 2);
    foreach (var resident in engine.State.Residents.Where(person => person.SettlementId == home.Id).ToArray())
        engine.EditResident(resident.Id, new ResidentEdit { Age = 91, Health = .1 });
    engine.Tick();
    Assert(engine.State.Settlements.All(town => town.Id != home.Id)
        && engine.State.Society.Buildings.All(item => item.Id != building.Id), "The historical targets must disappear normally with their empty town");
    Assert(engine.State.ArchivedResidents.Any(person => person.Id == id)
        && engine.State.ArchivedResidents.Any(person => person.Id == returning.Id), "The fixture must retain both deceased residents");
    EditPersonalityOnly(engine, id, building.Id);
    var view = EditPersonalityOnly(engine, returning.Id, 0);
    Click(view, "resident-goal-edit");
    Assert(ChoiceId(Control<ComboBox>(view, "resident-goal-town").SelectedItem!) == home.Id,
        "The extinct target town was replaced with a living town or cleared");
    Call(view, "CloseModal");
}

static void ActiveMissionGoal()
{
    var engine = TwoTownWorld();
    var courier = engine.State.Residents[0];
    var home = engine.State.Settlements.First(town => town.Id == courier.SettlementId);
    var target = engine.State.Settlements.First(town => town.Id != home.Id);
    var mind = JsonSerializer.Deserialize<AgentState>(engine.ExportResidentMind(courier.Id))!;
    mind.Goal = new AgentGoal { Kind = AgentGoalKind.DeliverMessage, TargetSettlementId = target.Id,
        TargetX = target.X, TargetY = target.Y, ReviewTick = 100, PlayerDirected = true };
    engine.EditResident(courier.Id, new ResidentEdit { X = home.X, Y = home.Y, Agent = mind });
    engine.Tick();
    Assert(engine.GetResident(courier.Id)!.Agent.DestinationSettlementId == target.Id, "The courier must have an actual active task");
    EditPersonalityOnly(engine, courier.Id, 0);
}

static void InvalidGoalEdit()
{
    var (engine, id, _) = WorkingWorld(Profession.Farmer);
    var view = View(engine);
    Call(view, "OpenResident", id);
    Click(view, "resident-goal-edit");
    var before = engine.ExportJson();
    Control<TextBox>(view, "resident-goal-reason").Text = new string('x', 401);
    Control<NumericUpDown>(view, "resident-courage").Value = .01m;
    Click(view, "resident-goal-apply");
    Assert(Field<Border>(view, "_modal").IsVisible && engine.ExportJson() == before,
        "An invalid goal reason partially committed its accompanying personality edit");
}

static MainView EditPersonalityOnly(WorldEngine engine, int id, int targetEntity)
{
    var person = engine.GetResident(id)!;
    var beforeGoal = JsonSerializer.Serialize(person.Agent.Goal);
    var beforeMission = MissionSnapshot(person);
    var beforeStocks = Stocks(engine);
    var view = View(engine);
    Call(view, "OpenResident", id);
    Click(view, "resident-goal-edit");
    Assert(ChoiceId(Control<ComboBox>(view, "resident-goal-entity").SelectedItem!) == targetEntity,
        "The current facility or historical reference is not selected in the real goal form");
    Control<NumericUpDown>(view, "resident-courage").Value = .01m;
    Click(view, "resident-goal-apply");
    person = engine.GetResident(id)!;
    Assert(person.Agent.Personality.Courage == .01 && !Field<Border>(view, "_modal").IsVisible,
        "The real goal form failed to apply a personality-only edit");
    Assert(JsonSerializer.Serialize(person.Agent.Goal) == beforeGoal,
        "A personality-only edit rewrote the goal's reference, progress, deadline or player-directed flag");
    Assert(MissionSnapshot(person) == beforeMission && Stocks(engine).SequenceEqual(beforeStocks),
        "A personality-only edit restarted a mission or altered carried goods, messages or town supplies");
    var saved = engine.ExportJson();
    Assert(WorldEngine.ImportJson(saved).ExportJson() == saved, "The edited goal or historical reference failed its exact save roundtrip");
    return view;
}

static string MissionSnapshot(Resident person) => JsonSerializer.Serialize(new { person.Inventory,
    person.Agent.DestinationSettlementId, person.Agent.MissionOriginSettlementId, person.Agent.MissionStartedTick,
    person.Agent.MissionRetryTick, person.Agent.CarriedMessages });
static int ChoiceId(object choice) => (int)choice.GetType().GetProperty("Id")!.GetValue(choice)!;

static (WorldEngine Engine, int ResidentId, Building Target) WorkingWorld(Profession profession)
{
    var engine = WorldEngine.Create(77, 64, 64, false);
    foreach (var tile in engine.State.Tiles) { tile.Terrain = TerrainType.Grass; tile.Fertility = 80; }
    engine.State.NaturalDisasters = false;
    engine.SpawnResidents(16, 32, RaceKind.Human, 3);
    var home = engine.State.Settlements.Single();
    engine.SetNationResources(home.NationId, 1000, 1000, 1000, 1000);
    if (profession == Profession.Scholar)
    {
        engine.GrantFacility(home.Id, BuildingKind.Academy, home.X + 2, home.Y);
        engine.StartResearch(home.Id, ResearchKind.Agriculture);
    }
    if (profession == Profession.Mage) engine.GrantFacility(home.Id, BuildingKind.ArcaneSanctum, home.X + 2, home.Y);
    var id = engine.State.Residents[0].Id;
    engine.EditResident(id, new ResidentEdit { Profession = profession, X = home.X, Y = home.Y,
        Inventory = new ResourceStock { Food = 1 }, MagicTalent = 50 });
    engine.Tick();
    var target = engine.State.Society.Buildings.Single(building => building.Id == engine.GetResident(id)!.Agent.Goal.TargetEntityId);
    return (engine, id, target);
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
