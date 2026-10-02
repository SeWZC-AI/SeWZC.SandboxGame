using System.Reflection;
using SeWZC.WorldBox.Core;

internal static class PresentationWorldTests
{
    public static readonly (string Name, Action Run)[] Cases =
    [
        ("logging changes terrain only after real resource depletion", Logging),
        ("meteor damages the actual world and survives save restore", Meteor),
        ("route preview is read only and shares actual movement decisions", Route),
        ("expanded names are deterministic and distinct across residents and towns", Names),
        ("ecology rules and tile editing validate atomically and persist zero values", Editing),
        ("construction completion clears its forest site through local work", Construction)
    ];
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static WorldEngine World()
    {
        var engine = WorldEngine.Create(481, 64, 64, false);
        foreach (var tile in engine.State.Tiles) { tile.Terrain = TerrainType.Grass; tile.Fertility = 80; }
        engine.State.NaturalDisasters = false;
        engine.SpawnResidents(16, 16, RaceKind.Human, 4);
        return engine;
    }
    private static object? Invoke(WorldEngine engine, string name, params object[] args) =>
        typeof(WorldEngine).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(engine, args);
    private static void Logging()
    {
        var engine = World(); var resident = engine.State.Residents[0];
        resident.X = 22; resident.Y = 22; resident.Profession = Profession.Lumberjack;
        var tile = engine.State.Tiles[22 * 64 + 22]; tile.Terrain = TerrainType.Forest; tile.ResourceAmount = .1;
        var wood = resident.Inventory.Wood;
        Invoke(engine, "GatherActualResources", resident, Profession.Lumberjack);
        Check(tile.Terrain == TerrainType.Grass && tile.ResourceAmount == 0, "Exhausted logging did not clear forest");
        Check(Math.Abs(resident.Inventory.Wood - wood - .1) < .00001, "Harvest created or lost wood");
        Check(engine.GetVisualsAfter(0).Any(v => v.Kind == WorldVisualKind.Logging), "No real logging notification");
        var restored = WorldEngine.ImportJson(engine.ExportJson());
        Check(restored.State.Tiles[22 * 64 + 22].Terrain == TerrainType.Grass, "Clearing not saved");
    }
    private static void Meteor()
    {
        var engine = World(); var town = engine.State.Settlements[0]; var resident = engine.State.Residents[0];
        resident.X = town.X; resident.Y = town.Y;
        var tile = engine.State.Tiles[town.Y * 64 + town.X]; tile.RoadLevel = 3; tile.Terrain = TerrainType.Forest;
        var health = resident.Health;
        engine.TriggerDisaster(town.X, town.Y, DisasterKind.Meteor, 3);
        Check(tile.Terrain == TerrainType.Sand && tile.RoadLevel == 0 && tile.ResourceAmount == 0 && tile.FireTicks > 0, "Meteor missed terrain");
        Check(resident.Health < health && engine.State.Society.Buildings.Any(b => b.Health < 100), "Meteor missed entities");
        Check(engine.GetVisualsAfter(0).Last().Kind == WorldVisualKind.Meteor, "Meteor notification missing");
        var saved = engine.ExportJson(); var restored = WorldEngine.ImportJson(saved);
        Check(restored.ExportJson() == saved, "Meteor state failed roundtrip");
        engine.Step(20); restored.Step(20); Check(engine.ExportJson() == restored.ExportJson(), "Meteor continuation diverged");
    }
    private static void Route()
    {
        var engine = World(); var person = engine.State.Residents[0];
        person.X = person.FromX = 16; person.Y = person.FromY = 16;
        person.Agent.Goal = new() { Kind = AgentGoalKind.Work, TargetX = 24, TargetY = 16 };
        engine.PaintTerrain(18, 16, TerrainType.Water, 1);
        var before = engine.ExportJson();
        var route = engine.PreviewResidentRoute(person.Id);
        Check(route.Count > 2 && route.Skip(1).All(p => engine.State.Tiles[p.Y * 64 + p.X].IsWalkable), "Route crosses impassable terrain");
        Check(engine.ExportJson() == before, "Preview mutated world or random state");
        foreach (var expected in route.Skip(1))
        {
            engine.State.Tick += 8;
            Check((bool)Invoke(engine, "MoveAgentTowards", person, 24, 16)!, "Preview promised an impossible step");
            Check(person.X == expected.X && person.Y == expected.Y, "Actual movement differs from preview");
        }
    }
    private static void Names()
    {
        var engine = World(); var second = World();
        engine.SpawnResidents(16, 16, RaceKind.Human, 200); second.SpawnResidents(16, 16, RaceKind.Human, 200);
        Check(engine.State.Residents.Select(r => r.Name).Distinct().Count() == engine.State.Residents.Count, "Duplicate generated resident names");
        Check(engine.ExportJson() == second.ExportJson(), "Name generation is not deterministic");
        for (var y = 8; y < 64; y += 16) for (var x = 8; x < 64; x += 16) engine.SpawnResidents(x, y, RaceKind.Elf, 1);
        Check(engine.State.Nations.Select(n => n.Name).Distinct().Count() == engine.State.Nations.Count, "Duplicate country names");
        Check(engine.State.Settlements.Select(t => t.Name).Distinct().Count() == engine.State.Settlements.Count, "Duplicate town names");
    }
    private static void Editing()
    {
        var engine = World();
        engine.ConfigureWorld(engine.State.Rules with { ResourceRegeneration = false, FireSpread = false, GatheringRate = .25, CombatDamageRate = 3 }, false, false);
        engine.EditTile(22, 22, 0, 0, 0);
        var before = engine.ExportJson();
        try { engine.EditTile(22, 22, double.NaN, 90, 2); throw new InvalidOperationException("NaN accepted"); } catch (ArgumentException) { }
        try { engine.ConfigureWorld(engine.State.Rules with { CombatDamageRate = double.PositiveInfinity }, true, true); throw new InvalidOperationException("Invalid rules accepted"); } catch (ArgumentException) { }
        Check(engine.ExportJson() == before, "Rejected edit partially mutated world");
        var restored = WorldEngine.ImportJson(before);
        Check(restored.State.Rules == engine.State.Rules && restored.State.Tiles[22 * 64 + 22].Fertility == 0, "Rules/zero values not persisted");
        engine.Step(64); restored.Step(64); Check(engine.ExportJson() == restored.ExportJson(), "New rules lost deterministic continuation");
        Check(engine.State.Tiles[22 * 64 + 22].ResourceAmount == 0, "Disabled regeneration still refilled resources");
        engine.EditTile(40, 40, 5000, 80, 0); engine.State.Rules.ResourceRegeneration = true;
        engine.Step(40);
        Check(engine.State.Tiles[40 * 64 + 40].ResourceAmount == 5000, "Regeneration erased player-supplied resources");
    }
    private static void Construction()
    {
        var engine = World(); var town = engine.State.Settlements[0];
        town.Resources.Wood = town.Resources.Stone = 100;
        engine.PaintTerrain(19, 16, TerrainType.Forest, 0);
        var id = engine.BuildFacility(town.Id, BuildingKind.Workshop, 19, 16);
        var b = engine.State.Society.Buildings.Single(b => b.Id == id);
        var worker = engine.State.Residents[0]; worker.X = 19; worker.Y = 16; worker.Profession = Profession.Builder;
        worker.Agent.Goal = new() { Kind = AgentGoalKind.Work, TargetEntityId = id, TargetX = 19, TargetY = 16 };
        for (var i = 0; i < 65 && !b.IsCompleted; i++) { engine.State.Tick++; engine.TryWorkAtBuilding(worker); }
        Check(b.IsCompleted && engine.State.Tiles[16 * 64 + 19].Terrain == TerrainType.Grass, "Real construction did not clear site");
    }
}
