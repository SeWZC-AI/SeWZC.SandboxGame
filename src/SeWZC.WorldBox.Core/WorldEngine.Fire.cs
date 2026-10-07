namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    /// <summary>根据地形燃料、资源和供水计算 0 至 1 的可燃性。</summary>
    /// <param name="tile">要评估可燃性的地格。</param>
    public static double TerrainFlammability(Tile tile)
    {
        var fuel = tile.Terrain switch
        {
            TerrainType.Grass or TerrainType.DryFertile or TerrainType.Meadow or TerrainType.Savanna
                or TerrainType.Scrub or TerrainType.Floodplain
                or TerrainType.AlpineMeadow => .3 * Math.Clamp(tile.ResourceAmount / 25, 0, 1),
            TerrainType.Forest or TerrainType.Woodland or TerrainType.Rainforest => .8 *
                Math.Clamp(tile.ResourceAmount / 25, 0, 1),
            TerrainType.Hills or TerrainType.Tundra => .15 * Math.Clamp(tile.ResourceAmount / 25, 0, 1),
            TerrainType.Wetland when tile.DroughtTicks > 0 => .12,
            _ => 0,
        };
        // 可通行不代表可燃，湿地须按实际燃料和供水判断点火条件。
        return Math.Clamp(fuel * (tile.DroughtTicks > 0 ? 1.5 : Math.Exp(-tile.NaturalWaterYield * 40)), 0, 1);
    }

    /// <summary>根据建筑用途和等级计算可燃性。</summary>
    /// <param name="building">要评估可燃性的建筑。</param>
    public static double BuildingFlammability(Building building)
    {
        return building.Kind switch
        {
            BuildingKind.Farm or BuildingKind.Waystation or BuildingKind.Bridge or BuildingKind.Dock
                or BuildingKind.Shipyard or BuildingKind.LumberCamp or BuildingKind.Granary or BuildingKind.Housing
                or BuildingKind.Market or BuildingKind.Watchtower => .8,
            BuildingKind.Workshop or BuildingKind.TownCenter => .6,
            BuildingKind.Academy or BuildingKind.Infirmary or BuildingKind.RunicGarden => .35,
            BuildingKind.MountainPass => 0,
            BuildingKind.Quarry or BuildingKind.Well => .15,
            _ => .15,
        } * Math.Pow(.75, building.Level - 1);
    }

    /// <summary>取地形与存活建筑中的最大可燃性，地点越界时返回零。</summary>
    /// <param name="x">横向地格坐标。</param>
    /// <param name="y">纵向地格坐标。</param>
    public double GetTileFlammability(int x, int y)
    {
        if (!InBounds(x, y)) return 0;
        var fuel = TerrainFlammability(State.Tiles[Index(x, y)]);
        foreach (var building in State.Society.Buildings)
            if (building.X == x && building.Y == y && building.Health > 0)
                fuel = Math.Max(fuel, BuildingFlammability(building));
        return fuel;
    }

    private bool Ignite(int index)
    {
        var tile = State.Tiles[index];
        if (tile.FireTicks > 0 || GetTileFlammability(index % State.Width, index / State.Width) <= 0) return false;
        tile.FireTicks = 60 + RandomInt(31);
        _burningTiles.Add(index);
        EmitVisual(WorldVisualKind.Fire, index % State.Width, index / State.Width);
        return true;
    }

    private void EndFire(int index, bool exhausted)
    {
        var tile = State.Tiles[index];
        tile.FireTicks = 0;
        _burningTiles.Remove(index);
        if (!exhausted || TerrainFlammability(tile) <= 0) return;
        if (IsForestTerrain(tile.Terrain)) tile.Terrain = TerrainType.Grass;
        tile.Plants = default;
        tile.ResourceAmount *= .25;
        tile.Fertility = (byte)Math.Max(5, tile.Fertility - 10);
    }

    /// <summary>尝试让邻近火源的成年居民消耗随身饮水扑救；返回是否产生扑救效果。</summary>
    /// <param name="person">扑火的居民。</param>
    public bool TryExtinguishFire(Resident person)
    {
        var goal = person.Agent.Goal;
        if (person.Health <= 0 || person.Age < 14 || goal.Kind != AgentGoalKind.ExtinguishFire
            || !InBounds(goal.TargetX, goal.TargetY) || Distance(person.X, person.Y, goal.TargetX, goal.TargetY) > 1
            || State.Tick - person.MoveStartedTick < person.MoveDurationTicks || !Walkable(person.X, person.Y)
            || State.Tiles[Index(person.X, person.Y)].FireTicks > 0 || person.Inventory.Water < .1) return false;
        var index = Index(goal.TargetX, goal.TargetY);
        var tile = State.Tiles[index];
        if (tile.FireTicks <= 0)
        {
            person.Agent.NextThinkTick = State.Tick;
            return false;
        }

        if (tile.FireSuppressionTick != State.Tick)
        {
            tile.FireSuppressionTick = State.Tick;
            tile.FireSuppressed = 0;
        }

        // 每格共用每日扑救上限，避免聚集大量居民后火灾在一日内直接消失。
        var reduction = Math.Min(2 - tile.FireSuppressed, tile.FireTicks);
        if (reduction <= 0) return false;
        person.Inventory.Water -= .1;
        tile.FireSuppressed += reduction;
        tile.FireTicks -= reduction;
        person.Agent.Fatigue = Math.Min(100, person.Agent.Fatigue + .3);
        person.Activity = ResidentActivity.Working;
        if (tile.FireTicks == 0) EndFire(index, false);
        return true;
    }

    private void AddFirefightingChoice(Resident person, List<GoalChoice> choices)
    {
        if (_burningTiles.Count == 0 || person.Age < 14 || person.Inventory.Water < .1
            || person.Hunger >= 60 || person.Thirst >= 60 ||
            State.Tiles[Index(person.X, person.Y)].FireTicks > 0) return;
        foreach (var offset in VisibleResourceOffsets)
        {
            var x = person.X + offset.X;
            var y = person.Y + offset.Y;
            if (!InBounds(x, y) || State.Tiles[Index(x, y)].FireTicks <= 0) continue;
            if (!Directions.Any(d =>
                    Walkable(x + d.X, y + d.Y) && State.Tiles[Index(x + d.X, y + d.Y)].FireTicks == 0)) continue;
            choices.Add(new GoalChoice(AgentGoalKind.ExtinguishFire, x, y, 190 - offset.Distance,
                "携带饮水赶到火场边缘，持续用水扑救；同一火场每日扑救量有限"));
            return;
        }
    }
}
