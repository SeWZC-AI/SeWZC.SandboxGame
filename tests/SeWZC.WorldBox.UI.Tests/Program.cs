using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media.Imaging;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using SeWZC.WorldBox.Core;
using SeWZC.WorldBox.Desktop;
using SeWZC.WorldBox.UI;
using SeWZC.WorldBox.UI.Controls;
using SeWZC.WorldBox.UI.Platform;

AppBuilder.Configure<TestApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions()).SetupWithoutStarting();

var tests = new (string Name, Action Test)[]
{
    ("Imported separators are formatted without changing historical world data", ImportedSeparators),
    ("Context details prioritize useful facts and omit unrelated tabs", ContextDetails),
    ("Resident search remains attached and excludes archived people by default", ResidentSearch),
    ("Close terrain refresh scans visible chunks without modifying the world", VisibleTerrain),
    ("Terrain images ignore invisible resource changes and retain the forest stump threshold", TerrainResourceImages),
    ("Incremental terrain matches a fresh image across chunk borders and edits", IncrementalTerrain),
    ("Ecology reuses unchanged visible inputs and refreshes real edits and visibility", EcologyCache),
    ("Pixel rectangles preserve RGBA order and clip at canvas edges", PixelRectangles),
    ("Resident geometry includes only the layers drawn at the current zoom", ResidentGeometryLayers),
    ("Save capture suspends stepping and continuing map strokes cancel it", SaveCaptureBoundary),
    ("Autosave skips unchanged worlds and tracks edits simulation and failed writes", AutosaveChanges),
    ("Desktop saves compress exact chunks and preserve the last file on encoding failure", DesktopSave),
    ("Paged tools expose every building and keep previews outside toolbar layout", ToolPagination),
    ("Typed map tools preserve stroke and one-shot editing behavior", TypedMapTools),
    ("Building damage stays visible without expanding secondary details", BuildingDamage),
    ("Building details show concrete effects and blockers without boilerplate", BuildingDetailCopy),
    ("Tall buildings keep the plot behind clickable and roofs still select their footprint", BuildingOcclusion),
    ("Highlight controls stay disabled on reopening and guide omits simulation from inspection", HighlightControls),
    ("Building details control the actual facility and list real roads", BuildingControls),
    ("Town centers open town information and mobile expansion fills available height", TownInformationAndMobileHeight),
    ("Map resources and resident plans remain read-only during inspection", DetailedInspection),
    ("Map objects select quietly and details require the explicit view button", QuietSelection),
    ("Returning through resident details restores the original ground selection", InspectorReturnSelection),
    ("Inspector history is scoped to an open visit and retains recent locations", InspectorHistoryBoundaries),
    ("Cancelling editors restores the previous simulation state", ModalCancelState),
    ("Applying editors leaves a running world paused", ModalCommitState),
    ("Abandoned checkpoint captures cannot commit or overwrite a newer editing visit", DeferredModalSubmissions),
    ("Abandoned first map strokes cannot publish an undo point or retain a temporary pause", DeferredMapStrokes),
    ("Late exports preserve newer dialogs and queued exports never capture a replacement world", ExportWindowLifecycle),
    ("Settlement tabs and overview entries preserve the selected town", SettlementEntrypoints),
    ("Object links refresh names, targets and availability as the world changes", LiveObjectLinks),
    ("Returning between town research pages restores the selected branch and graph viewport", ResearchNavigationState),
    ("Construction, spell and railway map pickers pause and restore their source", ActionMapPickers),
    ("Map picking isolates its original context from tools and navigation", MapPickerContextIsolation),
    ("Research spells default to local adults while resident spells prefer that resident", SpellContext),
    ("Advanced research choices show separate prerequisites and commit only valid projects", AdvancedResearchUi), (
        "Advanced resource editors preserve untouched stocks and gifted factories expose requirements",
        AdvancedResourcesUi),
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
    ("Abandoned map picker cannot reopen an empty modal", MapPickerLifecycle),
};
var failures = 0;
var suiteClock = Stopwatch.StartNew();
foreach (var (name, test) in tests)
{
    var started = Stopwatch.GetTimestamp();
    try
    {
        test();
        Console.WriteLine($"PASS {name} ({Stopwatch.GetElapsedTime(started).TotalMilliseconds:F0} ms)");
    }
    catch (Exception error)
    {
        while (error is TargetInvocationException { InnerException: { } inner }) error = inner;
        failures++;
        Console.Error.WriteLine($"FAIL {name}: {error}");
    }
}

Console.WriteLine(
    $"{tests.Length - failures}/{tests.Length} UI checks passed in {suiteClock.Elapsed.TotalSeconds:F2} s");
return failures == 0 ? 0 : 1;

static void TownInformationAndMobileHeight()
{
    var engine = TwoTownWorld();
    var view = View(engine);
    var before = engine.ExportJson();

    void Layout()
    {
        view.Arrange(new Rect(0, 0, 390, 844));
        var body = Field<Grid>(view, "_body");
        body.Measure(new Size(390, 774));
        body.Arrange(new Rect(0, 0, 390, 774));
        Call(view, "ApplyLayout");
        body.Measure(new Size(390, 774));
        body.Arrange(new Rect(0, 0, 390, 774));
    }

    var center = engine.State.Society.Buildings.First(b => b.Kind == BuildingKind.TownCenter);
    Call(view, "OpenBuilding", center);
    Layout();
    Assert(Control<TextBlock>(view, "center-town-summary").Text!.Contains("城镇生效"), "Center omitted the town status");
    Click(view, "center-town-info");
    Layout();
    Assert(
        Field<int>(view, "_inspectorSettlementId") == center.SettlementId &&
        Control<TextBlock>(view, "town-expansion-summary").Text!.Contains("城镇等级"),
        "Center opened another town or omitted its details");
    var collapsed = Control<Border>(view, "inspector-panel").Bounds.Height;
    Click(view, "inspector-expand");
    Layout();
    var expanded = Control<Border>(view, "inspector-panel").Bounds.Height;
    Assert(expanded > 500 && expanded > collapsed * 1.6, $"Mobile panel only grew from {collapsed} to {expanded}");
    Click(view, "inspector-expand");
    Layout();
    Assert(Control<Border>(view, "inspector-panel").Bounds.Height < expanded,
        "Collapsing retained the expanded height");
    Assert(engine.ExportJson() == before, "Town navigation or panel expansion changed the world");
}

static void TypedMapTools()
{
    var engine = TwoTownWorld();
    var map = new WorldMapControl { Engine = engine, BrushRadius = 0 };
    map.Measure(new Size(800, 800));
    map.Arrange(new Rect(0, 0, 800, 800));
    map.FitWorld();
    var edits = 0;
    var starts = 0;
    map.WorldEditing += (_, _) => starts++;
    map.WorldEdited += (_, _) => edits++;
    map.ActiveTool = MapTool.ForTerrain(TerrainType.Sand);
    Call(map, "ApplyTool", map.GetTileScreenPosition(25, 25));
    Call(map, "ApplyTool", map.GetTileScreenPosition(28, 25));
    Assert(Enumerable.Range(25, 4).All(x => engine.State.Tiles[25 * engine.State.Width + x].Terrain == TerrainType.Sand)
           && edits == 2 && starts == 1, "Typed terrain tool lost continuous painting or its undo boundary");

    var town = engine.State.Settlements.First();
    var population = engine.State.Population;
    map.ActiveTool = MapTool.ForResidents(RaceKind.Human);
    map.SpawnCount = 1;
    Call(map, "ApplyTool", map.GetTileScreenPosition(town.X, town.Y));
    Call(map, "ApplyTool", map.GetTileScreenPosition(town.X + 1, town.Y));
    Assert(engine.State.Population == population + 1 && edits == 3 && starts == 2,
        "Dragging a typed spawn tool placed more than one group");

    map.ActiveTool = MapTool.ForResidents(RaceKind.Elf);
    Call(map, "ApplyTool", map.GetTileScreenPosition(town.X, town.Y));
    Assert(engine.State.Population == population + 2 && starts == 3,
        "Changing a typed tool retained the previous stroke");
    var before = engine.ExportJson();
    map.ActiveTool = MapTool.Pan;
    Call(map, "ApplyTool", map.GetTileScreenPosition(26, 26));
    Assert(engine.ExportJson() == before && edits == 4, "Navigation tool mutated the world");
    Assert(ReferenceEquals(MapTool.ForBuilding(BuildingKind.Bridge), MapTool.ForBuilding(BuildingKind.Bridge)),
        "A cached tool was reallocated on selection");
}

static void ToolPagination()
{
    var engine = TwoTownWorld();
    var view = View(engine);
    var map = Map(view);
    var before = engine.ExportJson();
    Call(view, "SetCategory", ToolCategory.Build);
    var found = new HashSet<string>();
    var pageCount = (Enum.GetValues<BuildingKind>().Length + 7) / 8;
    for (var page = 0; page < pageCount; page++)
    {
        foreach (var tool in Field<MapTool?[]>(view, "_slotTools"))
            if (tool is not null)
                found.Add(tool.Id);
        if (page < pageCount - 1) Click(view, "tool-page-next");
    }

    Assert(
        Enum.GetValues<BuildingKind>().Where(k => k != BuildingKind.TownCenter)
            .All(k => found.Contains("build:" + k)) && found.Contains("road:Road"), "Pagination hid a real tool");
    Call(view, "SetCategory", ToolCategory.Terrain);
    found.Clear();
    pageCount = (Enum.GetValues<TerrainType>().Length + 7) / 8;
    for (var page = 0; page < pageCount; page++)
    {
        foreach (var tool in Field<MapTool?[]>(view, "_slotTools"))
            if (tool is not null)
                found.Add(tool.Id);
        if (page < pageCount - 1) Click(view, "tool-page-next");
    }

    Assert(Enum.GetValues<TerrainType>().All(t => found.Contains(t.ToString())), "Pagination hid a terrain tool");
    Call(view, "SelectTool", MapTool.ForResidents(RaceKind.Human));
    Call(map, "PreviewPlacement", map.GetTileScreenPosition(12, 12), false);
    Assert(!Field<Border>(view, "_placementBar").IsVisible, "Mouse hover opened a shifting option bar");
    Preview(map, 12, 12);
    Assert(Field<Border>(view, "_placementBar").IsVisible, "Touch preview has no confirmation");
    Assert(Field<Border>(view, "_placementBar").Parent != Field<Border>(view, "_toolBar").Parent,
        "Preview participates in the toolbar stack");
    map.CancelPlacement();
    Assert(engine.ExportJson() == before, "Tool paging or preview changed the world");
}

static void BuildingControls()
{
    var engine = TwoTownWorld();
    var view = View(engine);
    var building = engine.State.Society.Buildings.First(b => b.Kind == BuildingKind.Workshop);
    Call(view, "OpenBuilding", building);
    Assert(Field<string>(view, "_inspectorMode") == "building", "Facility opened ground details");
    Assert(view.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text?.Contains("实际可采材料") == true),
        "Building lacks functional description");
    Click(view, "building-toggle");
    Assert(!building.Enabled, "Building control did not stop work");
    Assert(!engine.TryWorkAtBuilding(engine.State.Residents[0]), "Stopped workshop still performed work");
    Click(view, "building-toggle");
    Assert(building.Enabled, "Building did not resume");
    engine.BuildRoad(engine.State.Settlements[0].Id, 15, 12, 0);
    var saved = engine.ExportJson();
    Call(view, "OpenInspector", "structures", true);
    Assert(Map(view).Overlay == 4, "Structures page did not activate map colors");
    Assert(engine.ExportJson() == saved, "Activating infrastructure colors changed the simulation");
    Control<ComboBox>(view, "structures-kind").SelectedIndex = 1;
    Assert(view.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text?.Contains("道路 1 级") == true),
        "Road listing omitted actual road");
    var town = engine.State.Settlements.First(t => t.Id == building.SettlementId);
    town.Resources.Wood = town.Resources.Stone = 0;
    Call(view, "ShowBuildingUpgrade", building.Id, false);
    Assert(Control<TextBlock>(view, "upgrade-paid-status").Text!.Contains("缺"), "Upgrade omitted material shortage");
    Assert(Control<TextBlock>(view, "upgrade-gift-status").Text!.Contains("不扣施工材料"),
        "Gift inherited paid material requirements");
    Call(view, "CloseModal");
    building.Level = 3;
    Call(view, "ShowBuildingUpgrade", building.Id, false);
    Assert(!view.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text?.Contains("→ 4 级") == true),
        "Maximum-level upgrade promised level four");
    Assert(view.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text?.Contains("已达到最高等级") == true),
        "Upgrade omitted maximum level restriction");
    Assert(
        !view.GetLogicalDescendants().OfType<Button>().Any(b =>
            AutomationProperties.GetAutomationId(b) is "upgrade-apply" or "upgrade-gift"),
        "Maximum level retained unusable actions");
    Click(view, "upgrade-close");
}

static void BuildingOcclusion()
{
    var engine = TwoTownWorld();
    var centre = engine.State.Society.Buildings.First(b => b.Kind == BuildingKind.TownCenter);
    var rear = engine.State.Residents[0];
    var front = engine.State.Residents[1];
    rear.X = rear.FromX = front.X = front.FromX = centre.X;
    rear.Y = rear.FromY = centre.Y - 1;
    front.Y = front.FromY = centre.Y + 1;
    var view = View(engine);
    var map = Map(view);
    map.FocusTile(centre.X, centre.Y);
    for (var i = 0; i < 8; i++) map.ZoomIn();
    engine.State.Society.Buildings.RemoveAll(b => b.Id != centre.Id && b.X == centre.X && b.Y == centre.Y - 1);
    var before = engine.ExportJson();
    Call(map, "BuildScene", engine.State);
    var sprites =
        ((IEnumerable)typeof(WorldMapControl).GetField("_sceneSprites", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(map)!).Cast<object>().ToArray();
    var ids = sprites.Select(s => (int)s.GetType().GetProperty("Id")!.GetValue(s)!).ToArray();
    Assert(
        Array.IndexOf(ids, rear.Id) < Array.IndexOf(ids, centre.Id) &&
        Array.IndexOf(ids, front.Id) > Array.IndexOf(ids, centre.Id),
        "Building does not draw between people behind and ahead of it");
    var bounds =
        (Rect)typeof(WorldMapControl).GetMethod("BuildingBounds", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [centre])!;
    Assert(bounds.Height > 8, "Fixture does not contain a building taller than one tile");
    var foot = map.GetTileScreenPosition(centre.X, centre.Y);
    var size = map.GetTileScreenPosition(centre.X + 1, centre.Y).X - foot.X;
    Call(map, "SelectObjectAt", new Point(foot.X, foot.Y - size * 1.1));
    Assert(map.SelectedBuildingId == centre.Id, "Clicking a high roof selected the ground behind the building");
    Assert(Field<(int X, int Y)?>(map, "_selection") == (centre.X, centre.Y),
        "Roof selection highlights the wrong footprint");
    var rearBuildingId = engine.GrantFacility(centre.SettlementId, BuildingKind.Housing, centre.X, centre.Y - 1);
    map.RefreshWorld();
    Call(map, "SelectObjectAt", map.GetTileScreenPosition(centre.X, centre.Y - 1));
    Assert(map.SelectedBuildingId == rearBuildingId, "Foreground roof blocked the building on the pointer's plot");
    before = engine.ExportJson();
    Assert(engine.ExportJson() == before, "Depth ordering or selecting a roof changed the world");
}

static void HighlightControls()
{
    var engine = TwoTownWorld();
    var view = View(engine);
    var map = Map(view);
    var before = engine.ExportJson();
    Call(view, "OpenInspector", "structures", true);
    Assert(map.Overlay == 4, "First opening did not enable construction highlights");
    Click(view, "map-highlights-off");
    Call(view, "OpenInspector", "overview", true);
    Call(view, "OpenInspector", "structures", true);
    Assert(map.Overlay == 0, "Reopening forced disabled highlights on");
    Assert(Control<ComboBox>(view, "map-overlay").ItemCount >= 10, "Additional highlight types are missing");
    Call(view, "OpenInspector", "guide", true);
    Assert(Control<TextBlock>(view, "inspector-title").Text == "玩法说明", "Game guide retains the world overview title");
    Assert(view.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text?.Contains("未使用额度不会累计") == true),
        "Water rule has no guide entry");
    Assert(engine.ExportJson() == before, "Highlights or guide changed the simulation");
}

static void BuildingDamage()
{
    var engine = TwoTownWorld();
    var view = View(engine);
    var building = engine.State.Society.Buildings.First(b => b.Kind == BuildingKind.Workshop);
    building.Health = 23;
    Call(view, "OpenBuilding", building);
    var health = Control<TextBlock>(view, "building-health");
    var status = Control<TextBlock>(view, "building-status");
    Assert(
        health.Text!.Contains("23") && status.Text!.Contains("低于 50") &&
        !status.GetLogicalAncestors().OfType<Expander>().Any(),
        "Damage magnitude and operational threshold are hidden");
    Click(view, "building-repair");
    Call(view, "RefreshInspector", false);
    Assert(health.Text!.Contains("100 / 100"), "Repair did not refresh the visible health");
    var button = Control<Button>(view, "building-repair");
    Assert(
        button.HorizontalContentAlignment == HorizontalAlignment.Center &&
        button.VerticalContentAlignment == VerticalAlignment.Center, "Action label is not centred");
    Assert(button.Margin.Left >= 2 && button.Margin.Right >= 2, "Adjacent wrap buttons touch each other");
}

static void BuildingDetailCopy()
{
    var engine = TwoTownWorld();
    var b = engine.State.Society.Buildings.First(b => b.Kind == BuildingKind.Workshop);
    b.Kind = BuildingKind.Watchtower;
    b.Level = 1;
    b.Health = 100;
    var before = engine.ExportJson();
    var view = View(engine);
    Call(view, "OpenBuilding", b);
    var effects = Control<TextBlock>(view, "building-effects").Text!;
    Assert(effects.Contains("同聚落") && effects.Contains("3 至 4 格") && effects.Contains("无需工作人员"),
        "Watchtower effect lost scope or numerical benefit");
    Assert(!effects.Contains("来源") && !effects.Contains("健康") && !effects.Contains("耐火"),
        "Primary effects repeat obvious sources or maintenance facts");
    Assert(Control<TextBlock>(view, "building-next-level").Text!.Contains("4 至 5 格"),
        "Next level omitted the actual change");
    Assert(!Control<TextBlock>(view, "building-next-level").Text!.Contains("未生效"),
        "Preview is incorrectly reported as a blocked effect");
    Assert(!Control<TextBlock>(view, "building-status").IsVisible,
        "A functioning passive building displays a redundant status");
    Assert(engine.ExportJson() == before, "Inspection changed the world");
    b.Enabled = false;
    Call(view, "RefreshInspector", false);
    Assert(Control<TextBlock>(view, "building-status").Text!.Contains("恢复运营"),
        "Disabled building omitted the corrective action");
    b.Enabled = true;
    b.Health = 23;
    Call(view, "RefreshInspector", false);
    Assert(Control<TextBlock>(view, "building-status").Text!.Contains("至少 50"),
        "Damage omitted the operational threshold");
    b.Health = 100;
    engine.State.Tiles[b.Y * engine.State.Width + b.X].FireTicks = 9;
    Assert(engine.GetBuildingDetailStatus(b.Id).Contains("剩余 9 日"), "Fire blocker omitted remaining time");
    engine.State.Tiles[b.Y * engine.State.Width + b.X].FireTicks = 0;
    b.Kind = BuildingKind.SignalTower;
    Assert(
        engine.GetBuildingDetailStatus(b.Id).Contains("电气化") && engine.GetBuildingDetailStatus(b.Id).Contains("信号网络"),
        "Signal tower omitted specific missing research");
    foreach (var kind in Enum.GetValues<BuildingKind>())
    {
        b.Kind = kind;
        var text = string.Join("\n", engine.GetBuildingEffects(b.Id));
        Assert(!text.Contains("需要完工、健康与运营条件") && !text.Contains("来源：升级完工后"), "Generic conditions survived for " + kind);
        if (kind == BuildingKind.Well)
        {
            Assert(text.Contains("供水量") && !text.Contains("今日剩余") && !text.Contains("每日可取水"),
                "Well inspection retained a separate water quota field");
        }

        if (WorldEngine.BuildingRace(kind) is not null && kind != BuildingKind.DwarvenForge)
            Assert(!text.Contains("每批加工产出"), "Non-manufacturing racial facility describes a fictitious product");
    }

    engine.State.Tiles[b.Y * engine.State.Width + b.X].ClaimedSettlementId = 0;
    Assert(engine.GetBuildingDetailStatus(b.Id).Contains("占领区域"), "Lost town ground has no concrete operating blocker");
}

static void DetailedInspection()
{
    var engine = TwoTownWorld();
    var view = View(engine);
    var person = engine.State.Residents[0];
    var before = engine.ExportJson();
    Call(view, "OpenResident", person.Id);
    var text = string.Join("\n", view.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text));
    Assert(
        text.Contains("体力") && text.Contains("预期寿命") && text.Contains("当前任务：") && text.Contains("当前劳作：") &&
        text.Contains("后续："), "Resident omits current status and future actions");
    Call(view, "OpenInspector", "overview", true);
    Control<ComboBox>(view, "map-resources").SelectedIndex = 1;
    Assert(Map(view).ResourceVisibility == ResourceVisibility.All && engine.ExportJson() == before,
        "Resource visibility edits simulation knowledge");
}

static void QuietSelection()
{
    var engine = TwoTownWorld();
    var view = View(engine);
    var resident = engine.State.Residents[0];
    resident.Health = 61.5;
    var building = engine.State.Society.Buildings[0];
    building.Health = 86;
    var before = engine.ExportJson();
    Call(view, "ToggleTools");
    Call(view, "SelectMapObject", "resident", resident.Id, 0, 0);
    Assert(!Field<bool>(view, "_mobilePanel") && !Field<Border>(view, "_modal").IsVisible, "Selection opened a panel");
    Assert(Control<Border>(view, "selection-summary").IsVisible, "Selection has no summary");
    Assert(Field<TextBlock>(view, "_selectionText").Text!.Contains($"{resident.Name}（61.5/100）"),
        "Resident preview omitted current/maximum health");
    Assert(!Field<bool>(view, "_toolsOpen"), "Open tools hid the new map selection summary");
    Click(view, "selection-view");
    Assert(Field<bool>(view, "_mobilePanel") && Field<string>(view, "_inspectorMode") == "resident",
        "Explicit view did not open resident details");
    Call(view, "SelectMapObject", "building", building.Id, building.X, building.Y);
    Assert(!Field<bool>(view, "_mobilePanel"), "Building selection opened details");
    Assert(Field<TextBlock>(view, "_selectionText").Text!.Contains("（86/100）"),
        "Building preview omitted current/maximum health");
    Click(view, "selection-view");
    Assert(Field<string>(view, "_inspectorMode") == "building", "Building details did not open");
    Call(view, "SelectMapObject", "tile", 0, 1, 1);
    Assert(!Field<bool>(view, "_mobilePanel") && engine.ExportJson() == before,
        "Ground selection changed the world or opened details");
    Click(view, "selection-clear");
    Assert(!Control<Border>(view, "selection-summary").IsVisible, "Selection did not clear");
}

static void InspectorReturnSelection()
{
    var engine = TwoTownWorld();
    var view = View(engine);
    var map = Map(view);
    var before = engine.ExportJson();
    Call(map, "SelectTile", map.GetTileScreenPosition(2, 2));
    Click(view, "selection-view");
    Call(view, "OpenResident", engine.State.Residents[0].Id);
    Assert(map.SelectedResidentId is not null, "Resident detail did not select its resident");
    Click(view, "inspector-back");
    Assert(Field<string>(view, "_inspectorMode") == "tile" && Field<string?>(view, "_mapSelectionKind") == "tile",
        "Returning restored the tile data but not its selection kind");
    Assert(Field<(int X, int Y)?>(map, "_selection") == (2, 2) && map.SelectedResidentId is null,
        "Returning left the resident selected on the map");
    Click(view, "inspector-close");
    Assert(
        Control<Border>(view, "selection-summary").IsVisible && Field<(int X, int Y)?>(view, "_selectedTile") == (2, 2),
        "Closing returned tile details lost its summary");
    var building = engine.State.Society.Buildings[0];
    Call(view, "OpenBuilding", building);
    Call(view, "OpenResident", engine.State.Residents[0].Id);
    Click(view, "inspector-back");
    Assert(
        Field<string?>(view, "_mapSelectionKind") == "building" && map.SelectedBuildingId == building.Id &&
        map.SelectedResidentId is null,
        "Returning to a building left another object highlighted");
    Click(view, "inspector-close");
    var resident = engine.State.Residents[0];
    Call(view, "OpenResident", resident.Id);
    map.FollowSelectedResident = true;
    SetField(view, "_expandedInspector", true);
    Call(view, "OpenNation", resident.NationId);
    Click(view, "inspector-back");
    Assert(
        map.SelectedResidentId == resident.Id && map.FollowSelectedResident && Field<bool>(view, "_expandedInspector"),
        "Returning to a resident discarded following or inspector expansion");
    Assert(engine.ExportJson() == before, "Returning through details changed the world");
}

static void InspectorHistoryBoundaries()
{
    var engine = TwoTownWorld();
    var view = View(engine);
    var map = Map(view);
    var before = engine.ExportJson();
    Call(view, "OpenInspector", "overview", true);
    Click(view, "inspector-residents");
    Click(view, "inspector-close");
    Call(map, "SelectTile", map.GetTileScreenPosition(2, 2));
    Click(view, "selection-view");
    Click(view, "inspector-back");
    Assert(!Field<bool>(view, "_mobilePanel"), "A new map visit returned to a previously closed inspector");
    Call(view, "OpenInspector", "overview", true);
    Click(view, "inspector-overview");
    Click(view, "inspector-back");
    Assert(!Field<bool>(view, "_mobilePanel"), "Clicking the current global tab created a duplicate history entry");
    Call(view, "OpenInspector", "overview", true);
    for (var i = 0; i < 33; i++) Click(view, i % 2 == 0 ? "inspector-nations" : "inspector-overview");
    Click(view, "inspector-back");
    Assert(Field<bool>(view, "_mobilePanel") && Field<string>(view, "_inspectorMode") == "overview",
        "Reaching the history limit discarded the most recent page");
    var returned = 1;
    while (Field<bool>(view, "_mobilePanel") && returned <= 33)
    {
        Click(view, "inspector-back");
        returned++;
    }

    Assert(returned == 33 && !Field<bool>(view, "_mobilePanel"),
        "History did not retain exactly the latest 32 locations");
    Assert(engine.ExportJson() == before, "History navigation changed the world");
}

static void ModalCancelState()
{
    var engine = TwoTownWorld();
    var view = View(engine);
    var resident = engine.State.Residents[0];
    var before = engine.ExportJson();
    var editors = new (string Method, object?[] Args)[]
    {
        ("ShowNationEditor", [resident.NationId]), ("ShowResidentEditor", [resident.Id]),
        ("ShowCultureEditor", [resident.CultureId]), ("ShowRules", []),
        ("ShowSpellSelectionEditor", [SpellKind.Heal, (int?)resident.SettlementId]),
    };
    foreach (var paused in new[] { false, true })
    foreach (var editor in editors)
    {
        SetField(view, "_paused", paused);
        SetField(view, "_ready", true);
        Call(view, editor.Method, editor.Args);
        Assert(Field<Border>(view, "_modal").IsVisible && Map(view).IsSimulationPaused,
            editor.Method + " did not temporarily stop simulation");
        SetField(view, "_accumulator", .4);
        var tick = engine.State.Tick;
        Call(view, "OnTick", null, EventArgs.Empty);
        Assert(engine.State.Tick == tick, editor.Method + " advanced simulation while its form was visible");
        Click(view, "modal-cancel");
        Assert(Field<bool>(view, "_paused") == paused && Map(view).IsSimulationPaused == paused,
            editor.Method + " cancellation changed the previous pause state");
    }

    Assert(engine.ExportJson() == before, "Opening or cancelling editors changed the world");
}

static void ModalCommitState()
{
    var engine = TwoTownWorld();
    var view = View(engine);
    var resident = engine.State.Residents[0];
    var editors = new (string Method, object?[] Args, string Apply)[]
    {
        ("ShowNationEditor", [resident.NationId], "nation-apply"),
        ("ShowResidentEditor", [resident.Id], "resident-apply"),
        ("ShowCultureEditor", [resident.CultureId], "culture-apply"), ("ShowRules", [], "world-rules-apply"),
    };
    foreach (var editor in editors)
    {
        SetField(view, "_paused", false);
        // 继续编辑时复用已有恢复点，检查提交和关闭流程。
        SetField(view, "_checkpoint", engine.ExportJson());
        Call(view, editor.Method, editor.Args);
        Click(view, editor.Apply);
        Assert(!Field<Border>(view, "_modal").IsVisible && Field<bool>(view, "_paused") && Map(view).IsSimulationPaused,
            editor.Method + " resumed simulation after applying changes");
    }
}

static void ExportWindowLifecycle()
{
    var previousStorage = App.Storage;
    var storage = new DeferredExportStorage();
    try
    {
        App.Storage = storage;
        foreach (var replaceWorld in new[] { false, true })
        {
            storage = new DeferredExportStorage();
            App.Storage = storage;
            var engine = EmptyWorld(42, 32);
            var view = View(engine);
            var before = engine.ExportJson();
            SetField(view, "_paused", false);
            Call(view, "ShowStorage");
            var export = (Task)Call(view, "ExportWorldAsync")!;
            Await(() => storage.ExportCalls == 1, "The original world export never reached storage");
            Assert(storage.ExportedJson == before, "Export did not capture its original world");
            if (replaceWorld)
            {
                engine = EmptyWorld(43, 32);
                Call(view, "ReplaceWorld", engine);
            }

            Call(view, "ShowRules");
            var currentWindow = Field<Border>(view, "_modal").Child;
            var paused = Field<bool>(view, "_paused");
            storage.CompleteExport();
            Await(() => export.IsCompleted, "The delayed export did not finish");
            export.GetAwaiter().GetResult();
            Assert(
                Field<Border>(view, "_modal").IsVisible &&
                ReferenceEquals(currentWindow, Field<Border>(view, "_modal").Child),
                "An old export closed the newer rules window");
            Assert(
                ReferenceEquals(engine, Field<WorldEngine>(view, "_engine")) && Field<bool>(view, "_paused") == paused,
                "Completing an old export changed the current world or pause state");
        }

        storage = new DeferredExportStorage();
        App.Storage = storage;
        var queuedView = View(EmptyWorld(42, 32));
        Call(queuedView, "ShowStorage");
        var gate = Field<SemaphoreSlim>(queuedView, "_saveGate");
        gate.Wait();
        Task queued;
        try
        {
            queued = (Task)Call(queuedView, "ExportWorldAsync")!;
            Call(queuedView, "ReplaceWorld", EmptyWorld(43, 32));
            Call(queuedView, "ShowStorage");
        }
        finally
        {
            gate.Release();
        }

        Await(() => queued.IsCompleted, "The abandoned queued export did not finish");
        queued.GetAwaiter().GetResult();
        Assert(storage.ExportCalls == 0 && Field<Border>(queuedView, "_modal").IsVisible,
            "A queued old export captured the replacement world or closed its storage window");
    }
    finally
    {
        storage.CompleteExport();
        App.Storage = previousStorage;
    }

    static void Await(Func<bool> completed, string message)
    {
        var clock = Stopwatch.StartNew();
        while (!completed() && clock.Elapsed.TotalSeconds < 2)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(1);
        }

        Assert(completed(), message);
    }
}

static void DeferredModalSubmissions()
{
    foreach (var editor in new[] { "nation", "culture" })
    foreach (var abandon in new[] { "cancel", "window", "world" })
    {
        var engine = TwoTownWorld();
        var view = View(engine);
        var resident = engine.State.Residents[0];
        Call(view, "TogglePause");
        var before = engine.ExportJson();
        Call(view, editor == "nation" ? "ShowNationEditor" : "ShowCultureEditor",
            editor == "nation" ? resident.NationId : resident.CultureId);
        Control<TextBox>(view, editor + "-name").Text = "不能提交的旧窗口";
        var gate = Field<SemaphoreSlim>(view, "_saveGate");
        gate.Wait();
        Task pending;
        var current = engine;
        var currentBefore = before;
        Control? newerWindow = null;
        string? checkpointAfterAbandon = null;
        try
        {
            Click(view, editor + "-apply");
            pending = Field<Task?>(view, "_prepareEditTask") ??
                      throw new Exception("Submit did not wait for its initial checkpoint capture");
            Assert(!pending.IsCompleted && engine.ExportJson() == before,
                "A blocked initial submit already changed the world");
            if (editor == "nation")
            {
                Assert(!Control<NumericUpDown>(view, "nation-food").IsEffectivelyEnabled &&
                       !Control<ComboBox>(view, "nation-technology").IsEffectivelyEnabled
                       && Control<Button>(view, "modal-cancel").IsEffectivelyEnabled,
                    "Pending submit left draft controls editable or blocked cancellation");
            }

            if (abandon == "cancel") Click(view, "modal-cancel");
            else if (abandon == "window")
            {
                Call(view, "ShowRules");
                newerWindow = Field<Border>(view, "_modal").Child;
            }
            else
            {
                current = EmptyWorld(43, 32);
                Call(view, "ReplaceWorld", current);
                currentBefore = current.ExportJson();
                Call(view, "ShowRules");
                newerWindow = Field<Border>(view, "_modal").Child;
            }

            checkpointAfterAbandon = Field<string?>(view, "_checkpoint");
        }
        finally
        {
            gate.Release();
        }

        AwaitUi(() => pending.IsCompleted && Field<Task?>(view, "_prepareEditTask") is null,
            "An abandoned submission did not finish cancellation");
        Assert(
            engine.ExportJson() == before && current.ExportJson() == currentBefore &&
            ReferenceEquals(current, Field<WorldEngine>(view, "_engine")),
            "An abandoned form committed its command or changed the replacement world");
        Assert(Field<string?>(view, "_checkpoint") == checkpointAfterAbandon,
            "An old checkpoint capture overwrote the current editing visit");
        Assert(Field<bool>(view, "_paused") == (abandon == "world"),
            "An abandoned submission changed the original running preference");
        Assert(abandon == "cancel"
                ? !Field<Border>(view, "_modal").IsVisible
                : Field<Border>(view, "_modal").IsVisible &&
                  ReferenceEquals(newerWindow, Field<Border>(view, "_modal").Child),
            "An old submission reopened its form or closed the newer window");
        if (abandon == "cancel")
            Assert(!Map(view).IsSimulationPaused, "Cancelled checkpoint capture retained its temporary pause");
    }

    {
        var engine = TwoTownWorld();
        var view = View(engine);
        var nation = engine.State.Nations[0];
        var originalName = nation.Name;
        Call(view, "TogglePause");
        var before = engine.ExportJson();
        Call(view, "ShowNationEditor", nation.Id);
        Control<TextBox>(view, "nation-name").Text = "被替换的提交";
        var gate = Field<SemaphoreSlim>(view, "_saveGate");
        gate.Wait();
        Task oldPending;
        Task newerPending;
        try
        {
            Click(view, "nation-apply");
            oldPending = Field<Task?>(view, "_prepareEditTask") ??
                         throw new Exception("Original submit did not wait for its checkpoint");
            Call(view, "ShowRules");
            Control<NumericUpDown>(view, "rule-gathering-rate").Value = .5m;
            Click(view, "world-rules-apply");
            newerPending = Field<Task?>(view, "_prepareEditTask") ??
                           throw new Exception("Replacement submit did not wait for its checkpoint");
            AwaitUi(() => oldPending.IsCompleted, "The replaced submission did not finish cancellation");
            Assert(!newerPending.IsCompleted && ReferenceEquals(newerPending, Field<Task?>(view, "_prepareEditTask"))
                                             && !Control<NumericUpDown>(view, "rule-gathering-rate")
                                                 .IsEffectivelyEnabled && Control<Button>(view, "modal-cancel")
                                                 .IsEffectivelyEnabled,
                "An old submission's cleanup enabled the newer pending form or released its capture boundary");
        }
        finally
        {
            gate.Release();
        }

        AwaitUi(
            () => newerPending.IsCompleted && Field<Task?>(view, "_prepareEditTask") is null &&
                  !Field<Border>(view, "_modal").IsVisible,
            "The replacement pending rules submit did not finish");
        Assert(
            nation.Name == originalName && engine.State.Rules.GatheringRate == .5 &&
            Field<string?>(view, "_checkpoint") == before,
            "Overlapping editing visits committed the old draft or lost the replacement's original checkpoint");
    }
    {
        var engine = TwoTownWorld();
        var view = View(engine);
        var nation = engine.State.Nations[0];
        Call(view, "TogglePause");
        var before = engine.ExportJson();
        Call(view, "ShowNationEditor", nation.Id);
        const string marker = "唯一的提交";
        Control<TextBox>(view, "nation-name").Text = marker;
        var gate = Field<SemaphoreSlim>(view, "_saveGate");
        gate.Wait();
        Task pending;
        try
        {
            Click(view, "nation-apply");
            pending = Field<Task?>(view, "_prepareEditTask") ??
                      throw new Exception("Submit did not wait for its checkpoint");
            Click(view, "nation-apply");
        }
        finally
        {
            gate.Release();
        }

        AwaitUi(
            () => pending.IsCompleted && Field<Task?>(view, "_prepareEditTask") is null &&
                  !Field<Border>(view, "_modal").IsVisible,
            "A valid deferred submission did not complete");
        Assert(nation.Name == marker && engine.State.Events.Count(e => e.Message.Contains("更名为" + marker + "。")) == 1,
            "Repeated submit applied the same pending command more than once");
        Assert(
            Field<string?>(view, "_checkpoint") == before && Field<bool>(view, "_paused") &&
            Map(view).IsSimulationPaused,
            "A committed deferred submission lost its original checkpoint or resumed simulation");
    }
    foreach (var leaveResearch in new[] { false, true })
    {
        var engine = TwoTownWorld();
        var town = engine.State.Settlements[0];
        engine.SetNationResources(town.NationId, 1000, 1000, 1000, 1000, 100, 100, 100);
        engine.GrantFacility(town.Id, BuildingKind.Academy, town.X + 2, town.Y + 2);
        var view = View(engine);
        Call(view, "TogglePause");
        Call(view, "OpenSettlement", town.Id, "research");
        Click(view, "research-node-Agriculture");
        var before = engine.ExportJson();
        var gate = Field<SemaphoreSlim>(view, "_saveGate");
        gate.Wait();
        Task pending;
        try
        {
            Click(view, "research-start");
            pending = Field<Task?>(view, "_prepareEditTask") ??
                      throw new Exception("Research submit did not wait for its checkpoint");
            Click(view, "research-node-Logistics");
            if (leaveResearch) Click(view, "settlement-tab-communication");
        }
        finally
        {
            gate.Release();
        }

        AwaitUi(() => pending.IsCompleted && Field<Task?>(view, "_prepareEditTask") is null,
            "Deferred research did not finish its capture boundary");
        var project = engine.State.Society.Research.Single(r => r.SettlementId == town.Id).ActiveProject;
        Advancement? expected = leaveResearch ? null : Advancement.Agriculture;
        Assert(project == expected,
            leaveResearch
                ? "Leaving the research page committed its abandoned project"
                : "Waiting for a checkpoint changed the clicked research to a later selection");
        if (leaveResearch)
        {
            Assert(engine.ExportJson() == before && !Field<bool>(view, "_paused"),
                "Leaving pending research changed the world or its running preference");
        }
    }

    {
        var engine = TwoTownWorld();
        var view = View(engine);
        var nation = engine.State.Nations[0];
        Call(view, "TogglePause");
        var before = engine.ExportJson();
        var rejected = (Task<bool>)Call(view, "SubmitEditAsync",
            (Action)(() => throw new InvalidOperationException("无修改的失败")), false)!;
        AwaitUi(() => rejected.IsCompleted, "An unchanged failed submission did not finish");
        Assert(
            !rejected.GetAwaiter().GetResult() && engine.ExportJson() == before &&
            Field<string?>(view, "_checkpoint") is null && !Field<bool>(view, "_paused"),
            "A failed command that changed nothing published an undo point or retained pause");
        var partial = (Task<bool>)Call(view, "SubmitEditAsync", (Action)(() =>
        {
            engine.RenameNation(nation.Id, "需撤销的部分修改");
            throw new InvalidOperationException("修改后的失败");
        }), false)!;
        AwaitUi(() => partial.IsCompleted, "A partially changed failed submission did not finish");
        Assert(!partial.GetAwaiter().GetResult() && engine.ExportJson() != before &&
               Field<string?>(view, "_checkpoint") == before
               && Field<bool>(view, "_paused") && Control<Button>(view, "world-undo").IsEnabled,
            "A partially changed failed command discarded its undo point or resumed simulation");
        Click(view, "world-undo");
        Assert(Field<WorldEngine>(view, "_engine").ExportJson() == before,
            "Undo did not restore a partially failed command's original world");
    }
}

static void DeferredMapStrokes()
{
    foreach (var abandon in new[] { "pan", "modal", "picker" })
    {
        var engine = TwoTownWorld();
        engine.PaintTerrain(2, 2, TerrainType.Mountain, 0);
        var view = View(engine);
        var map = Map(view);
        Call(view, "TogglePause");
        var before = engine.ExportJson();
        map.ActiveTool = MapTool.ForTerrain(TerrainType.Grass);
        var gate = Field<SemaphoreSlim>(view, "_saveGate");
        gate.Wait();
        Task pending;
        Control? newerWindow = null;
        try
        {
            Call(map, "ApplyTool", map.GetTileScreenPosition(2, 2));
            pending = Field<Task?>(view, "_prepareEditTask") ??
                      throw new Exception("First map stroke did not wait for its checkpoint");
            Assert(!pending.IsCompleted && engine.ExportJson() == before,
                "A blocked map stroke changed the world before capturing its checkpoint");
            if (abandon == "pan") Call(view, "SuspendTool");
            else if (abandon == "modal") Call(view, "ShowRules");
            else
            {
                Call(view, "ShowGoalEditor", engine.State.Residents[0].Id);
                Click(view, "map-pick-resident_goal_x");
            }

            newerWindow = Field<Border>(view, "_modal").Child;
        }
        finally
        {
            gate.Release();
        }

        AwaitUi(
            () => pending.IsCompleted && Field<Task?>(view, "_prepareEditTask") is null &&
                  !Field<bool>(map, "_preparingWorldEdit"),
            "An abandoned first map stroke did not exit its capture boundary");
        Assert(
            engine.ExportJson() == before && Field<string?>(view, "_checkpoint") is null &&
            !Field<bool>(view, "_paused"),
            "An abandoned map stroke changed terrain, published a checkpoint or paused a running world permanently");
        if (abandon == "pan")
        {
            Assert(!Field<Border>(view, "_modal").IsVisible && !map.IsSimulationPaused,
                "Switching to pan retained the abandoned stroke's temporary pause");
        }
        else
        {
            Assert(ReferenceEquals(newerWindow, Field<Border>(view, "_modal").Child),
                "An old map stroke replaced the newer window");
            if (abandon == "picker")
            {
                Assert(map.PickingLocation && !Field<Border>(view, "_modal").IsVisible,
                    "An old stroke exited or reopened the active map picker");
                Call(view, "FinishMapPick", null, null);
            }

            Assert(Field<Border>(view, "_modal").IsVisible, "An old stroke closed the newer form");
            Click(view, "modal-cancel");
            Assert(!Field<bool>(view, "_paused") && !map.IsSimulationPaused,
                "Cancelling the newer form failed to resume its original running world");
        }
    }
}

static void SettlementEntrypoints()
{
    var engine = TwoTownWorld();
    var view = View(engine);
    var town = engine.State.Settlements[1];
    town.Tier = SettlementTier.City;
    var before = engine.ExportJson();
    Call(view, "OpenInspector", "overview", true);
    Click(view, "overview-settlements");
    Assert((Control<Button>(view, $"settlement-row-{town.Id}").Content as TextBlock)?.Text?.Contains("城镇等级 城") == true,
        "Settlement list showed a stale numeric level instead of its actual city tier");
    Click(view, $"settlement-row-{town.Id}");
    foreach (var mode in new[] { "settlement", "infrastructure", "research", "communication", "settlement" })
    {
        Click(view, "settlement-tab-" + mode);
        Assert(Field<string>(view, "_inspectorMode") == mode && Field<int>(view, "_inspectorSettlementId") == town.Id,
            "Changing the settlement tab lost the selected town");
        var navigation = Field<StackPanel>(view, "_inspectorNavigation");
        Assert(new[] { "settlement", "infrastructure", "research", "communication" }.All(tab =>
                Control<Button>(view, "settlement-tab-" + tab).GetLogicalAncestors().Contains(navigation)),
            "Settlement tabs are buried in the scrolling body");
    }

    foreach (var mode in new[] { "research", "communication" })
    {
        Call(view, "OpenInspector", "overview", true);
        Click(view, "overview-" + mode);
        Assert(Field<string>(view, "_inspectorMode") == mode && Field<int>(view, "_inspectorSettlementId") == town.Id,
            "Overview's " + mode + " entry discarded the current town");
    }

    Assert(engine.ExportJson() == before, "Settlement entries or tabs changed the world");
}

static void ActionMapPickers()
{
    var engine = TwoTownWorld();
    var view = View(engine);
    var town = engine.State.Settlements[0];
    var before = engine.ExportJson();
    var forms = new (string Method, object?[] Args, string Prefix, string Mode)[]
    {
        ("ShowBuildingEditor", [town.Id], "building", "infrastructure"),
        ("ShowSpellSelectionEditor", [SpellKind.Heal, (int?)town.Id], "spell", "research"),
        ("ShowRailEditor", [town.Id], "rail", "research"),
    };
    foreach (var form in forms)
    {
        Call(view, "OpenSettlement", town.Id, form.Mode);
        SetField(view, "_paused", false);
        SetField(view, "_ready", true);
        Map(view).SelectResident(engine.State.Residents[0].Id, true);
        var selection = Map(view).CaptureMapSelection();
        Call(view, form.Method, form.Args);
        var coordinate = Control<NumericUpDown>(view, form.Prefix + "-x");
        Click(view, "map-pick-" + form.Prefix + "_x");
        Assert(Map(view).PickingLocation && !Field<Border>(view, "_modal").IsVisible && Map(view).IsSimulationPaused,
            form.Prefix + " picker did not enter a paused map view");
        SetField(view, "_accumulator", .4);
        var tick = engine.State.Tick;
        Call(view, "OnTick", null, EventArgs.Empty);
        Assert(engine.State.Tick == tick, form.Prefix + " picker advanced simulation while its form was hidden");
        Call(Map(view), "SelectTile", Map(view).GetTileScreenPosition(20, 21));
        Assert(!Map(view).PickingLocation && Field<Border>(view, "_modal").IsVisible &&
               Field<bool>(view, "_mobilePanel")
               && Field<string>(view, "_inspectorMode") == form.Mode &&
               Field<int>(view, "_inspectorSettlementId") == town.Id,
            form.Prefix + " picking failed to restore its original inspector");
        Assert(ReferenceEquals(coordinate, Control<NumericUpDown>(view, form.Prefix + "-x")) && coordinate.Value == 20
            && Control<NumericUpDown>(view, form.Prefix + "-y").Value == 21,
            form.Prefix + " picking replaced the form or lost coordinates");
        Assert(Map(view).CaptureMapSelection() == selection,
            form.Prefix + " picking lost the original map selection or follow state");
        Click(view, "modal-cancel");
        Assert(!Field<bool>(view, "_paused") && !Map(view).IsSimulationPaused,
            form.Prefix + " picker cancellation retained its temporary pause");
    }

    Assert(engine.ExportJson() == before, "Action map picking changed the world");
}

static void LiveObjectLinks()
{
    var engine = TwoTownWorld();
    var home = engine.State.Settlements[0];
    var remote = engine.State.Settlements[1];
    var resident = engine.State.Residents[0];
    var nation = engine.State.Nations.First(n => n.Id == home.NationId);
    engine.PaintTerrain(1, 30, TerrainType.Grass, 3);
    engine.SpawnResidents(1, 30, RaceKind.Human, 1);
    var nextCapital = engine.State.Settlements.Single(t => t.Id != home.Id && t.Id != remote.Id);
    var representative = engine.State.Residents.Single(r => r.SettlementId == nextCapital.Id);
    engine.TransferTerritory(nextCapital.X, nextCapital.Y, nation.Id, 0);
    var otherNationId = engine.SplitSettlement(remote.Id, "另一国家");
    var otherNation = engine.State.Nations.First(n => n.Id == otherNationId);
    nation.CapitalId = home.Id;
    nation.RepresentativeId = resident.Id;
    home.RepresentativeId = 0;
    var view = View(engine);
    Call(view, "OpenNation", nation.Id);
    var capitalLink = Control<Button>(view, "nation-capital");
    var representativeLink = Control<Button>(view, "nation-representative");
    nation.CapitalId = nextCapital.Id;
    nation.RepresentativeId = representative.Id;
    nextCapital.Name = "迁移后的首都";
    representative.Name = "新任代表";
    Call(view, "RefreshInspector", false);
    Assert(ReferenceEquals(capitalLink, Control<Button>(view, "nation-capital")) && capitalLink.IsEnabled &&
           ButtonText(capitalLink).Contains(nextCapital.Name)
           && ReferenceEquals(representativeLink, Control<Button>(view, "nation-representative")) &&
           representativeLink.IsEnabled && ButtonText(representativeLink).Contains(representative.Name),
        "National links retained the previous capital or representative label");
    Click(view, "nation-capital");
    Assert(Field<int>(view, "_inspectorSettlementId") == nextCapital.Id, "Capital link opened its old target");
    Click(view, "inspector-back");
    Click(view, "nation-representative");
    Assert(Field<int>(view, "_selectedResidentId") == representative.Id, "Representative link opened its old target");
    Click(view, "inspector-back");
    nation.CapitalId = nation.RepresentativeId = 0;
    Call(view, "RefreshInspector", false);
    Assert(
        !Control<Button>(view, "nation-capital").IsEnabled && !Control<Button>(view, "nation-representative").IsEnabled,
        "Missing national targets retained enabled navigation links");
    nation.CapitalId = nextCapital.Id;
    nation.RepresentativeId = representative.Id;
    Call(view, "OpenSettlement", home.Id, "settlement");
    var nationLink = Control<Button>(view, "settlement-nation");
    var townRepresentativeLink = Control<Button>(view, "settlement-representative");
    Assert(!townRepresentativeLink.IsEnabled, "A settlement without a representative omitted the unavailable state");
    home.RepresentativeId = resident.Id;
    resident.Name = "本地新代表";
    Call(view, "RefreshInspector", false);
    Assert(ReferenceEquals(townRepresentativeLink, Control<Button>(view, "settlement-representative"))
           && townRepresentativeLink.IsEnabled && ButtonText(townRepresentativeLink).Contains(resident.Name),
        "Settlement representative link failed to appear when appointed");
    Click(view, "settlement-representative");
    Assert(Field<int>(view, "_selectedResidentId") == resident.Id,
        "Settlement representative link opened another resident");
    Click(view, "inspector-back");
    nationLink = Control<Button>(view, "settlement-nation");
    engine.TransferTerritory(home.X, home.Y, otherNation.Id, 0);
    engine.RenameNation(otherNation.Id, "改名后的归属国家");
    Call(view, "RefreshInspector", false);
    Assert(
        ReferenceEquals(nationLink, Control<Button>(view, "settlement-nation")) && nationLink.IsEnabled &&
        ButtonText(nationLink).Contains(otherNation.Name),
        "Settlement ownership or national rename left its link stale");
    Click(view, "settlement-nation");
    Assert(Field<int>(view, "_selectedNationId") == otherNation.Id, "Settlement link opened the previous owner");
    Click(view, "inspector-back");
    home.RepresentativeId = 999_999;
    Call(view, "RefreshInspector", false);
    Assert(!Control<Button>(view, "settlement-representative").IsEnabled,
        "A removed settlement representative retained an enabled link");
    Call(view, "OpenResident", resident.Id);
    var residentNationLink = Control<Button>(view, "resident-nation");
    var residentTownLink = Control<Button>(view, "resident-settlement-link");
    engine.EditResident(resident.Id, new ResidentEdit { SettlementId = nextCapital.Id });
    resident = engine.GetResident(resident.Id)!;
    engine.RenameNation(nation.Id, "居民的新归属国家");
    nextCapital.Name = "居民的新家园";
    Call(view, "RefreshInspector", false);
    Assert(ReferenceEquals(residentNationLink, Control<Button>(view, "resident-nation")) &&
           residentNationLink.IsEnabled && ButtonText(residentNationLink).Contains(nation.Name)
           && ReferenceEquals(residentTownLink, Control<Button>(view, "resident-settlement-link")) &&
           residentTownLink.IsEnabled && ButtonText(residentTownLink).Contains(nextCapital.Name),
        "Resident migration or renamed destinations left its belonging links stale");
    Click(view, "resident-nation");
    Assert(Field<int>(view, "_selectedNationId") == nation.Id, "Resident link opened the previous nation");
    Click(view, "inspector-back");
    Click(view, "resident-settlement-link");
    Assert(Field<int>(view, "_inspectorSettlementId") == nextCapital.Id,
        "Resident link opened the previous settlement");
    Click(view, "inspector-back");
    resident.NationId = resident.SettlementId = 999_999;
    Call(view, "RefreshInspector", false);
    Assert(
        !Control<Button>(view, "resident-nation").IsEnabled &&
        !Control<Button>(view, "resident-settlement-link").IsEnabled,
        "A resident's missing historical destinations retained enabled links");
}

static void MapPickerContextIsolation()
{
    var engine = TwoTownWorld();
    var view = View(engine);
    var resident = engine.State.Residents[0];
    var other = engine.State.Residents[1];
    Call(view, "OpenInspector", "overview", true);
    Call(view, "OpenResident", resident.Id);
    var map = Map(view);
    map.FollowSelectedResident = true;
    Call(view, "ShowGoalEditor", resident.Id);
    var before = engine.ExportJson();
    var selection = map.CaptureMapSelection();
    Click(view, "map-pick-resident_goal_x");
    var mode = Field<string>(view, "_inspectorMode");
    var category = Field<ToolCategory>(view, "_category");
    var nation = Field<int>(view, "_selectedNationId");
    var town = Field<int>(view, "_inspectorSettlementId");
    var tile = Field<(int X, int Y)?>(view, "_selectedTile");
    var selectedBuilding = Field<int>(view, "_selectedBuildingId");
    var history = Field<ICollection>(view, "_navigation").Count;
    Call(view, "ToggleTools");
    Call(view, "SelectTool", MapTool.ForTerrain(TerrainType.Grass));
    Call(view, "SetCategory", ToolCategory.Build);
    Call(view, "SuspendTool");
    Call(view, "CloseInspector");
    Call(view, "OpenInspector", "overview", true);
    Call(view, "GoBack");
    Call(view, "OpenResident", other.Id);
    Call(view, "OpenNation", other.NationId);
    Call(view, "OpenSettlement", other.SettlementId, "communication");
    Call(view, "OpenTile", 3, 4, true);
    Call(view, "OpenBuilding", engine.State.Society.Buildings[0]);
    Call(view, "FocusEvent", engine.State.Events[0]);
    Assert(
        map.PickingLocation && map.ActiveTool == MapTool.Inspect && !Field<bool>(view, "_mobilePanel") &&
        !Field<bool>(view, "_toolsOpen"),
        "Tools or navigation escaped the active map picker");
    Assert(Field<string>(view, "_inspectorMode") == mode && Field<ToolCategory>(view, "_category") == category
                                                         && Field<int>(view, "_selectedResidentId") == resident.Id &&
                                                         Field<int>(view, "_selectedNationId") == nation
                                                         && Field<int>(view, "_inspectorSettlementId") == town &&
                                                         Field<(int X, int Y)?>(view, "_selectedTile") == tile
                                                         && Field<int>(view, "_selectedBuildingId") ==
                                                         selectedBuilding &&
                                                         Field<ICollection>(view, "_navigation").Count == history
                                                         && map.CaptureMapSelection() == selection,
        "A hidden navigation action altered the picker's source context");
    map.ActiveTool = MapTool.ForTerrain(TerrainType.Grass);
    Call(map, "ApplyTool", map.GetTileScreenPosition(20, 21));
    Assert(!map.PickingLocation && Field<Border>(view, "_modal").IsVisible && Field<bool>(view, "_mobilePanel")
           && Field<string>(view, "_inspectorMode") == mode && Field<int>(view, "_selectedResidentId") == resident.Id,
        "A stale drawing tool failed to return to the original map-picker form");
    Assert(
        Control<NumericUpDown>(view, "resident-goal-x").Value == 20 && Control<NumericUpDown>(view, "resident-goal-y")
                                                                        .Value == 21
                                                                    && map.CaptureMapSelection() == selection &&
                                                                    engine.ExportJson() == before,
        "Map picking applied a terrain tool or lost its explicit coordinates and selection");
    Click(view, "modal-cancel");
}

static void ResearchNavigationState()
{
    var application = Application.Current!;
    var theme = new FluentTheme();
    application.Styles.Add(theme);
    var engine = TwoTownWorld();
    var view = View(engine);
    var before = engine.ExportJson();
    var inspectorScroll = Control<ScrollViewer>(view, "inspector-scroll");
    inspectorScroll.Width = 280;
    inspectorScroll.Height = 420;
    var window = new Window { Content = view, Width = 1000, Height = 800 };

    void LayoutResearch()
    {
        inspectorScroll.ApplyTemplate();
        view.Measure(new Size(1000, 800));
        view.Arrange(new Rect(0, 0, 1000, 800));
        var body = Field<Grid>(view, "_body");
        body.Measure(new Size(1000, 730));
        body.Arrange(new Rect(0, 0, 1000, 730));
        inspectorScroll.Measure(new Size(280, 420));
        inspectorScroll.Arrange(new Rect(0, 0, 280, 420));
        var graph = Control<ResearchGraphControl>(view, "research-graph");
        graph.Measure(new Size(280, 560));
        graph.Arrange(new Rect(0, 0, 280, 560));
        Dispatcher.UIThread.RunJobs();
    }

    try
    {
        window.Show();
        Call(view, "OpenSettlement", engine.State.Settlements[0].Id, "research");
        LayoutResearch();
        Click(view, "research-branch-工业与能源");
        Click(view, "research-node-EnergyRecycling");
        Click(view, "research-development");
        Click(view, "research-full-path");
        var original = Control<ResearchGraphControl>(view, "research-graph");
        original.SetZoom(1.25);
        LayoutResearch();
        Click(view, "research-focus");
        var viewport = original.CaptureViewport();
        Assert(viewport.Offset.Length > 0, "Research fixture did not pan beyond the graph origin");
        inspectorScroll.Offset = new Vector(0, 180);
        LayoutResearch();
        var inspectorOffset = inspectorScroll.Offset;
        Assert(inspectorOffset.Y > 0,
            $"Research fixture did not scroll the outer inspector: extent {inspectorScroll.Extent}, viewport {inspectorScroll.Viewport}, bounds {inspectorScroll.Bounds}");
        viewport = original.CaptureViewport();
        Call(view, "OpenSettlement", engine.State.Settlements[1].Id, "research");
        LayoutResearch();
        Click(view, "research-route-magic");
        Click(view, "research-branch-元素与结界");
        Click(view, "research-node-Elementalism");
        Click(view, "research-development");
        Control<ResearchGraphControl>(view, "research-graph").SetZoom(.5);
        Click(view, "inspector-back");
        LayoutResearch();
        var restored = Control<ResearchGraphControl>(view, "research-graph");
        Assert(Field<int>(view, "_inspectorSettlementId") == engine.State.Settlements[0].Id
               && Field<Advancement>(view, "_selectedResearch") == Advancement.EnergyRecycling
               && Field<ResearchRoute>(view, "_researchRoute") == ResearchRoute.Technology &&
               Field<ResearchBranch>(view, "_researchBranch") == ResearchBranch.Industry
               && Field<bool>(view, "_civilizationDetails"),
            "Returning restored the town but discarded its research selection or filters");
        Assert(restored.CaptureViewport() == viewport,
            "Returning discarded research zoom, pan or full-path presentation");
        Assert(inspectorScroll.Offset == inspectorOffset,
            "Returning discarded the inspector's independent scroll position");
        Assert(engine.ExportJson() == before, "Research view navigation changed the world");
    }
    finally
    {
        window.Close();
        application.Styles.Remove(theme);
    }
}

static void SpellContext()
{
    var engine = TwoTownWorld();
    var town = engine.State.Settlements[0];
    var local = engine.State.Residents[0];
    var remote = engine.State.Residents[1];
    local.Name = "本地施法者";
    local.Age = remote.Age = 25;
    local.Profession = remote.Profession = Profession.Mage;
    local.MagicTalent = remote.MagicTalent = 50;
    local.MagicTraining = 12;
    remote.MagicTraining = 90;
    local.Mana = remote.Mana = 100;
    remote.Name = "远方施法者";
    engine.SpawnResidents(town.X, town.Y, RaceKind.Human, 3);
    var extra = engine.State.Residents.Where(r => r.Id != local.Id && r.Id != remote.Id).ToArray();
    var alternate = extra[0];
    alternate.Name = "本地候补施法者";
    alternate.Age = 25;
    alternate.MagicTalent = 50;
    alternate.MagicTraining = 8;
    alternate.X = town.X + 1;
    alternate.Y = town.Y + 1;
    extra[1].Name = "已死亡施法者";
    extra[1].Age = 30;
    extra[1].Health = 0;
    extra[1].MagicTraining = 100;
    extra[2].Name = "未成年施法者";
    extra[2].Age = 8;
    extra[2].MagicTraining = 100;
    engine.GrantReceivedResearch(town.Id, Advancement.ArcaneArts);
    engine.GrantReceivedResearch(town.Id, Advancement.ManaAttunement);
    engine.GrantReceivedResearch(town.Id, Advancement.Elementalism);
    var view = View(engine);
    var before = engine.ExportJson();
    Call(view, "OpenResident", remote.Id);
    Call(view, "OpenSettlement", town.Id, "research");
    Click(view, "research-route-magic");
    Click(view, "research-node-Elementalism");
    Click(view, "research-spell-FrostBolt");
    var caster = Control<ComboBox>(view, "spell-caster");
    Assert(caster.SelectedItem?.ToString()?.Contains(local.Name) == true,
        "Research from the local town selected a distant caster");
    Assert(caster.Items.Cast<object>().All(item => !item.ToString()!.Contains(remote.Name)
                                                   && !item.ToString()!.Contains(extra[1].Name) &&
                                                   !item.ToString()!.Contains(extra[2].Name)),
        "Local caster choices contain a distant, dead or underage resident");
    var choices = caster.Items.Cast<object>().ToArray();
    var alternateIndex = Array.FindIndex(choices, item => item.ToString()!.Contains(alternate.Name));
    var localIndex = Array.FindIndex(choices, item => item.ToString()!.Contains(local.Name));
    Assert(alternateIndex >= 0 && localIndex >= 0, "Caster selection omitted a local living adult");
    caster.SelectedIndex = alternateIndex;
    Assert(
        Control<NumericUpDown>(view, "spell-x").Value == alternate.X &&
        Control<NumericUpDown>(view, "spell-y").Value == alternate.Y,
        "Changing a caster left the untouched default target at the previous caster");
    Click(view, "map-pick-spell_x");
    Call(view, "FinishMapPick", alternate.X, alternate.Y);
    caster.SelectedIndex = localIndex;
    Assert(
        Control<NumericUpDown>(view, "spell-x").Value == alternate.X &&
        Control<NumericUpDown>(view, "spell-y").Value == alternate.Y,
        "Selecting the existing map coordinates failed to mark an explicit spell target");
    Control<NumericUpDown>(view, "spell-x").Value = 15;
    Control<NumericUpDown>(view, "spell-y").Value = 16;
    caster.SelectedIndex = alternateIndex;
    Assert(Control<NumericUpDown>(view, "spell-x").Value == 15 && Control<NumericUpDown>(view, "spell-y").Value == 16,
        "Changing a caster overwrote the user's chosen target");
    Control<NumericUpDown>(view, "spell-x").Value = 15.5m;
    Assert(
        Control<TextBlock>(view, "spell-requirements").Text?.Contains("整数") == true &&
        !Control<Button>(view, "spell-apply").IsEnabled,
        "A fractional spell coordinate threw or retained an enabled action");
    Click(view, "modal-cancel");
    Call(view, "OpenResident", remote.Id);
    Click(view, "resident-spell");
    Assert(Control<ComboBox>(view, "spell-caster").SelectedItem?.ToString()?.Contains(remote.Name) == true,
        "Resident spell entry selected someone other than the inspected resident");
    Click(view, "modal-cancel");
    Assert(engine.ExportJson() == before, "Spell selection or coordinate defaults changed the world");
}

static void AdvancedResearchUi()
{
    var engine = TwoTownWorld();
    var town = engine.State.Settlements[0];
    engine.SetNationResources(town.NationId, 1000, 1000, 1000, 1000, 100, 100, 100);
    engine.GrantFacility(town.Id, BuildingKind.Academy, town.X + 2, town.Y + 2);
    var view = View(engine);
    Call(view, "OpenInspector", "research", true);
    var before = engine.ExportJson();
    Assert(Control<TextBlock>(view, "research-requirements").Text?.Contains("投入材料：") == true,
        "Initial research selection hid its cost and conditions");
    Click(view, "research-node-Electrification");
    Assert(view.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text?.Contains("动力工厂") == true),
        "Research choice has no concrete unlock description");
    Assert(!Control<Button>(view, "research-start").IsEnabled, "Locked research has an enabled submit button");
    Assert(engine.ExportJson() == before, "Inspecting the research tree changed the world");
    Assert(
        !view.GetLogicalDescendants().OfType<ComboBox>()
            .Any(c => AutomationProperties.GetAutomationId(c) == "research-kind"),
        "Tree still uses a research dropdown");
    Assert(
        ResearchRules.All.All(r =>
            view.GetLogicalDescendants().OfType<Button>()
                .Any(b => AutomationProperties.GetAutomationId(b) == "research-node-" + r.Key)),
        "Tree omits research branches");
    var graph = Control<ResearchGraphControl>(view, "research-graph");

    void CheckGraph(IEnumerable<Advancement> definitions)
    {
        var expected = definitions.ToArray();
        Assert(graph.Layout.Nodes.Count == expected.Length, "Displayed graph has the wrong nodes");
        Assert(
            graph.Layout.Edges.Select(e => (e.From, e.To)).ToHashSet()
                .SetEquals(expected.SelectMany(d => d.Prerequisites.Select(p => (p, d)))),
            "Drawn connectors do not match the actual prerequisites");
        foreach (var (kind, rect) in graph.Layout.Nodes)
        {
            Assert(graph.Layout.Nodes.Where(n => n.Key != kind).All(n => !n.Value.Intersects(rect)),
                "Research nodes overlap");
            foreach (var edge in graph.Layout.Edges)
                for (var i = 1; i < edge.Points.Count; i++)
                {
                    var a = edge.Points[i - 1];
                    var b = edge.Points[i];
                    var crosses = a.X == b.X
                        ? a.X > rect.Left && a.X < rect.Right && Math.Min(a.Y, b.Y) < rect.Bottom &&
                          Math.Max(a.Y, b.Y) > rect.Top
                        : a.Y > rect.Top && a.Y < rect.Bottom && Math.Min(a.X, b.X) < rect.Right &&
                          Math.Max(a.X, b.X) > rect.Left;
                    Assert(!crosses, "A prerequisite connector passes through a research node");
                }
        }
    }

    var combinedEdges = graph.Layout.Edges.Select(e => (e.From, e.To)).ToHashSet();
    CheckGraph(ResearchRules.Route(false));
    Click(view, "research-route-magic");
    CheckGraph(ResearchRules.Route(true));
    combinedEdges.UnionWith(graph.Layout.Edges.Select(e => (e.From, e.To)));
    Assert(combinedEdges.SetEquals(ResearchRules.All.SelectMany(d => d.Prerequisites.Select(p => (p, d)))),
        "Separate empire trees omit an actual dependency");
    Click(view, "research-route-common");
    CheckGraph(ResearchRules.All.Where(d => d.Shared));
    Click(view, "research-route-technology");
    Click(view, "research-node-Electrification");
    Click(view, "research-zoom-out");
    Click(view, "research-zoom-in");
    var sameNode = Control<Button>(view, "research-node-Electrification");
    Call(view, "RefreshInspector", false);
    Assert(
        ReferenceEquals(sameNode, Control<Button>(view, "research-node-Electrification")) &&
        engine.ExportJson() == before,
        "Refreshing, filtering or zooming the graph changed the world or replaced its nodes");
    view.Arrange(new Rect(0, 0, 390, 844));
    Call(view, "ApplyLayout");
    Click(view, "research-expand");
    Assert((Control<Button>(view, "inspector-expand").Content as TextBlock)?.Text == "收起",
        "Compact tree expansion left its header action stale");
    engine.GrantReceivedResearch(town.Id, Advancement.Industry);
    engine.GrantReceivedResearch(town.Id, Advancement.SignalNetwork);
    Call(view, "RefreshInspector", false);
    Assert(Control<TextBlock>(view, "research-requirements").Text?.Contains("前置知识与魔法规则已满足") == true,
        "Research requirements stayed stale after receiving prerequisite knowledge");
    Click(view, "research-start");
    Assert(
        engine.State.Society.Research.Single(r => r.SettlementId == town.Id).ActiveProject ==
        Advancement.Electrification,
        "Valid advanced research did not start through the ordinary UI");
}

static void AdvancedResourcesUi()
{
    var engine = TwoTownWorld();
    var town = engine.State.Settlements[0];
    var resident = engine.State.Residents[0];
    engine.SetNationResources(town.NationId, alloy: 20.25, energyCells: 5.5, crystals: 7.75);
    var stocks = engine.State.Settlements
        .Select(t => (t.Resources.Alloy, t.Resources.EnergyCells, t.Resources.Crystals)).ToArray();
    var view = View(engine);
    Call(view, "ShowNationEditor", town.NationId);
    Control<TextBox>(view, "nation-name").Text = "保留双线物资";
    Click(view, "nation-apply");
    Assert(
        stocks.SequenceEqual(engine.State.Settlements.Select(t =>
            (t.Resources.Alloy, t.Resources.EnergyCells, t.Resources.Crystals))),
        "Rename changed advanced resource distribution");
    Call(view, "ShowBuildingEditor", town.Id);
    Assert(
        Control<TextBlock>(view, "building-requirements").Text
            ?.Contains(WorldEngine.BuildingDescription(BuildingKind.Farm)) == true,
        "Initial building selection hid its purpose");
    Assert(!Control<StackPanel>(view, "building-bridge-options").IsVisible,
        "Farm editor showed unrelated bridge fields");
    Control<ComboBox>(view, "building-kind").SelectedItem = BuildingKind.Bridge;
    Assert(Control<StackPanel>(view, "building-bridge-options").IsVisible, "Bridge editor hid direction and level");
    Control<ComboBox>(view, "building-kind").SelectedItem = BuildingKind.Fabricator;
    Assert(!Control<StackPanel>(view, "building-bridge-options").IsVisible,
        "Changing facility left bridge fields visible");
    Control<NumericUpDown>(view, "building-x").Value = town.X + 2;
    Control<NumericUpDown>(view, "building-y").Value = town.Y + 2;
    Assert(Control<TextBlock>(view, "building-placement-status").Text!.Contains("尚未掌握"),
        "Construction preview omitted missing knowledge");
    Control<CheckBox>(view, "building-gift").IsChecked = true;
    Assert(Control<TextBlock>(view, "building-placement-status").Text!.Contains("可直接赐予"),
        "Gift preview retained paid construction restrictions");
    var beforePreview = engine.ExportJson();
    Control<NumericUpDown>(view, "building-x").Value = town.X;
    Control<NumericUpDown>(view, "building-y").Value = town.Y;
    Assert(Control<TextBlock>(view, "building-placement-status").Text!.Contains("已有建筑"),
        "Preview did not follow edited coordinates");
    Assert(engine.ExportJson() == beforePreview, "Construction preview changed the world");
    Control<NumericUpDown>(view, "building-x").Value = town.X + 2;
    Control<NumericUpDown>(view, "building-y").Value = town.Y + 2;
    Click(view, "building-apply");
    var factory = engine.State.Society.Buildings.Single(b => b.Kind == BuildingKind.Fabricator);
    Assert(factory.IsCompleted && engine.GetProductionStatus(factory.Id).Contains("知识"),
        "Gift bypassed operating prerequisites");
}

static void GoalRouteRefresh()
{
    var engine = TwoTownWorld();
    var view = View(engine);
    var resident = engine.State.Residents[0];
    var map = Map(view);
    Call(view, "OpenResident", resident.Id);
    Call(view, "ShowGoalEditor", resident.Id);
    Control<ComboBox>(view, "resident-goal").SelectedItem = AgentGoalKind.Work;
    Control<ComboBox>(view, "resident-goal-entity").SelectedIndex = 0;
    Control<NumericUpDown>(view, "resident-goal-x").Value = 15;
    Control<NumericUpDown>(view, "resident-goal-y").Value = 12;
    Click(view, "resident-goal-apply");
    Assert(Field<IReadOnlyList<RoutePoint>>(map, "_selectedRoute").Count > 1,
        "Paused edit left the cached route empty");
    Assert(engine.State.Tick == 0, "Refreshing the preview advanced simulation");
}

static void EmptyRuleNumber()
{
    var engine = TwoTownWorld();
    var view = View(engine);
    Call(view, "ShowRules");
    var before = engine.ExportJson();
    Control<NumericUpDown>(view, "rule-gathering-rate").Value = null;
    Click(view, "world-rules-apply");
    Assert(engine.ExportJson() == before && Field<Border>(view, "_modal").IsVisible,
        "Empty input changed world or closed the form");
}

static void ImportedSeparators()
{
    var engine = TwoTownWorld();
    var id = engine.State.Residents[0].Id;
    engine.EditResident(id, new ResidentEdit { Name = "伊恩\u00B7河翼" });
    var before = engine.ExportJson();
    var view = View(engine);
    Call(view, "OpenResident", id);
    Assert(view.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text?.Contains("伊恩 河翼") == true),
        "Legacy name separators remain in the visible title");
    Call(view, "RefreshUi", true);
    Assert(engine.ExportJson() == before, "Formatting rewrote the saved name or history");
    Call(view, "OpenInspector", "residents", true);
    Control<TextBox>(view, "resident-search").Text = "伊恩 河翼";
    Call(view, "RefreshUi", true);
    Assert(Control<Button>(view, $"resident-row-{id}").IsEnabled, "Displayed legacy name cannot be searched");
}

static void ContextDetails()
{
    var engine = TwoTownWorld();
    var town = engine.State.Settlements[0];
    var tile = engine.State.Tiles[town.Y * engine.State.Width + town.X];
    tile.Rainfall = .25;
    tile.NaturalWaterYield = 1.5;
    var view = View(engine);
    Call(view, "OpenResident", engine.State.Residents[0].Id);
    Assert(
        !view.GetLogicalDescendants().OfType<Control>()
            .Any(c => AutomationProperties.GetAutomationId(c) == "inspector-overview"),
        "Resident detail retained unrelated world tabs");
    Call(view, "SelectMapObject", "tile", 0, town.X, town.Y);
    var summary = Field<TextBlock>(view, "_selectionText").Text!;
    Assert(
        !summary.Contains("位置") &&
        summary.Contains($"（{tile.ResourceAmount:0.#}/{WorldEngine.NaturalResourceCapacity(tile):0.#}）"),
        "Tile selection omitted the compact stock/capacity or retained coordinates");
    Call(view, "OpenInspector", "tile", true);
    var water = Control<TextBlock>(view, "tile-water").Text!;
    Assert(water.Contains("供水量 1.5 / 日") && !water.Contains("今日剩余") && !water.Contains("海拔"),
        "Supply omitted the combined daily total or elevation occupied the main detail");
    Assert(
        !view.GetLogicalDescendants().OfType<TextBlock>()
            .Any(t => t.Text?.Contains("降水") == true || t.Text?.Contains("补水") == true),
        "Source breakdown leaked into main or folded details");
    Assert(!Control<Expander>(view, "tile-geography").IsExpanded, "Elevation was not folded");
    tile.DroughtTicks = 2;
    Call(view, "RefreshUi", true);
    Assert(Control<TextBlock>(view, "tile-water").Text!.Contains("供水量 0.3 / 日"),
        "Supply did not reflect drought in the same field");
    var terrain = tile.Terrain;
    tile.Terrain = TerrainType.Lake;
    Call(view, "RefreshUi", true);
    Assert(Control<TextBlock>(view, "tile-water").Text!.Contains("供水量 无限"), "Fresh water used a separate supply label");
    tile.Terrain = terrain;
    tile.DroughtTicks = 0;
    var well = engine.State.Society.Buildings.First(b =>
        b.Kind == BuildingKind.TownCenter && b.SettlementId == town.Id);
    well.Kind = BuildingKind.Well;
    tile.NaturalWaterYield = .03;
    Call(view, "OpenBuilding", well);
    Assert(Control<TextBlock>(view, "building-effects").Text!.Contains("供水量 0.3 / 日"),
        "Well inspector still shows the tiny natural surface supply");
    Call(view, "SelectMapObject", "tile", 0, town.X, town.Y);
    Call(view, "OpenInspector", "tile", true);
    Assert(Control<TextBlock>(view, "tile-water").Text!.Contains("供水量 0.33 / 日"),
        "Tile inspector did not combine the functioning well in its single supply field");
    well.Kind = BuildingKind.TownCenter;
    tile.NaturalWaterYield = 1.5;
    Call(view, "ShowLandProject", town.X, town.Y);
    Assert(
        !view.GetLogicalDescendants().OfType<TextBlock>().Any(t =>
            t.IsVisible && t.GetLogicalAncestors().OfType<Control>().All(c => c.IsVisible) &&
            t.Text is "桥梁方向" or "桥梁等级"), "Ordinary land buildings retained direction labels");
    Call(view, "CloseModal");
    var center = engine.State.Society.Buildings.First(b => b.Kind == BuildingKind.TownCenter);
    Call(view, "OpenBuilding", center);
    Assert(view.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text?.Contains("家园粮仓") == true),
        "Center details omit their actual stock and function");
    Assert(
        !view.GetLogicalDescendants().OfType<TextBlock>()
            .Any(t => t.Text?.Contains("建造原因") == true || t.Text?.Contains("距中心") == true),
        "Planning internals leaked into selected building detail");
    var before = engine.ExportJson();
    Call(view, "OpenInspector", "guide", true);
    var guide = Control<TextBlock>(view, "guide-water").Text!;
    Assert(guide.Contains("供水量") && !guide.Contains("降水") && !guide.Contains("补水"),
        "Guide retained separate water sources");
    Assert(engine.ExportJson() == before, "Reading the water guide changed the world");
}

static void ResidentSearch()
{
    var engine = TwoTownWorld();
    var dead = engine.State.Residents[0];
    engine.EditResident(dead.Id, new ResidentEdit { Health = 0 });
    engine.Step();
    var view = View(engine);
    Call(view, "OpenInspector", "residents", true);
    Assert(
        !view.GetLogicalDescendants().OfType<Button>()
            .Any(b => AutomationProperties.GetAutomationId(b) == $"resident-row-{dead.Id}"),
        "Resident list included deceased people by default");
    var input = Control<TextBox>(view, "resident-search");
    input.Text = "测试输入";
    Call(view, "RefreshUi", true);
    Call(view, "RefreshUi", true);
    Assert(ReferenceEquals(input, Control<TextBox>(view, "resident-search")) && input.Text == "测试输入",
        "Timed updates replaced the active search editor");
    input.Text = dead.Id.ToString();
    Control<CheckBox>(view, "residents-deceased").IsChecked = true;
    Call(view, "RefreshUi", true);
    Assert(Control<Button>(view, $"resident-row-{dead.Id}").IsEnabled,
        "Explicitly including deceased people lost their historical records");
}

static void VisibleTerrain()
{
    var engine = EmptyWorld(42, 144);
    var map = Map(View(engine));
    map.Arrange(new Rect(0, 0, 320, 480));
    map.RefreshWorld(true);
    var before = engine.ExportJson();
    for (var i = 0; i < 20; i++) map.ZoomIn();
    map.RefreshWorld();
    Assert(map.TerrainTilesScanned < engine.State.Tiles.Length / 8,
        $"Near camera scans {map.TerrainTilesScanned} of {engine.State.Tiles.Length} tiles");
    Assert(engine.ExportJson() == before, "Visible chunk caching changed the simulation");
}

static void TerrainResourceImages()
{
    var engine = WorldEngine.Create(42, 32, 32, false);
    engine.State.NaturalDisasters = false;
    foreach (var tile in engine.State.Tiles)
    {
        tile.Terrain = TerrainType.Grass;
        tile.ResourceAmount = 100;
    }

    var map = Map(View(engine));
    map.RefreshWorld(true);

    object? Image()
    {
        var chunks = Field<IDictionary>(map, "_chunks");
        var chunk = chunks.Values.Cast<object>().Single();
        return chunk.GetType().GetProperty("Terrain")!.GetValue(chunk);
    }

    var grass = engine.State.Tiles[10 * 32 + 10];
    var original = Image();
    grass.ResourceAmount = 20;
    engine.Step();
    map.RefreshWorld();
    Assert(ReferenceEquals(original, Image()), "Grass harvesting rebuilt an unchanged terrain image");
    grass.Terrain = TerrainType.Forest;
    grass.ResourceAmount = 30;
    map.RefreshWorld();
    var forest = Image();
    Assert(!ReferenceEquals(original, forest), "A real terrain change failed to rebuild the image");
    grass.ResourceAmount = 27;
    map.RefreshWorld();
    Assert(ReferenceEquals(forest, Image()), "Forest resources rebuilt the same standing trees");
    grass.ResourceAmount = 24;
    map.RefreshWorld();
    var stump = Image();
    Assert(!ReferenceEquals(forest, stump), "Depleted forest did not show its stump");
    grass.ResourceAmount = 26;
    var before = engine.ExportJson();
    map.RefreshWorld();
    Assert(!ReferenceEquals(stump, Image()) && engine.ExportJson() == before,
        "Forest recovery remained stale or observation changed the world");
}

static void EcologyCache()
{
    var engine = EmptyWorld(42, 32);
    foreach (var tile in engine.State.Tiles)
    {
        tile.Terrain = TerrainType.Grass;
        tile.Fertility = 100;
        tile.ResourceAmount = 150;
        tile.NaturalWaterYield = .02;
        tile.Plants = new PlantCoverage { Grass = .6 };
        tile.Wildlife = WildlifeKind.Rabbit;
        tile.WildlifePopulation = .5;
    }

    var map = Map(View(engine));
    map.FocusTile(16, 16);
    for (var i = 0; i < 7; i++) map.ZoomIn();
    using var target = new RenderTargetBitmap(new PixelSize(256, 256), new Vector(96, 96));

    void Draw()
    {
        using var context = target.CreateDrawingContext();
        Call(map, "DrawEcology", context, engine.State);
    }

    var before = engine.ExportJson();
    Draw();
    var builds = map.EcologyCacheBuildCount;
    var plants = map.RenderedPlantCount;
    Assert(map.RenderedWildlifeCount > 0 && plants > 0, "Ecology fixture is not visible");
    engine.State.Tick++;
    map.RefreshWorld();
    Draw();
    Assert(map.EcologyCacheBuildCount == builds, "An unrelated simulation day rebuilt ecology");
    engine.State.Tick--;
    Assert(engine.ExportJson() == before, "Drawing ecology changed the saved world");
    foreach (var tile in engine.State.Tiles) tile.ResourceAmount = 200;
    map.RefreshWorld();
    Draw();
    Assert(map.EcologyCacheBuildCount == builds, "Saturated resources rebuilt identical ecology");
    var center = engine.State.Tiles[16 * 32 + 16];
    center.WildlifePopulation = 2;
    map.RefreshWorld();
    Draw();
    Assert(map.EcologyCacheBuildCount == ++builds, "A real animal edit remained stale");
    center.ResourceAmount = 0;
    map.RefreshWorld();
    Draw();
    Assert(map.EcologyCacheBuildCount == ++builds && map.RenderedPlantCount == plants - 1,
        "Resource exhaustion left a plant icon behind");
    map.ShowWildlife = false;
    map.RefreshWorld();
    Draw();
    Assert(map.EcologyCacheBuildCount == ++builds && map.RenderedWildlifeCount == 0,
        "The animal visibility control retained cached icons");
    center.Deposit = ResourceKind.Coal;
    map.ResourceVisibility = ResourceVisibility.All;
    map.RefreshWorld();
    Draw();
    Assert(map.EcologyCacheBuildCount == ++builds, "A newly visible deposit remained stale");
    var viewport = ((int Left, int Right, int Top, int Bottom))Call(map, "VisibleTiles", engine.State, 2)!;
    Assert(viewport.Bottom + 1 < engine.State.Height, "Water-flow fixture requires an offscreen neighbour");
    engine.State.Tiles[viewport.Bottom * 32 + 16].Terrain = TerrainType.River;
    map.RefreshWorld();
    Draw();
    builds = map.EcologyCacheBuildCount;
    engine.State.Tiles[(viewport.Bottom + 1) * 32 + 16].Terrain = TerrainType.River;
    map.RefreshWorld();
    Draw();
    var streams = Field<List<(Point Start, Point End)>>(map, "_waterStreams");
    Assert(map.EcologyCacheBuildCount == builds + 1 && streams.Single().Start.X == streams.Single().End.X,
        "An offscreen river neighbour left the cached flow direction stale");
}

static void PixelRectangles()
{
    var type = typeof(WorldMapControl).Assembly.GetType("SeWZC.WorldBox.UI.Controls.PixelCanvas")!;
    var canvas = Activator.CreateInstance(type, 3, 2)!;
    type.GetMethod("Rect")!.Invoke(canvas, [-1, 0, 3, 2, 0x11223344u]);
    var pixels = (byte[])type.GetProperty("Pixels")!.GetValue(canvas)!;
    byte[] expected =
    [
        0x11, 0x22, 0x33, 0x44, 0x11, 0x22, 0x33, 0x44, 0, 0, 0, 0,
        0x11, 0x22, 0x33, 0x44, 0x11, 0x22, 0x33, 0x44, 0, 0, 0, 0,
    ];
    Assert(pixels.SequenceEqual(expected), "Clipped rectangle changed channels or wrote outside its bounds");
    type.GetMethod("Rect")!.Invoke(canvas, [3, -2, 5, 1, 0xFFFFFFFFu]);
    Assert(pixels.SequenceEqual(expected), "An off-canvas rectangle changed pixels");
}

static void IncrementalTerrain()
{
    var engine = WorldEngine.Create(42, 64, 64, false);
    engine.State.NaturalDisasters = false;
    var map = Map(View(engine));
    map.RefreshWorld(true);

    Dictionary<object, byte[]> Pixels(WorldMapControl control)
    {
        var chunks = Field<IDictionary>(control, "_chunks");
        return chunks.Keys.Cast<object>().ToDictionary(key => key, key =>
        {
            var chunk = chunks[key]!;
            var canvas = chunk.GetType().GetProperty("TerrainCanvas")!.GetValue(chunk)!;
            return (byte[])canvas.GetType().GetProperty("Pixels")!.GetValue(canvas)!;
        });
    }

    var initialBuffers = Pixels(map);

    void Verify()
    {
        var before = engine.ExportJson();
        map.RefreshWorld();
        var fresh = Map(View(engine));
        fresh.RefreshWorld(true);
        var actual = Pixels(map);
        var expected = Pixels(fresh);
        Assert(expected.Count == actual.Count && expected.All(pair => actual[pair.Key].SequenceEqual(pair.Value)),
            "A partial update differs from a complete redraw, including its gutter");
        Assert(actual.All(pair => ReferenceEquals(pair.Value, initialBuffers[pair.Key])),
            "An edit replaced the managed chunk buffer");
        Assert(engine.ExportJson() == before, "Terrain observation changed the saved world");
    }

    var center = engine.State.Tiles[20 * 64 + 20];
    center.Terrain = TerrainType.Desert;
    Verify();
    Assert(map.TerrainTilesDrawn <= 9, "A one-tile edit redrew the entire chunk");
    foreach (var x in new[] { 0, 31, 32, 63 })
    {
        var tile = engine.State.Tiles[31 * 64 + x];
        tile.Terrain = TerrainType.River;
        tile.RoadLevel = 1;
        Verify();
        tile.Terrain = TerrainType.Mountain;
        tile.RoadLevel = 0;
        Verify();
        tile.Terrain = TerrainType.Forest;
        tile.ResourceAmount = 30;
        tile.DroughtTicks = 3;
        Verify();
        tile.ResourceAmount = 24;
        Verify();
        // 干旱森林与普通灌丛曾在旧图像哈希中冲突。
        tile.Terrain = TerrainType.Scrub;
        tile.DroughtTicks = 0;
        Verify();
    }
}

static void ResidentGeometryLayers()
{
    var engine = WorldEngine.Create(42, 32, 32, false);
    engine.State.Tiles[16 * 32 + 16].Terrain = TerrainType.Grass;
    engine.SpawnResidents(16, 16, RaceKind.Human, 1);
    var person = engine.State.Residents.Single();
    person.Inventory.Food = 4;
    var before = engine.ExportJson();
    var map = Map(View(engine));
    map.Arrange(new Rect(0, 0, 200, 200));
    map.FitWorld();
    Call(map, "RebuildResidents");
    Assert(Field<object?>(map, "_cargoGeometry") is null && Field<object?>(map, "_messageGeometry") is null
                                                         && Field<object?>(map, "_magicGeometry") is null,
        "Subpixel overview badges still constructed geometry");
    map.FocusTile(person.X, person.Y);
    Call(map, "RebuildResidents");
    Assert(Field<object?>(map, "_cargoGeometry") is not null, "Readable medium-zoom cargo was removed");
    for (var i = 0; i < 4; i++) map.ZoomIn();
    Call(map, "RebuildResidents");
    Assert(Field<object?>(map, "_cargoGeometry") is null && Field<object?>(map, "_heads") is null,
        "Near-scene sprites also constructed unused silhouette details");
    Assert(Field<IDictionary>(map, "_renderedResidentPoints").Contains(person.Id),
        "Near residents lost their click position");
    Assert(engine.ExportJson() == before, "Changing resident detail levels changed the world");
}

static void SaveCaptureBoundary()
{
    var engine = TwoTownWorld();
    var view = View(engine);
    var map = Map(view);

    void Set(string field, object value)
    {
        typeof(MainView).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(view, value);
    }

    using var capture = new CancellationTokenSource();
    Set("_saveCapture", capture);
    Set("_ready", true);
    Set("_paused", false);
    var tick = engine.State.Tick;
    Call(view, "OnTick", null, EventArgs.Empty);
    Assert(engine.State.Tick == tick, "Simulation advanced while a save was reading the world");
    Call(view, "FinishSaveCapture", capture);
    map.ActiveTool = MapTool.ForTerrain(TerrainType.Grass);
    Call(map, "ApplyTool", map.GetTileScreenPosition(30, 30));
    using var continuing = new CancellationTokenSource();
    Set("_saveCapture", continuing);
    Call(map, "ApplyTool", map.GetTileScreenPosition(31, 30));
    Assert(continuing.IsCancellationRequested, "Continuing stroke did not cancel the incomplete save");
    Call(view, "FinishSaveCapture", continuing);
}

static void AutosaveChanges()
{
    var previous = App.Storage;
    var storage = new CountingSaveStorage();
    try
    {
        App.Storage = storage;
        var engine = TwoTownWorld();
        var view = View(engine);
        SetField(view, "_ready", true);

        void Save(bool manual = false)
        {
            var task = (Task)Call(view, "SaveAsync", manual)!;
            AwaitUi(() => task.IsCompleted, "Save did not complete");
            task.GetAwaiter().GetResult();
        }

        Save();
        Save();
        Assert(storage.Calls == 1, "An unchanged world was captured again");
        Save(true);
        Assert(storage.Calls == 2, "Explicit save was skipped");
        engine.Step();
        Save();
        Assert(storage.Calls == 3, "Simulation change was skipped");
        var edit = (Task<bool>)Call(view, "SubmitEditAsync", (Action)(() => engine.State.Residents[0].Name = "已编辑"),
            false)!;
        AwaitUi(() => edit.IsCompleted, "Edit did not complete");
        Assert(edit.Result, "Edit was canceled");
        Save();
        Assert(storage.Calls == 4, "Same-tick edit was skipped");
        engine.Step();
        storage.FailNext = true;
        Save();
        Save();
        Assert(storage.Calls == 6, "Failed save incorrectly marked the world as saved");
        Call(view, "ReplaceWorld", WorldEngine.ImportJson(engine.ExportJson()));
        Save();
        Assert(storage.Calls == 7 && storage.Chunks > 1, "Replacement or chunked storage was skipped");
        var map = Map(view);
        map.ActiveTool = MapTool.ForTerrain(TerrainType.Grass);
        SetField(view, "_lastSave", -31d);
        Call(map, "ApplyTool", map.GetTileScreenPosition(30, 30));
        Call(view, "OnTick", null, EventArgs.Empty);
        AwaitUi(() => !Field<bool>(view, "_saving"), "Post-edit save did not settle");
        Assert(storage.Calls == 7, "Completing an edit immediately started another automatic capture");
    }
    finally
    {
        App.Storage = previous;
    }
}

static void DesktopSave()
{
    var directory = Path.Combine(Path.GetTempPath(), "worldbox-save-test-" + Guid.NewGuid().ToString("N"));
    try
    {
        var path = Path.Combine(directory, "autosave.worldbox");
        var storage = new DesktopWorldStorage(path);
        var json = TwoTownWorld().ExportJson();
        var write = storage.SaveChunksAsync([json[..(json.Length / 2)], json[(json.Length / 2)..]]);
        AwaitUi(() => write.IsCompleted, "Desktop write did not complete");
        write.GetAwaiter().GetResult();
        var saved = File.ReadAllBytes(path);
        Assert(saved[0] == 0x1f && saved[1] == 0x8b && saved.Length < json.Length / 2,
            "Desktop save was not compressed");
        var read = storage.LoadAsync();
        AwaitUi(() => read.IsCompleted, "Desktop read did not complete");
        Assert(read.GetAwaiter().GetResult() == json, "Desktop gzip changed the complete world");
        try
        {
            write = storage.SaveChunksAsync(["\ud800"]);
            AwaitUi(() => write.IsCompleted, "Failed desktop write did not complete");
            write.GetAwaiter().GetResult();
            throw new Exception("Malformed UTF-16 was saved");
        }
        catch (EncoderFallbackException)
        {
        }

        Assert(File.ReadAllBytes(path).SequenceEqual(saved) && Directory.GetFiles(directory).Length == 1,
            "Failed encoding replaced the last complete save or leaked a temporary file");
    }
    finally
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }
}

static void FoldedDetails()
{
    var engine = TwoTownWorld();
    var view = View(engine);
    Call(view, "OpenResident", engine.State.Residents[0].Id);
    var fold = Control<Expander>(view, "resident-cognition");
    Assert(!fold.IsExpanded && !Control<Expander>(view, "resident-history").IsExpanded,
        "Secondary lists should start folded");
    fold.IsExpanded = true;
    Call(view, "RefreshUi", true);
    Assert(ReferenceEquals(fold, Control<Expander>(view, "resident-cognition")) && fold.IsExpanded,
        "Timed refresh rebuilt or collapsed content");
    Call(view, "OpenInspector", "overview", true);
    Call(view, "OpenResident", engine.State.Residents[0].Id);
    Assert(Control<Expander>(view, "resident-cognition").IsExpanded, "Navigation lost fold preference");
    Assert(Control<Button>(view, "resident-goal-edit").IsEnabled, "Goal actions unavailable");
}

static void CloseZoom()
{
    var engine = TwoTownWorld();
    var map = Map(View(engine));
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
    var engine = TwoTownWorld();
    var view = View(engine);
    Call(view, "ShowTileEditor", 12, 12);
    Control<NumericUpDown>(view, "tile-resources").Value = 12.5m;
    Control<NumericUpDown>(view, "tile-fertility").Value = 0;
    Control<NumericUpDown>(view, "tile-road").Value = 3;
    Click(view, "tile-apply");
    var tile = engine.State.Tiles[12 * engine.State.Width + 12];
    Assert(tile.ResourceAmount == 12.5 && tile.Fertility == 0 && tile.RoadLevel == 3,
        "Tile form did not commit values");
    Call(view, "ShowTileEditor", 12, 12);
    var before = engine.ExportJson();
    Control<NumericUpDown>(view, "tile-resources").Value = 50;
    Control<NumericUpDown>(view, "tile-road").Value = 1.5m;
    Click(view, "tile-apply");
    Assert(engine.ExportJson() == before && Field<Border>(view, "_modal").IsVisible,
        "Invalid form applied partially or closed");
}

static void TownDisappears(string mode, bool closeInspector)
{
    var engine = TwoTownWorld();
    var first = engine.State.Settlements[0];
    var survivor = engine.State.Settlements[1];
    var resident = engine.State.Residents.Single(r => r.SettlementId == first.Id);
    resident.Health = .1;
    resident.Age = 91;
    engine.State.Rules.Births = false;
    engine.State.Rules.Hunger = false;
    engine.State.Rules.Aging = true;
    var view = View(engine);
    Call(view, "OpenInspector", mode, true);
    Call(view, "RefreshUi", true);
    var initialContent = Field<ScrollViewer>(view, "_inspectorScroll").Content;
    Call(view, "RefreshUi", true);
    Assert(ReferenceEquals(initialContent, Field<ScrollViewer>(view, "_inspectorScroll").Content),
        "Ordinary refresh must preserve existing controls");
    if (closeInspector) Call(view, "CloseInspector");

    engine.Step();
    Assert(engine.State.Settlements.Count == 1, "The selected town must disappear during simulation");
    Call(view, "RefreshUi", true);
    Assert(Field<int>(view, "_inspectorSettlementId") == survivor.Id, "Inspector must select a surviving town");
    var replacement = Field<ScrollViewer>(view, "_inspectorScroll").Content;
    Call(view, "RefreshUi", true);
    Assert(ReferenceEquals(replacement, Field<ScrollViewer>(view, "_inspectorScroll").Content),
        "Recovery must not cause repeated control rebuilding");

    var last = engine.State.Residents.Single();
    last.Health = .1;
    last.Age = 91;
    engine.Step();
    Assert(engine.State.Settlements.Count == 0, "Last town must also disappear");
    Call(view, "RefreshUi", true);
    Assert(Field<int>(view, "_inspectorSettlementId") == 0, "Empty worlds must clear the selected town");
    var emptyContent = Field<ScrollViewer>(view, "_inspectorScroll").Content;
    Call(view, "RefreshUi", true);
    Assert(ReferenceEquals(emptyContent, Field<ScrollViewer>(view, "_inspectorScroll").Content),
        "Empty state must also retain stable controls");
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
        Assert((after[i].Wood, after[i].Stone, after[i].Ore) == (before[i].Wood, before[i].Stone, before[i].Ore),
            "Untouched resources must retain their local distribution and fractions");
    }
}

static void ResourceValidation()
{
    var engine = TwoTownWorld();
    var id = engine.State.Nations.Single().Id;
    var before = engine.ExportJson();
    engine.SetNationResources(id);
    Assert(engine.ExportJson() == before, "An empty resource patch must not create changes or events");
    try
    {
        engine.SetNationResources(id, 10, double.NaN);
        throw new Exception("Invalid resource was accepted");
    }
    catch (ArgumentOutOfRangeException)
    {
    }

    Assert(engine.ExportJson() == before, "All supplied amounts must be validated before modifying any stock");
}

static void ArchivedIdentity()
{
    var engine = TwoTownWorld();
    var resident = engine.State.Residents[0];
    var homeId = resident.SettlementId;
    resident.Health = .1;
    resident.Age = 91;
    engine.State.Rules.Births = false;
    engine.State.Rules.Aging = true;
    engine.Step();
    var archived = engine.State.ArchivedResidents.Single(person => person.Id == resident.Id);
    // 历史快照可能保留已解散的军队。
    archived.ArmyId = 123456;
    Assert(engine.State.Settlements.All(town => town.Id != homeId), "Fixture needs a vanished home");
    engine.PaintTerrain(archived.X, archived.Y, TerrainType.Water, 0);
    var view = View(engine);
    Call(view, "ShowResidentEditor", archived.Id);
    // 无主题的 Headless 控件不在逻辑树中生成选项卡内容，直接检查传入的身份表单。
    var tabs = Control<TabControl>(view, "resident-editor-tabs");
    var identity = (StackPanel)tabs.ItemsSource!.Cast<TabItem>()
        .Single(tab => AutomationProperties.GetAutomationId(tab) == "resident-tab-identity").Content!;
    identity.Children.OfType<TextBox>().Single(input => AutomationProperties.GetAutomationId(input) == "resident-name")
        .Text = "Remembered resident";
    Click(view, "resident-apply");
    var edited = engine.State.ArchivedResidents.Single(person => person.Id == resident.Id);
    Assert(edited.Name == "Remembered resident", "Historical identity edit must succeed without a living home or army");
    Assert(edited.SettlementId == homeId && edited.ArmyId == 123456,
        "Name-only edits must retain historical references");
    Assert(!Field<Border>(view, "_modal").IsVisible, "Successful archive edit must close the form");
}

static void AutomaticWorkGoal()
{
    var (engine, id, building) = WorkingWorld(Profession.Farmer);
    Assert(engine.GetResident(id)!.Agent.Goal.Kind == AgentGoalKind.Work,
        "The farmer must autonomously choose building work");
    EditPersonalityOnly(engine, id, building.Id);
}

static void ResearchAndMagicGoals()
{
    foreach (var profession in new[] { Profession.Scholar, Profession.Mage })
    {
        var (engine, id, building) = WorkingWorld(profession);
        var kind = profession == Profession.Scholar ? AgentGoalKind.Study : AgentGoalKind.TrainMagic;
        Assert(engine.GetResident(id)!.Agent.Goal.Kind == kind,
            "Specialists must autonomously target the expected work kind");
        var view = EditPersonalityOnly(engine, id, building.Id);
        Click(view, "resident-goal-edit");
        var choices = Control<ComboBox>(view, "resident-goal-entity").ItemsSource!.Cast<object>().Select(ChoiceId)
            .Where(value => value != 0).ToArray();
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
    mind.Goal = new AgentGoal
    {
        Kind = AgentGoalKind.ReturnHome,
        TargetSettlementId = home.Id,
        TargetX = home.X,
        TargetY = home.Y,
        StartedTick = engine.State.Tick,
        ReviewTick = engine.State.Tick + 20,
    };
    engine.EditResident(returning.Id, new ResidentEdit { Agent = mind });
    engine.SpawnResidents(26, 26, RaceKind.Elf, 2);
    foreach (var resident in engine.State.Residents.Where(person => person.SettlementId == home.Id).ToArray())
        engine.EditResident(resident.Id, new ResidentEdit { Age = 91, Health = .1 });
    engine.Tick();
    Assert(engine.State.Settlements.All(town => town.Id != home.Id)
           && engine.State.Society.Buildings.All(item => item.Id != building.Id),
        "The historical targets must disappear normally with their empty town");
    Assert(engine.State.ArchivedResidents.Any(person => person.Id == id)
           && engine.State.ArchivedResidents.Any(person => person.Id == returning.Id),
        "The fixture must retain both deceased residents");
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
    mind.Goal = new AgentGoal
    {
        Kind = AgentGoalKind.DeliverMessage,
        TargetSettlementId = target.Id,
        TargetX = target.X,
        TargetY = target.Y,
        ReviewTick = 100,
        PlayerDirected = true,
    };
    engine.EditResident(courier.Id, new ResidentEdit { X = home.X, Y = home.Y, Agent = mind });
    engine.Tick();
    Assert(engine.GetResident(courier.Id)!.Agent.DestinationSettlementId == target.Id,
        "The courier must have an actual active task");
    EditPersonalityOnly(engine, courier.Id, 0);
}

static void InvalidGoalEdit()
{
    var (engine, id, _) = WorkingWorld(Profession.Farmer);
    var view = View(engine);
    Call(view, "OpenResident", id);
    Click(view, "resident-goal-edit");
    var before = engine.ExportJson();
    Control<NumericUpDown>(view, "resident-goal-duration").Value = 100001;
    Control<NumericUpDown>(view, "resident-courage").Value = .01m;
    Click(view, "resident-goal-apply");
    Assert(Field<Border>(view, "_modal").IsVisible && engine.ExportJson() == before,
        "An invalid goal duration partially committed its accompanying personality edit");
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
    Assert(WorldEngine.ImportJson(saved).ExportJson() == saved,
        "The edited goal or historical reference failed its exact save roundtrip");
    return view;
}

static string MissionSnapshot(Resident person)
{
    return JsonSerializer.Serialize(new
    {
        person.Inventory,
        person.Agent.DestinationSettlementId,
        person.Agent.MissionOriginSettlementId,
        person.Agent.MissionStartedTick,
        person.Agent.MissionRetryTick,
        person.Agent.CarriedMessages,
    });
}

static int ChoiceId(object choice)
{
    return (int)choice.GetType().GetProperty("Id")!.GetValue(choice)!;
}

static (WorldEngine Engine, int ResidentId, Building Target) WorkingWorld(Profession profession)
{
    var engine = WorldEngine.Create(77, 32, 32, false);
    foreach (var tile in engine.State.Tiles)
    {
        tile.Terrain = TerrainType.Grass;
        tile.Fertility = 80;
        tile.Wildlife = WildlifeKind.None;
        tile.WildlifePopulation = 0;
        tile.OtherWildlife = default;
    }

    engine.State.NaturalDisasters = false;
    engine.State.Rules.Thirst = false;
    engine.SpawnResidents(12, 16, RaceKind.Human, 3);
    var home = engine.State.Settlements.Single();
    engine.TransferTerritory(home.X, home.Y, home.NationId, 5);
    engine.SetNationResources(home.NationId, 1000, 1000, 1000, 1000);
    if (profession == Profession.Scholar)
    {
        engine.GrantFacility(home.Id, BuildingKind.Academy, home.X + 2, home.Y);
        engine.StartResearch(home.Id, Advancement.Agriculture);
    }

    if (profession == Profession.Mage) engine.GrantFacility(home.Id, BuildingKind.ArcaneSanctum, home.X + 2, home.Y);
    var id = engine.State.Residents[0].Id;
    engine.EditResident(id, new ResidentEdit
    {
        Profession = profession,
        X = home.X,
        Y = home.Y,
        Inventory = new ResourceStock { Food = 1 },
        MagicTalent = 50,
    });
    engine.Tick();
    var target =
        engine.State.Society.Buildings.Single(building =>
            building.Id == engine.GetResident(id)!.Agent.Goal.TargetEntityId);
    return (engine, id, target);
}

static void ReplaceWorldPlacement()
{
    var big = EmptyWorld(73921, 64);
    big.PaintTerrain(40, 50, TerrainType.Grass);
    var view = View(big);
    var map = Map(view);
    Preview(map, 40, 50);
    map.InfrastructureTownId = 999;
    map.InfrastructureKind = BuildingKind.Housing;
    var small = EmptyWorld(42, 32);
    Call(view, "ReplaceWorld", small);
    Assert(!map.HasPendingPlacement, "Replacing the world must remove old pending placement");
    Assert(map.InfrastructureTownId == 0 && map.InfrastructureKind is null,
        "Old town filters hid buildings in the replacement world");
    Assert(!Field<Border>(view, "_placementBar").IsVisible, "The old confirmation bar must disappear");
    var before = small.ExportJson();
    map.ConfirmPlacement();
    Assert(small.ExportJson() == before, "A late confirmation must not affect the replacement world");

    Preview(map, 10, 10);
    map.InfrastructureTownId = 999;
    map.InfrastructureKind = BuildingKind.Housing;
    Call(view, "RestoreCheckpoint");
    Assert(map.Engine!.State.Width == 64 && !map.HasPendingPlacement,
        "Undoing world replacement must also clear pending placement");
    Assert(map.InfrastructureTownId == 0 && map.InfrastructureKind is null,
        "Undo kept a filter for the discarded world");
}

static void PlacementBounds()
{
    var engine = EmptyWorld(42, 32);
    var map = Map(View(engine));
    map.ActiveTool = MapTool.ForResidents(RaceKind.Human);
    var before = engine.ExportJson();
    foreach (var tile in new[] { (engine.State.Width, 0), (0, engine.State.Height), (-1, 0), (0, -1) })
    {
        // 模拟携带过期坐标的迟到确认，检查输入路径是否拒绝访问。
        typeof(WorldMapControl).GetField("_pendingPlacement", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(map, tile);
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
    Assert(Map(view).PickingLocation && !Field<Border>(view, "_modal").IsVisible,
        "Map picking must temporarily hide its form");
    Call(view, "FinishMapPick", 20, 21);
    Assert(Control<NumericUpDown>(view, "resident-goal-x").Value == 20 && Field<Border>(view, "_modal").IsVisible,
        "Normal picking must restore the form with its selected location");
    Click(view, "map-pick-resident_goal_x");
    Call(view, "ShowNewWorld");
    Assert(!Map(view).PickingLocation, "Opening another modal must abandon the old map picker");
    Call(view, "ReplaceWorld", EmptyWorld(42, 32));
    Call(view, "CloseModal");
    Call(view, "FinishMapPick", 10, 10);
    Assert(!Field<Border>(view, "_modal").IsVisible, "A stale map pick must not reopen an empty modal");
}

static WorldEngine TwoTownWorld(bool largeTotal = false)
{
    var engine = EmptyWorld(42, 32);
    engine.PaintTerrain(12, 12, TerrainType.Grass, 5);
    engine.PaintTerrain(24, 24, TerrainType.Grass, 5);
    engine.SpawnResidents(12, 12, RaceKind.Human, 1);
    engine.SpawnResidents(24, 24, RaceKind.Human, 1);
    var towns = engine.State.Settlements.ToArray();
    Assert(towns.Length == 2, "Fixture requires two settlements");
    towns[0].Resources = new ResourceStock
    {
        Food = largeTotal ? 900_000.25 : 100.2, Wood = 70.125, Stone = 8.3, Ore = 10.4,
    };
    towns[1].Resources = new ResourceStock { Food = largeTotal ? 900_000.5 : .2, Wood = 1.75, Stone = .1, Ore = .375 };
    engine.TransferTerritory(towns[1].X, towns[1].Y, towns[0].NationId, 0);
    foreach (var town in towns) engine.TransferTerritory(town.X, town.Y, town.NationId, 5);
    return engine;
}

static WorldEngine EmptyWorld(int seed, int size)
{
    var engine = WorldEngine.Create(seed, size, size, false);
    // 表单、镜头和世界替换夹具省略动物种群。
    foreach (var tile in engine.State.Tiles)
    {
        tile.Wildlife = WildlifeKind.None;
        tile.WildlifePopulation = 0;
        tile.OtherWildlife = default;
    }

    return engine;
}

static (double Food, double Wood, double Stone, double Ore)[] Stocks(WorldEngine engine)
{
    return engine.State.Settlements
        .Select(town => (town.Resources.Food, town.Resources.Wood, town.Resources.Stone, town.Resources.Ore)).ToArray();
}

static MainView View(WorldEngine engine)
{
    var view = new MainView(engine);
    Call(view, "ReplaceWorld", engine);
    return view;
}

static WorldMapControl Map(MainView view)
{
    var map = Field<WorldMapControl>(view, "_map");
    map.Measure(new Size(800, 800));
    map.Arrange(new Rect(0, 0, 800, 800));
    return map;
}

static void Preview(WorldMapControl map, int x, int y)
{
    map.ActiveTool = MapTool.ForResidents(RaceKind.Human);
    Call(map, "PreviewPlacement", map.GetTileScreenPosition(x, y), true);
    Assert(map.HasPendingPlacement, "Fixture must create a touch preview");
}

static object? Call(object target, string name, params object?[] args)
{
    return target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, args);
}

static T Field<T>(object target, string name)
{
    return (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;
}

static void SetField(object target, string name, object? value)
{
    target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);
}

static T Control<T>(MainView view, string id) where T : Control
{
    return view.GetLogicalDescendants().OfType<T>()
        .Single(control => AutomationProperties.GetAutomationId(control) == id);
}

static void Click(MainView view, string id)
{
    Control<Button>(view, id).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
}

static string ButtonText(Button button)
{
    return button.Content is TextBlock text ? text.Text ?? "" : button.Content?.ToString() ?? "";
}

static void AwaitUi(Func<bool> completed, string message)
{
    var clock = Stopwatch.StartNew();
    while (!completed() && clock.Elapsed.TotalSeconds < 2)
    {
        Dispatcher.UIThread.RunJobs();
        Thread.Sleep(1);
    }

    Dispatcher.UIThread.RunJobs();
    Assert(completed(), message);
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

/// <summary>Headless 测试应用。</summary>
public sealed class TestApp : Application;

/// <summary>记录分块保存调用的测试存储，可模拟写入失败。</summary>
public sealed class CountingSaveStorage : IWorldStorage
{
    /// <summary>已经尝试的分块保存次数，包含失败调用。</summary>
    public int Calls { get; private set; }

    /// <summary>最近一次保存收到的文本块数量。</summary>
    public int Chunks { get; private set; }

    /// <summary>是否让下一次分块保存抛出写入异常，触发后自动清除。</summary>
    public bool FailNext { get; set; }

    /// <summary>测试中始终将应用视为前台。</summary>
    public bool IsBackground => false;

    /// <summary>拒绝单字符串保存，确保被测界面使用分块路径。</summary>
    /// <param name="json">世界 JSON。</param>
    public Task SaveAsync(string json)
    {
        throw new Exception("Autosave used the monolithic path");
    }

    /// <summary>记录文本块数量，并按设置模拟下一次保存失败。</summary>
    /// <param name="chunks">按顺序传入的世界 JSON 文本块。</param>
    public Task SaveChunksAsync(string[] chunks)
    {
        Calls++;
        Chunks = chunks.Length;
        if (FailNext)
        {
            FailNext = false;
            throw new IOException("Write failed");
        }

        return Task.CompletedTask;
    }

    /// <summary>没有本地自动存档。</summary>
    public Task<string?> LoadAsync()
    {
        return Task.FromResult<string?>(null);
    }

    /// <summary>导出立即完成。</summary>
    /// <param name="json">世界 JSON。</param>
    /// <param name="fileName">导出文件名。</param>
    public Task ExportAsync(string json, string fileName)
    {
        return Task.CompletedTask;
    }

    /// <summary>模拟取消文件导入。</summary>
    public Task<string?> ImportAsync()
    {
        return Task.FromResult<string?>(null);
    }
}

/// <summary>由测试控制导出完成时机的存储。</summary>
public sealed class DeferredExportStorage : IWorldStorage
{
    private readonly TaskCompletionSource _export = new();

    /// <summary>导出请求次数。</summary>
    public int ExportCalls { get; private set; }

    /// <summary>最近一次导出请求收到的世界 JSON。</summary>
    public string? ExportedJson { get; private set; }

    /// <summary>测试中始终将应用视为前台。</summary>
    public bool IsBackground => false;

    /// <summary>保存立即完成。</summary>
    /// <param name="json">世界 JSON。</param>
    public Task SaveAsync(string json)
    {
        return Task.CompletedTask;
    }

    /// <summary>没有本地自动存档。</summary>
    public Task<string?> LoadAsync()
    {
        return Task.FromResult<string?>(null);
    }

    /// <summary>模拟取消文件导入。</summary>
    public Task<string?> ImportAsync()
    {
        return Task.FromResult<string?>(null);
    }

    /// <summary>记录导出内容，返回等待测试显式完成的任务。</summary>
    /// <param name="json">世界 JSON。</param>
    /// <param name="fileName">导出文件名。</param>
    public Task ExportAsync(string json, string fileName)
    {
        ExportCalls++;
        ExportedJson = json;
        return _export.Task;
    }

    /// <summary>完成尚在等待的导出任务。</summary>
    public void CompleteExport()
    {
        _export.TrySetResult();
    }
}
