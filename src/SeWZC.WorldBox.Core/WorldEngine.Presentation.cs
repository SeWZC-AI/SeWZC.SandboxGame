namespace SeWZC.WorldBox.Core;

public enum WorldVisualKind
{
    Fire,
    Drought,
    Plague,
    Meteor,
    Battle,
    Heal,
    Harvest,
    Shield,
    Ember,
    Logging,
    Construction,
    Frost,
    Lightning,
    Rain,
    Waygate,
}

public readonly record struct WorldVisual(
    long Sequence,
    WorldVisualKind Kind,
    int X,
    int Y,
    int Radius,
    int FromX,
    int FromY);

public readonly record struct RoutePoint(int X, int Y);

public sealed partial class WorldEngine
{
    // Bounded notifications of actual actions. These are presentation data, never simulation inputs.
    private readonly Queue<WorldVisual> _visuals = new();
    public long VisualSequence { get; private set; }

    public IEnumerable<WorldVisual> GetVisualsAfter(long sequence)
    {
        return _visuals.Where(v => v.Sequence > sequence);
    }

    private void EmitVisual(WorldVisualKind kind, int x, int y, int radius = 1, int fromX = -1, int fromY = -1)
    {
        _visuals.Enqueue(new WorldVisual(++VisualSequence, kind, x, y, radius, fromX, fromY));
        while (_visuals.Count > 256) _visuals.Dequeue();
    }

    private void FinishLogging(Tile tile, int x, int y)
    {
        if (!IsForestTerrain(tile.Terrain) || tile.ResourceAmount > .000001) return;
        tile.Terrain = TerrainType.Grass;
        tile.ResourceAmount = 0;
        tile.Fertility = TerrainRules.Fertility(TerrainType.Grass);
        EmitVisual(WorldVisualKind.Logging, x, y);
    }

    /// <summary>Forecast only the current goal using the real local navigator; no world mutation or random draws.</summary>
    public IReadOnlyList<RoutePoint> PreviewResidentRoute(int residentId, int steps = 24)
    {
        var person = State.Residents.FirstOrDefault(r => r.Id == residentId);
        if (person is null || person.ArmyId != 0 || person.Agent.Goal.Kind == AgentGoalKind.Idle) return [];
        var goal = person.Agent.Goal;
        if (!InBounds(goal.TargetX, goal.TargetY)) return [];
        var targetX = goal.TargetX;
        var targetY = goal.TargetY;
        var factory = goal.Kind == AgentGoalKind.Work
            ? State.Society.Buildings.FirstOrDefault(b =>
                b.Id == goal.TargetEntityId && b.SettlementId == person.SettlementId && b.IsCompleted)
            : null;
        var production = factory is null ? null : AdvancementRules.For(factory.Kind);
        if (production is not null && _settlements.TryGetValue(person.SettlementId, out var home))
        {
            var needsInputs = MissingResources(person.Inventory, production.Input) is not null;
            targetX = needsInputs ? home.X : factory!.X;
            targetY = needsInputs ? home.Y : factory!.Y;
        }

        var cursor = new Resident
            { X = person.X, Y = person.Y, FromX = person.FromX, FromY = person.FromY, TravelMode = person.TravelMode };
        cursor.Agent.Goal.NavigationTarget = goal.NavigationTarget;
        cursor.Agent.Goal.NavigationVisited = new List<int>(goal.NavigationVisited);
        cursor.Agent.Goal.NavigationBestDistance = goal.NavigationBestDistance;
        cursor.Agent.Goal.NavigationWithoutProgress = goal.NavigationWithoutProgress;
        var route = new List<RoutePoint> { new(cursor.X, cursor.Y) };
        var visited = new HashSet<int> { Index(cursor.X, cursor.Y) };
        for (var i = 0; i < Math.Clamp(steps, 0, 64); i++)
        {
            var interactionRange = production is not null || goal.Kind is AgentGoalKind.Eat or AgentGoalKind.Rest
                                                              or AgentGoalKind.ReturnHome or AgentGoalKind.Socialize
                                                          || (goal.TargetEntityId != 0 &&
                                                              State.Society.Buildings.Any(b =>
                                                                  b.Id == goal.TargetEntityId &&
                                                                  (!b.IsCompleted || b.IsUpgrading ||
                                                                   IsWaterfrontBuilding(b.Kind))))
                ? 1
                : 0;
            if (Distance(cursor.X, cursor.Y, targetX, targetY) <= interactionRange &&
                Walkable(cursor.X, cursor.Y)) break;
            var next = SelectAgentStep(cursor, targetX, targetY);
            if (next < 0 || !visited.Add(next)) break;
            cursor.FromX = cursor.X;
            cursor.FromY = cursor.Y;
            cursor.X = next % State.Width;
            cursor.Y = next / State.Width;
            route.Add(new RoutePoint(cursor.X, cursor.Y));
        }

        return route;
    }

    /// <summary>Explicit god edit, fully checked before any field is changed.</summary>
    public void EditTile(int x, int y, double resources, int fertility, int roadLevel)
    {
        if (!InBounds(x, y)) throw new ArgumentException("地格不存在。");
        if (!double.IsFinite(resources) || resources is < 0 or > 1_000_000 || fertility is < 0 or > 100 ||
            roadLevel is < 0 or > 3)
            throw new ArgumentException("地格资源、肥力或道路等级超出范围。");
        var tile = State.Tiles[Index(x, y)];
        if (!tile.IsWalkable && roadLevel > 0) throw new ArgumentException("道路需要可通行的陆地。");
        tile.ResourceAmount = resources;
        tile.Fertility = (byte)fertility;
        tile.RoadLevel = (byte)roadLevel;
        AddEvent(WorldEventKind.Editor, "玩家调整当地资源、肥力与道路。", x, y);
    }
}
