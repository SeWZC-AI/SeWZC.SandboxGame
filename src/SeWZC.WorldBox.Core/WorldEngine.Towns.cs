namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    /// <summary>建立新聚落时，与现有聚落中心所需的最小距离，以地格计。</summary>
    public const int MinimumSettlementDistance = 16;

    /// <summary>启用城镇加成所需的独占陆地数量。</summary>
    public const int SettlementActivationArea = 25;

    /// <summary>居民在本城镇独占区域外采集时的产量倍率。</summary>
    public const double OutsideTerritoryGatheringMultiplier = .5;

    private readonly Dictionary<int, (long Claims, long Terrain, int X, int Y, int Nation, int Area)> _settlementAreas =
        [];

    /// <summary>建立新村需要实际运输和交付的初始物资。</summary>
    public static ResourceStock VillageFoundingCost => new() { Food = 160, Water = 60, Wood = 80, Stone = 40 };

    /// <summary>返回聚落村镇城等级的中文名称。</summary>
    /// <param name="tier">待显示的村镇城等级。</param>
    public static string SettlementTierName(SettlementTier tier)
    {
        return tier switch
        {
            SettlementTier.Town => "镇",
            SettlementTier.City => "城",
            _ => "村",
        };
    }

    /// <summary>返回从当前村镇城等级晋升所需的人口。</summary>
    /// <param name="tier">晋升前的当前村镇城等级。</param>
    public static int ExpansionPopulation(SettlementTier tier)
    {
        return tier == SettlementTier.Village ? 60 : 160;
    }

    /// <summary>计算曼哈顿半径内包含中心的地格数量。</summary>
    /// <param name="radius">曼哈顿距离半径，以地格为单位。</param>
    public static int ClaimRadiusArea(int radius)
    {
        return 1 + 2 * radius * (radius + 1);
    }

    /// <summary>计算聚落晋升所需的独占陆地面积。</summary>
    /// <param name="id">聚落的稳定 ID。</param>
    public int GetSettlementExpansionArea(int id)
    {
        return ClaimRadiusArea((RequireTown(id).MaxClaimRadius + 1) / 2);
    }

    /// <summary>返回从当前村镇城等级晋升需要投入的资源。</summary>
    /// <param name="tier">晋升前的当前村镇城等级。</param>
    public static ResourceStock SettlementExpansionCost(SettlementTier tier)
    {
        return tier switch
        {
            SettlementTier.Village => new ResourceStock { Food = 80, Wood = 80, Stone = 40 },
            SettlementTier.Town => new ResourceStock { Food = 180, Wood = 180, Stone = 120, Ore = 30 },
            _ => new ResourceStock(),
        };
    }

    /// <summary>统计聚落附近独占登记的本国陆地面积；聚落不存在时返回零。</summary>
    /// <param name="id">聚落的稳定 ID。</param>
    public int GetSettlementArea(int id)
    {
        if (!_settlements.TryGetValue(id, out var town)) return 0;
        _territoryCounts.Bind(State.Tiles);
        if (_settlementAreas.TryGetValue(id, out var cached) && cached.Claims == _territoryCounts.Revision
                                                             && cached.Terrain == _territoryCounts.TraversalRevision &&
                                                             cached.X == town.X && cached.Y == town.Y &&
                                                             cached.Nation == town.NationId)
            return cached.Area;
        // 城镇加成须按本地独占登记陆地计算，避免借用国家总领土满足条件。
        var area = Circle(town.X, town.Y, 17).Count(i =>
            Distance(town.X, town.Y, i % State.Width, i / State.Width) <= 17 && State.Tiles[i].ClaimedSettlementId == id
                                                                             && State.Tiles[i].NationId ==
                                                                             town.NationId &&
                                                                             (State.Tiles[i].IsWalkable ||
                                                                              State.Tiles[i].Terrain ==
                                                                              TerrainType.Mountain) &&
                                                                             !IsWaterTerrain(State.Tiles[i].Terrain));
        _settlementAreas[id] = (_territoryCounts.Revision, _territoryCounts.TraversalRevision, town.X, town.Y,
            town.NationId, area);
        return area;
    }

    /// <summary>判断建村交付已完成且独占陆地足以启用城镇加成。</summary>
    /// <param name="id">聚落的稳定 ID。</param>
    public bool IsSettlementActive(int id)
    {
        return _settlements.TryGetValue(id, out var town) && !town.FoundationPending
                                                          && GetSettlementArea(id) >= SettlementActivationArea;
    }

    private int EffectiveSettlementRank(Settlement town)
    {
        return IsSettlementActive(town.Id) ? (int)town.Tier : 0;
    }

    private bool SettlementNeedsClaimArea(Settlement town)
    {
        return State.Rules.Expansion && !town.FoundationPending
                                     && (!IsSettlementActive(town.Id) || (town.Tier < SettlementTier.City &&
                                                                          town.Population >=
                                                                          ExpansionPopulation(town.Tier)
                                                                          && GetSettlementArea(town.Id) <
                                                                          GetSettlementExpansionArea(town.Id)));
    }

    /// <summary>查询居民在此地采集所用的本城镇领地内外倍率。</summary>
    /// <param name="residentId">待操作居民的稳定 ID。</param>
    /// <param name="x">横向地格坐标。</param>
    /// <param name="y">纵向地格坐标。</param>
    public double GetGatheringTerritoryMultiplier(int residentId, int x, int y)
    {
        var person = GetResident(residentId);
        return person is null || !InBounds(x, y)
            ? OutsideTerritoryGatheringMultiplier
            : GatheringTerritoryMultiplier(person, State.Tiles[Index(x, y)]);
    }

    private static double GatheringTerritoryMultiplier(Resident person, Tile source)
    {
        return source.ClaimedSettlementId == person.SettlementId
               && source.NationId == person.NationId
            ? 1
            : OutsideTerritoryGatheringMultiplier;
    }

    /// <summary>检查聚落晋升的人口、领地、材料和项目条件；可晋升时返回空值，否则返回原因。</summary>
    /// <param name="id">聚落的稳定 ID。</param>
    public string? SettlementExpansionError(int id)
    {
        if (!_settlements.TryGetValue(id, out var town)) return "聚落已不存在";
        if (town.FoundationPending) return "先完成建村登记";
        if (town.Tier == SettlementTier.City) return "已完成城级扩充";
        if (town.IsExpanding) return "已有城镇扩充工程";
        if (town.Population < ExpansionPopulation(town.Tier))
            return $"人口 {town.Population} / {ExpansionPopulation(town.Tier)}";
        var area = GetSettlementArea(id);
        if (area < GetSettlementExpansionArea(id))
            return $"独占陆地 {area} / {GetSettlementExpansionArea(id)} 格，需占领最大半径一半的等价面积并实地登记";
        var center =
            State.Society.Buildings.FirstOrDefault(b => b.SettlementId == id && b.Kind == BuildingKind.TownCenter);
        if (center is null || !center.IsCompleted || center.IsUpgrading || center.Health < 50
            || State.Tiles[Index(town.X, town.Y)].FireTicks > 0) return "需要可工作的城镇中心组织扩充";
        return MissingResources(town.Resources, SettlementExpansionCost(town.Tier));
    }

    /// <summary>扣除聚落材料并启动村镇城晋升施工。</summary>
    /// <param name="id">聚落的稳定 ID。</param>
    public void ExpandTown(int id)
    {
        if (SettlementExpansionError(id) is { } error) throw new InvalidOperationException(error);
        var town = RequireTown(id);
        Spend(town.Resources, SettlementExpansionCost(town.Tier));
        town.ExpansionProgress = 0;
        town.ExpansionRequired = town.Tier == SettlementTier.Village ? 60 : 120;
        AddEvent(WorldEventKind.Construction, $"{town.Name}投入扩充材料，居民到城镇中心施工后升为{SettlementTierName(town.Tier + 1)}。",
            town.X, town.Y, EventAction.Started, town.Id);
    }

    private bool WorkOnTownExpansion(Settlement town, double effort)
    {
        if (!town.IsExpanding) return false;
        // 领地不足时暂停晋升完工，保留已经支付的材料和施工进度，避免重复收费。
        if (GetSettlementArea(town.Id) < GetSettlementExpansionArea(town.Id)) return false;
        town.ExpansionProgress = Math.Min(town.ExpansionRequired,
            town.ExpansionProgress + effort * State.Rules.DevelopmentRate);
        if (town.ExpansionProgress < town.ExpansionRequired) return true;
        town.Tier++;
        town.ExpansionProgress = town.ExpansionRequired = 0;
        RefreshSettlementName(town);
        AddEvent(WorldEventKind.Growth, $"{town.Name}完成城镇扩充，公共组织与通信效率提高。", town.X, town.Y, EventAction.Completed,
            town.Id);
        return true;
    }

    /// <summary>返回聚落的规模与发展条件摘要。</summary>
    /// <param name="id">聚落的稳定 ID。</param>
    public string GetSettlementSummary(int id)
    {
        var town = RequireTown(id);
        var rank = EffectiveSettlementRank(town);
        var area = GetSettlementArea(id);
        var text = $"城镇等级：{SettlementTierName(town.Tier)}\n"
                   + $"城镇生效：{(IsSettlementActive(id) ? "已生效" : "领地不足，加成暂停")}\n"
                   + $"人口 {town.Population}   住房 {GetHousingCapacity(id)}";
        if (town.IsExpanding)
        {
            return text + $"\n扩充施工 {town.ExpansionProgress:0.#} / {town.ExpansionRequired:0}，需居民到场"
                        + (area < GetSettlementExpansionArea(id) ? "\n领地不足，扩充暂停" : "");
        }

        if (town.Tier == SettlementTier.City) return text;
        return
            text + $"\n升{SettlementTierName(town.Tier + 1)}要求：人口 {town.Population}/{ExpansionPopulation(town.Tier)}\n"
                 + "扩充材料：" + ResourceStock.Format(SettlementExpansionCost(town.Tier)) + "\n" +
                 (SettlementExpansionError(id) ?? "条件已满足，可投入扩充");
    }
}
