using SeWZC.WorldBox.Core.Runtime;

namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    // 行动通知仅用于呈现并限制数量，不能作为模拟输入。
    private readonly Queue<WorldVisual> _visuals = new();

    /// <summary>最近发出的地图动画通知序号。</summary>
    public long VisualSequence { get; private set; }

    /// <summary>枚举仍在有限通知队列中、序号晚于指定值的地图动画通知。</summary>
    /// <param name="sequence">调用方已经处理的最后一个通知序号。</param>
    public IEnumerable<WorldVisual> GetVisualsAfter(long sequence)
    {
        return _visuals.Where(v => v.Sequence > sequence);
    }

    private void EmitVisual(WorldVisualKind kind, int x, int y, int radius = 1, int fromX = -1, int fromY = -1)
    {
        _visuals.Enqueue(new WorldVisual(++VisualSequence, kind, x, y, radius, fromX, fromY));
        while (_visuals.Count > 256)
            _visuals.Dequeue();
    }

    private void FinishLogging(TileCursor tile, int x, int y)
    {
        if (!IsForestTerrain(tile.Terrain) || tile.ResourceAmount > .000001)
            return;
        tile.Terrain = TerrainType.Grass;
        tile.ResourceAmount = 0;
        tile.Fertility = TerrainRules.Fertility(TerrainType.Grass);
        EmitVisual(WorldVisualKind.Logging, x, y);
    }

    /// <summary>使用实际局部导航预览当前目标的路线，不改变世界或消耗随机数。</summary>
    /// <param name="residentId">居民 ID。</param>
    /// <param name="steps">最多预览的步数，计算时限制在 0 至 64。</param>
    public IReadOnlyList<RoutePoint> PreviewResidentRoute(int residentId, int steps = 24)
    {
        var person = Current.Residents.FirstOrDefault(r => r.Id == residentId);
        if (person is null || person.ArmyId != 0 || person.Agent.Goal.Kind == AgentGoalKind.Idle)
            return [];
        var goal = person.Agent.Goal;
        if (!InBounds(goal.TargetX, goal.TargetY))
            return [];
        var targetX = goal.TargetX;
        var targetY = goal.TargetY;
        var factory = goal.Kind == AgentGoalKind.Work
            ? Current.Buildings.FirstOrDefault(b =>
                b.Value.Id == goal.TargetEntityId && b.Value.SettlementId == person.SettlementId && b.Value.IsCompleted)
            : null;
        var production = factory is null ? null : ProductionRules.For(factory.Value.Kind);
        if (production is not null && _settlements.TryGetValue(person.SettlementId, out var home))
        {
            var needsInputs = MissingResources(person.Inventory, production.Input) is not null;
            targetX = needsInputs ? home.X : factory!.Value.X;
            targetY = needsInputs ? home.Y : factory!.Value.Y;
        }

        var cursor = new ResidentCursor(person.Value);
        var route = new List<RoutePoint> { new(cursor.X, cursor.Y) };
        var visited = new HashSet<int> { Index(cursor.X, cursor.Y) };
        for (var i = 0; i < Math.Clamp(steps, 0, 64); i++)
        {
            var interactionRange = production is not null || goal.Kind is AgentGoalKind.Eat or AgentGoalKind.Rest
                                                              or AgentGoalKind.Socialize or AgentGoalKind.ReturnHome
                                                              or AgentGoalKind.Sleep
                                                          || (goal.TargetEntityId != 0 &&
                                                              Current.Buildings.Any(b =>
                                                                  b.Value.Id == goal.TargetEntityId &&
                                                                  (!b.Value.IsCompleted || b.Value.IsUpgrading ||
                                                                   IsWaterfrontBuilding(b.Value.Kind))))
                ? 1
                : 0;
            if (Distance(cursor.X, cursor.Y, targetX, targetY) <= interactionRange &&
                Walkable(cursor.X, cursor.Y, cursor.Race))
                break;
            var next = SelectAgentStep(cursor, targetX, targetY, out var navigation);
            cursor.Agent = cursor.Agent.WithGoal(navigation);
            if (next < 0 || !visited.Add(next))
                break;
            cursor.Replace(cursor.Value with
            {
                FromX = cursor.X, FromY = cursor.Y, X = next % Current.Width, Y = next / Current.Width,
            });
            route.Add(new RoutePoint(cursor.X, cursor.Y));
        }

        return route;
    }

    /// <summary>先校验所有输入，再应用玩家对地格资源、肥力和道路的直接编辑。</summary>
    /// <param name="x">横向地格坐标。</param>
    /// <param name="y">纵向地格坐标。</param>
    /// <param name="resources">新的共享自然资源存量，范围为 0 至 1,000,000。</param>
    /// <param name="fertility">新的肥力，范围为 0 至 100。</param>
    /// <param name="roadLevel">新的道路等级，范围为 0 至 3；有道路时地格须可通行。</param>
    public void EditTile(int x, int y, double resources, int fertility, int roadLevel)
    {
        if (!InBounds(x, y))
            throw new ArgumentException("地格不存在。");
        if (!double.IsFinite(resources) || resources is < 0 or > 1_000_000 || fertility is < 0 or > 100 ||
            roadLevel is < 0 or > 3)
            throw new ArgumentException("地格资源、肥力或道路等级超出范围。");
        var tile = Current.Tiles[Index(x, y)];
        if (!tile.IsWalkable && roadLevel > 0)
            throw new ArgumentException("道路需要可通行的陆地。");
        tile.Replace(tile.Value with
        {
            ResourceAmount = resources, Fertility = (byte)fertility, RoadLevel = (byte)roadLevel,
        });
        AddEvent(WorldEventKind.Editor, "玩家调整当地资源、肥力与道路。", x, y);
    }
}
