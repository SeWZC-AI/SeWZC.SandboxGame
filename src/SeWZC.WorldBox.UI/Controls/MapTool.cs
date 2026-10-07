using System.Diagnostics.CodeAnalysis;
using SeWZC.WorldBox.Core;

namespace SeWZC.WorldBox.UI.Controls;

/// <summary>一种地图交互工具的定义。</summary>
public abstract record MapTool
{
    private static readonly IReadOnlyDictionary<TerrainType, MapTool> TerrainTools =
        Enum.GetValues<TerrainType>().ToDictionary(k => k, k => (MapTool)new TerrainTool { Terrain = k });

    private static readonly IReadOnlyDictionary<RaceKind, MapTool> ResidentTools =
        Enum.GetValues<RaceKind>().ToDictionary(k => k, k => (MapTool)new ResidentTool { Race = k });

    private static readonly IReadOnlyDictionary<DisasterKind, MapTool> DisasterTools =
        Enum.GetValues<DisasterKind>().ToDictionary(k => k, k => (MapTool)new DisasterTool { Disaster = k });

    private static readonly IReadOnlyDictionary<BuildingKind, MapTool> BuildingTools =
        Enum.GetValues<BuildingKind>().ToDictionary(k => k, k => k == BuildingKind.Bridge
            ? (MapTool)new BridgeTool()
            : new BuildingTool { Building = k });

    /// <summary>选择并查看地图对象的工具。</summary>
    public static MapTool Inspect { get; } = new NavigationTool("inspect");

    /// <summary>平移地图镜头的工具。</summary>
    public static MapTool Pan { get; } = new NavigationTool("pan");

    /// <summary>修改国家领土归属的工具。</summary>
    public static MapTool Territory { get; } = new TerritoryTool();

    /// <summary>建设道路的工具。</summary>
    public static MapTool Road { get; } = new RoadTool();

    /// <summary>建设铁路的工具。</summary>
    public static MapTool Rail { get; } = new RailTool();

    /// <summary>工具的稳定标识。</summary>
    public abstract string Id { get; }

    /// <summary>是否只操作镜头与选择而不修改世界。</summary>
    public virtual bool IsNavigation => false;

    /// <summary>触屏放置时是否需要先预览再确认。</summary>
    public virtual bool RequiresTouchConfirmation => false;

    /// <summary>是否使用方形范围显示工具预览。</summary>
    public virtual bool SquarePreview => false;

    /// <summary>返回绘制指定地形的工具。</summary>
    public static MapTool ForTerrain(TerrainType terrain)
    {
        return TerrainTools[terrain];
    }

    /// <summary>返回投放指定种族居民的工具。</summary>
    public static MapTool ForResidents(RaceKind race)
    {
        return ResidentTools[race];
    }

    /// <summary>返回施加指定灾害的工具。</summary>
    public static MapTool ForDisaster(DisasterKind disaster)
    {
        return DisasterTools[disaster];
    }

    /// <summary>返回建设指定建筑的工具。</summary>
    public static MapTool ForBuilding(BuildingKind building)
    {
        return BuildingTools[building];
    }

    /// <summary>计算工具当前预览半径，以地格为单位。</summary>
    public virtual int PreviewRadius(WorldMapControl map)
    {
        return Math.Clamp(map.BrushRadius, 0, 16);
    }

    /// <summary>返回工具当前参数的中文说明。</summary>
    public virtual string Describe(WorldMapControl map)
    {
        return $"绘制范围：{map.BrushRadius} 格";
    }

    /// <summary>检查指定位置的放置条件；允许操作时返回空值，否则返回原因。</summary>
    public virtual string? PlacementError(WorldMapControl map, int x, int y)
    {
        return null;
    }

    internal abstract bool Apply(WorldMapControl map, (int X, int Y) tile);

    private sealed record NavigationTool(string Key) : MapTool
    {
        public override string Id => Key;
        public override bool IsNavigation => true;

        internal override bool Apply(WorldMapControl map, (int X, int Y) tile)
        {
            return false;
        }
    }

    private sealed record TerrainTool : MapTool
    {
        public required TerrainType Terrain { get; init; }
        public override string Id => Terrain.ToString();

        internal override bool Apply(WorldMapControl map, (int X, int Y) tile)
        {
            return map.PaintWithTool(tile,
                (x, y) => map.Engine!.PaintTerrain(x, y, Terrain, Math.Clamp(map.BrushRadius, 0, 16)));
        }
    }

    private sealed record TerritoryTool : MapTool
    {
        public override string Id => "territory";

        internal override bool Apply(WorldMapControl map, (int X, int Y) tile)
        {
            return map.Engine!.State.Nations.Any(n => n.Id == map.SelectedNationId) &&
                   map.PaintWithTool(tile, (x, y) => map.Engine.TransferTerritory(x, y, map.SelectedNationId,
                       Math.Clamp(map.BrushRadius, 0, 16)));
        }
    }

    private sealed record ResidentTool : MapTool
    {
        public required RaceKind Race { get; init; }
        public override string Id => Race.ToString();
        public override bool RequiresTouchConfirmation => true;

        public override int PreviewRadius(WorldMapControl map)
        {
            return 3;
        }

        public override string Describe(WorldMapControl map)
        {
            return $"投放 {map.SpawnCount} 位居民";
        }

        public override string? PlacementError(WorldMapControl map, int x, int y)
        {
            return map.Engine!.State.Tiles[y * map.Engine.State.Width + x].IsWalkable ? null : "居民需要可通行的陆地";
        }

        internal override bool Apply(WorldMapControl map, (int X, int Y) tile)
        {
            return map.PlaceWithTool(() => map.Engine!.SpawnResidents(tile.X, tile.Y, Race, map.SpawnCount));
        }
    }

    private sealed record DisasterTool : MapTool
    {
        public required DisasterKind Disaster { get; init; }
        public override string Id => Disaster.ToString();
        public override bool RequiresTouchConfirmation => true;

        public override int PreviewRadius(WorldMapControl map)
        {
            return map.DisasterRadius;
        }

        public override string Describe(WorldMapControl map)
        {
            return $"单次释放\n范围：{map.DisasterRadius} 格";
        }

        public override string? PlacementError(WorldMapControl map, int x, int y)
        {
            var engine = map.Engine!;
            if (!engine.State.Tiles[y * engine.State.Width + x].IsWalkable)
                return "请选择陆地上的灾害落点";
            return Disaster == DisasterKind.Plague &&
                   !engine.State.Residents.Any(r => Math.Abs(r.X - x) + Math.Abs(r.Y - y) <= map.DisasterRadius)
                ? "作用范围内没有居民"
                : null;
        }

        internal override bool Apply(WorldMapControl map, (int X, int Y) tile)
        {
            return map.PlaceWithTool(() => map.Engine!.TriggerDisaster(tile.X, tile.Y, Disaster, map.DisasterRadius));
        }
    }

    private abstract record ConstructionTool : MapTool
    {
        protected abstract bool PaintsStroke { get; }
        protected abstract void Construct(WorldMapControl map, int x, int y);

        internal override bool Apply(WorldMapControl map, (int X, int Y) tile)
        {
            return map.ConstructWithTool(tile, PaintsStroke, (x, y) => Construct(map, x, y));
        }
    }

    private record BuildingTool : ConstructionTool
    {
        public required BuildingKind Building { get; init; }
        public override string Id => "build:" + Building;
        protected override bool PaintsStroke => false;
        public override bool RequiresTouchConfirmation => true;
        public override bool SquarePreview => true;

        protected virtual BridgeDirection? Direction(WorldMapControl map)
        {
            return null;
        }

        protected virtual int Level(WorldMapControl map)
        {
            return 1;
        }

        public override string Describe(WorldMapControl map)
        {
            return map.GiftBuildings
                ? "建造方式：直接赐予\n无材料消耗，效果仍受建筑运营条件限制"
                : "建造方式：居民施工\n扣除当地材料后开工";
        }

        public override string? PlacementError(WorldMapControl map, int x, int y)
        {
            return map.Engine!.FacilityPlacementError(map.SelectedSettlementId, Building, x, y, map.GiftBuildings,
                Direction(map), Level(map));
        }

        protected override void Construct(WorldMapControl map, int x, int y)
        {
            if (map.GiftBuildings)
                map.Engine!.GrantFacility(map.SelectedSettlementId, Building, x, y, Direction(map), Level(map));
            else
                map.Engine!.BuildFacility(map.SelectedSettlementId, Building, x, y, Direction(map), Level(map));
        }
    }

    private sealed record BridgeTool : BuildingTool
    {
        [SetsRequiredMembers]
        public BridgeTool()
        {
            Building = BuildingKind.Bridge;
        }

        protected override BridgeDirection? Direction(WorldMapControl map)
        {
            return map.ConstructionBridgeDirection;
        }

        protected override int Level(WorldMapControl map)
        {
            return map.ConstructionBridgeLevel;
        }

        public override string Describe(WorldMapControl map)
        {
            var cost = WorldEngine.FacilityCost(Building, Level(map));
            return base.Describe(map) +
                   $"\n方向：{WorldEngine.BridgeDirectionName(map.ConstructionBridgeDirection)}\n等级 {Level(map)}   离自然岸最多 {WorldEngine.BridgeShoreLimit(Level(map))} 格\n材料：木材 {cost.Wood:0.#}   石材 {cost.Stone:0.#}";
        }
    }

    private sealed record RoadTool : ConstructionTool
    {
        public override string Id => "road:Road";
        protected override bool PaintsStroke => true;

        public override int PreviewRadius(WorldMapControl map)
        {
            return 0;
        }

        public override string Describe(WorldMapControl map)
        {
            return "修建道路\n每格木材：0.5\n每格石材：1";
        }

        public override string? PlacementError(WorldMapControl map, int x, int y)
        {
            return map.Engine!.RoadPlacementError(map.SelectedSettlementId, x, y);
        }

        protected override void Construct(WorldMapControl map, int x, int y)
        {
            map.Engine!.BuildRoad(map.SelectedSettlementId, x, y, 0);
        }
    }

    private sealed record RailTool : ConstructionTool
    {
        public override string Id => "road:Rail";
        protected override bool PaintsStroke => true;

        public override int PreviewRadius(WorldMapControl map)
        {
            return 0;
        }

        public override string Describe(WorldMapControl map)
        {
            return "铺设铁路\n每格石材：1\n每格合金：0.5";
        }

        public override string? PlacementError(WorldMapControl map, int x, int y)
        {
            return map.Engine!.RailPlacementError(map.SelectedSettlementId, x, y);
        }

        protected override void Construct(WorldMapControl map, int x, int y)
        {
            map.Engine!.BuildRail(map.SelectedSettlementId, x, y, 0);
        }
    }
}
