using SeWZC.WorldBox.Core.Runtime;

namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    private readonly Queue<int> _claimQueue = new();
    private int[] _connectedClaims = [];

    private long _connectedClaimsRevision = -1;

    // 建村只登记实际占据的地点；人口仅提高占地上限，不能自动取得周围领土。
    private void ClaimTerritory(SettlementCursor town, int radius)
    {
        town.Replace(town.Value with { MaxClaimRadius = Math.Max(town.MaxClaimRadius, Math.Clamp(radius, 1, 17)) });
        if (town.FoundationPending)
            return;
        var tile = Current.Tiles[Index(town.X, town.Y)];
        tile.Replace(tile.Value with { NationId = town.NationId, ClaimedSettlementId = town.Id });
    }

    private bool CanClaimTile(SettlementCursor town, int index, RaceKind race)
    {
        var tile = Current.Tiles[index];
        var x = index % Current.Width;
        var y = index / Current.Width;
        if (!RaceTerrainRules.CanWalk(tile.Value, race) || IsWaterTerrain(tile.Terrain) || tile.FireTicks > 0 ||
            tile.ClaimedSettlementId != 0
            || (tile.NationId != 0 && tile.NationId != town.NationId)
            || Distance(town.X, town.Y, x, y) > town.MaxClaimRadius)
            return false;
        foreach (var (dx, dy) in Directions)
            if (InBounds(x + dx, y + dy) && Current.Tiles[Index(x + dx, y + dy)].ClaimedSettlementId == town.Id
                                         && CanTraverseStep(x + dx, y + dy, x, y, TravelMode.Foot, race))
                return true;
        return false;
    }

    private int VisibleClaimSite(ResidentCursor person, SettlementCursor town)
    {
        if (town.FoundationPending || !Current.Rules.Expansion || person.Age < 14 || person.ArmyId != 0
            || person.Profession != Profession.Builder)
            return -1;
        var active = 0;
        var reservedSite = -1;
        foreach (var resident in _citizens[town.Id])
            if (resident.Id != person.Id && resident.Agent.Goal.Kind == AgentGoalKind.ClaimLand)
            {
                if (++active >= 2)
                    return -1;
                reservedSite = Index(resident.Agent.Goal.TargetX, resident.Agent.Goal.TargetY);
            }

        var reachable = 0;
        foreach (var offset in VisibleResourceOffsets)
        {
            var x = person.X + offset.X;
            var y = person.Y + offset.Y;
            if (InBounds(x, y) && Index(x, y) != reservedSite && CanClaimTile(town, Index(x, y), person.Race)
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

    private bool TryClaimLand(ResidentCursor person)
    {
        if (!_settlements.TryGetValue(person.SettlementId, out var town) || person.Age < 14 || person.ArmyId != 0
            || person.Health <= 0 || person.Agent.Goal.Kind != AgentGoalKind.ClaimLand
            || Current.Tick - person.MoveStartedTick < person.MoveDurationTicks
            || person.X != person.Agent.Goal.TargetX || person.Y != person.Agent.Goal.TargetY)
            return false;
        var index = Index(person.X, person.Y);
        if (Current.Residents.Any(r =>
                r.Id != person.Id && r.Health > 0 && r.SettlementId != town.Id && r.X == person.X && r.Y == person.Y))
            return false;
        if (!CanClaimTile(town, index, person.Race))
        {
            person.Agent = person.Agent with { NextThinkTick = Current.Tick };
            return false;
        }

        if (person.Agent.Goal.WorkTicks < 3)
            return true;
        var tile = Current.Tiles[index];
        tile.Replace(tile.Value with { NationId = town.NationId, ClaimedSettlementId = town.Id });
        person.Agent = person.Agent with { NextThinkTick = Current.Tick };
        if (Current.Tick % 12 == 0)
        {
            AddEvent(WorldEventKind.Growth, $"{person.Name}实地为{town.Name}登记新地盘。", person.X, person.Y,
                EventAction.General, town.Id, person.Id);
        }

        return true;
    }


    private void FinishFoundation(ResidentCursor person, SettlementCursor town)
    {
        if (!town.FoundationPending || person.X != town.X || person.Y != town.Y ||
            person.Agent.Goal.WorkTicks < 3)
            return;
        if (town.Resources.Wood + 1e-6 < VillageFoundingCost.Wood ||
            town.Resources.Stone + 1e-6 < VillageFoundingCost.Stone)
            return;
        town.Replace(town.Value with
        {
            Resources = town.Resources with
            {
                Wood = Math.Max(0, town.Resources.Wood - VillageFoundingCost.Wood),
                Stone = Math.Max(0, town.Resources.Stone - VillageFoundingCost.Stone),
            },
            FoundationPending = false,
        });
        ClaimTerritory(town, 4);
        AddFoundingFacility(town, BuildingKind.Farm);
        AddFoundingFacility(town, BuildingKind.Workshop);
        AddEvent(WorldEventKind.Founding, $"{person.Name}到场驻留后建立{town.Name}，开始实地登记地盘。", town.X, town.Y,
            EventAction.Completed, town.Id, person.Id);
    }

    private void RegisterBuildingGround(Building building)
    {
        if (IsPublicInfrastructure(building.Kind))
            return;
        if (!_settlements.TryGetValue(building.SettlementId, out var town) || town.FoundationPending)
            return;
        var tile = Current.Tiles[Index(building.X, building.Y)];
        if (tile.NationId != 0 && tile.NationId != town.NationId)
            return;
        if (tile.ClaimedSettlementId != 0 && tile.ClaimedSettlementId != town.Id)
            return;
        if (tile.ClaimedSettlementId == 0 && !Directions.Any(d => InBounds(building.X + d.X, building.Y + d.Y)
                                                                  && Current.Tiles[
                                                                          Index(building.X + d.X, building.Y + d.Y)]
                                                                      .ClaimedSettlementId == town.Id))
            return;
        tile.NationId = town.NationId;
        if (tile.ClaimedSettlementId == 0)
            tile.ClaimedSettlementId = town.Id;
    }

    // 归属和占领变化后才重建连通区域，避免每个居民都重复扫描。
    private void ReconcileConnectedClaims()
    {
        _territoryCounts.Bind(Current.Tiles);
        foreach (var town in Current.Settlements)
        {
            if (town.FoundationPending)
                continue;
            var root = Index(town.X, town.Y);
            Current.Tiles[root].NationId = town.NationId;
            Current.Tiles[root].ClaimedSettlementId = town.Id;
        }

        if (_connectedClaimsRevision == _territoryCounts.Revision)
            return;
        if (_connectedClaims.Length != Current.Tiles.Count)
            _connectedClaims = new int[Current.Tiles.Count];
        FillConnectedClaims(Current.Snapshot, _connectedClaims, _claimQueue);
        foreach (var index in _territoryCounts.OwnedTiles)
            if (_connectedClaims[index] == 0)
                _claimQueue.Enqueue(index);
        while (_claimQueue.TryDequeue(out var orphan))
            Current.Tiles[orphan].Replace(Current.Tiles[orphan].Value with { ClaimedSettlementId = 0, NationId = 0 });

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
