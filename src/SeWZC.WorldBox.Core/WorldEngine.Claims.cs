using SeWZC.WorldBox.Core.Runtime;

namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    private readonly Queue<int> _claimQueue = new();
    private int[] _connectedClaims = [];

    private long _connectedClaimsRevision = -1;

    // 建村只登记实际占据的地点；人口仅提高占地上限，不能自动取得周围领土。
    private void ClaimTerritory(StateReference<Settlement> town, int radius)
    {
        town.Replace(town.Value with { MaxClaimRadius = Math.Max(town.Value.MaxClaimRadius, Math.Clamp(radius, 1, 17)) });
        if (town.Value.FoundationPending)
            return;
        var tile = Tiles[Index(town.Value.X, town.Value.Y)];
        tile.Replace(tile.Value with { NationId = town.Value.NationId, ClaimedSettlementId = town.Value.Id });
    }

    private bool CanClaimTile(StateReference<Settlement> town, int index, RaceKind race)
    {
        var tile = Tiles[index];
        var x = index % Width;
        var y = index / Width;
        if (!RaceTerrainRules.CanWalk(tile.Value, race) || IsWaterTerrain(tile.Value.Terrain) || tile.Value.FireTicks > 0 ||
            tile.Value.ClaimedSettlementId != 0
            || (tile.Value.NationId != 0 && tile.Value.NationId != town.Value.NationId)
            || Distance(town.Value.X, town.Value.Y, x, y) > town.Value.MaxClaimRadius)
            return false;
        foreach (var (dx, dy) in Directions)
            if (InBounds(x + dx, y + dy) && Tiles[Index(x + dx, y + dy)].Value.ClaimedSettlementId == town.Value.Id
                                         && CanTraverseStep(x + dx, y + dy, x, y, TravelMode.Foot, race))
                return true;
        return false;
    }

    private int VisibleClaimSite(StateReference<Resident> person, StateReference<Settlement> town)
    {
        if (town.Value.FoundationPending || !Rules.Expansion || person.Value.Age < 14 || person.Value.ArmyId != 0
            || person.Value.Profession != Profession.Builder)
            return -1;
        var active = 0;
        var reservedSite = -1;
        foreach (var resident in _citizens[town.Value.Id])
            if (resident.Value.Id != person.Value.Id && resident.Value.Agent.Goal.Kind == AgentGoalKind.ClaimLand)
            {
                if (++active >= 2)
                    return -1;
                reservedSite = Index(resident.Value.Agent.Goal.TargetX, resident.Value.Agent.Goal.TargetY);
            }

        var reachable = 0;
        foreach (var offset in VisibleResourceOffsets)
        {
            var x = person.Value.X + offset.X;
            var y = person.Value.Y + offset.Y;
            if (InBounds(x, y) && Index(x, y) != reservedSite && CanClaimTile(town, Index(x, y), person.Value.Race)
                && VisibleSiteReachable(person, Index(x, y), ref reachable))
                return Index(x, y);
        }

        return -1;
    }

    /// <summary>尝试让到场居民登记当前目标地块；返回是否完成登记。</summary>
    /// <param name="person">登记地块的居民。</param>
    public bool TryClaimLand(Resident person)

    {
        return TryClaimLand(RequireResident(person.Id));
    }

    private bool TryClaimLand(StateReference<Resident> person)
    {
        if (!_settlements.TryGetValue(person.Value.SettlementId, out var town) || person.Value.Age < 14 || person.Value.ArmyId != 0
            || person.Value.Health <= 0 || person.Value.Agent.Goal.Kind != AgentGoalKind.ClaimLand
            || SimulationTick - person.Value.MoveStartedTick < person.Value.MoveDurationTicks
            || person.Value.X != person.Value.Agent.Goal.TargetX || person.Value.Y != person.Value.Agent.Goal.TargetY)
            return false;
        var index = Index(person.Value.X, person.Value.Y);
        if (Residents.Any(r =>
                r.Value.Id != person.Value.Id && r.Value.Health > 0 && r.Value.SettlementId != town.Value.Id && r.Value.X == person.Value.X && r.Value.Y == person.Value.Y))
            return false;
        if (!CanClaimTile(town, index, person.Value.Race))
        {
            person.Replace(person.Value.WithAgent(person.Value.Agent with { NextThinkTick = SimulationTick }));
            return false;
        }

        if (person.Value.Agent.Goal.WorkTicks < 3)
            return true;
        var tile = Tiles[index];
        tile.Replace(tile.Value with { NationId = town.Value.NationId, ClaimedSettlementId = town.Value.Id });
        person.Replace(person.Value.WithAgent(person.Value.Agent with { NextThinkTick = SimulationTick }));
        if (SimulationTick % 12 == 0)
        {
            AddEvent(WorldEventKind.Growth, $"{person.Value.Name}实地为{town.Value.Name}登记新地盘。", person.Value.X, person.Value.Y,
                EventAction.General, town.Value.Id, person.Value.Id);
        }

        return true;
    }


    private void FinishFoundation(StateReference<Resident> person, StateReference<Settlement> town)
    {
        if (!town.Value.FoundationPending || person.Value.X != town.Value.X || person.Value.Y != town.Value.Y ||
            person.Value.Agent.Goal.WorkTicks < 3)
            return;
        if (town.Value.Resources.Wood + 1e-6 < VillageFoundingCost.Wood ||
            town.Value.Resources.Stone + 1e-6 < VillageFoundingCost.Stone)
            return;
        town.Replace(town.Value with
        {
            Resources = town.Value.Resources with
            {
                Wood = Math.Max(0, town.Value.Resources.Wood - VillageFoundingCost.Wood),
                Stone = Math.Max(0, town.Value.Resources.Stone - VillageFoundingCost.Stone),
            },
            FoundationPending = false,
        });
        ClaimTerritory(town, 4);
        AddFoundingFacility(town, BuildingKind.Farm);
        AddFoundingFacility(town, BuildingKind.Workshop);
        AddEvent(WorldEventKind.Founding, $"{person.Value.Name}到场驻留后建立{town.Value.Name}，开始实地登记地盘。", town.Value.X, town.Value.Y,
            EventAction.Completed, town.Value.Id, person.Value.Id);
    }

    private void RegisterBuildingGround(Building building)
    {
        if (IsPublicInfrastructure(building.Kind))
            return;
        if (!_settlements.TryGetValue(building.SettlementId, out var town) || town.Value.FoundationPending)
            return;
        var tile = Tiles[Index(building.X, building.Y)];
        if (tile.Value.NationId != 0 && tile.Value.NationId != town.Value.NationId)
            return;
        if (tile.Value.ClaimedSettlementId != 0 && tile.Value.ClaimedSettlementId != town.Value.Id)
            return;
        if (tile.Value.ClaimedSettlementId == 0 && !Directions.Any(d => InBounds(building.X + d.X, building.Y + d.Y)
                                                                  && Tiles[
                                                                          Index(building.X + d.X, building.Y + d.Y)].Value
                                                                      .ClaimedSettlementId == town.Value.Id))
            return;
        tile.Replace(tile.Value.WithNationId(town.Value.NationId));
        if (tile.Value.ClaimedSettlementId == 0)
            tile.Replace(tile.Value.WithClaimedSettlementId(town.Value.Id));
    }

    // 归属和占领变化后才重建连通区域，避免每个居民都重复扫描。
    private void ReconcileConnectedClaims()
    {
        _territoryCounts.Bind(Tiles);
        foreach (var town in Settlements)
        {
            if (town.Value.FoundationPending)
                continue;
            var root = Index(town.Value.X, town.Value.Y);
            Tiles[root].Replace(Tiles[root].Value.WithNationId(town.Value.NationId));
            Tiles[root].Replace(Tiles[root].Value.WithClaimedSettlementId(town.Value.Id));
        }

        if (_connectedClaimsRevision == _territoryCounts.Revision)
            return;
        if (_connectedClaims.Length != Tiles.Count)
            _connectedClaims = new int[Tiles.Count];
        FillConnectedClaims(CaptureSnapshot(), _connectedClaims, _claimQueue);
        foreach (var index in _territoryCounts.OwnedTiles)
            if (_connectedClaims[index] == 0)
                _claimQueue.Enqueue(index);
        while (_claimQueue.TryDequeue(out var orphan))
            Tiles[orphan].Replace(Tiles[orphan].Value with { ClaimedSettlementId = 0, NationId = 0 });

        _connectedClaimsRevision = _territoryCounts.Revision;
    }

    private static void FillConnectedClaims(WorldState state, int[] connected, Queue<int> queue)
    {
        Array.Clear(connected);
        queue.Clear();
        foreach (var town in state.Settlements)
        {
            if (town.FoundationPending)
                continue;
            var root = town.Y * state.Width + town.X;
            if (state.Tiles[root].ClaimedSettlementId != town.Id ||
                state.Tiles[root].NationId != town.NationId)
                continue;
            connected[root] = town.Id;
            queue.Enqueue(root);
            while (queue.TryDequeue(out var current))
                foreach (var (dx, dy) in Directions)
                {
                    var x = current % state.Width + dx;
                    var y = current / state.Width + dy;
                    if (x < 0 || y < 0 || x >= state.Width || y >= state.Height)
                        continue;
                    var next = y * state.Width + x;
                    var tile = state.Tiles[next];
                    if (connected[next] != 0 || tile.ClaimedSettlementId != town.Id ||
                        tile.NationId != town.NationId)
                        continue;
                    if (IsWaterTerrain(state.Tiles[current].Terrain) && IsWaterTerrain(tile.Terrain))
                        continue;
                    connected[next] = town.Id;
                    queue.Enqueue(next);
                }
        }
    }

    private static void ValidateConnectedClaims(WorldState state)
    {
        var connected = new int[state.Tiles.Count];
        FillConnectedClaims(state, connected, new Queue<int>());
        for (var i = 0; i < state.Tiles.Count; i++)
            if ((state.Tiles[i].NationId != 0 || state.Tiles[i].ClaimedSettlementId != 0) && connected[i] == 0)
                throw new ArgumentException("无效存档：占领区域须登记给城镇并与中心相连。");
    }
}
