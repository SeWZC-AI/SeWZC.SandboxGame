using SeWZC.WorldBox.Core.Runtime;

namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    /// <summary>建立新聚落时，与现有聚落中心所需的最小距离，以地格计。</summary>
    public const int MinimumSettlementDistance = 16;

    /// <summary>启用城镇加成所需的独占陆地数量。</summary>
    public const int SettlementActivationArea = 25;

    /// <summary>居民在本城镇独占区域外采集时的产量倍率。</summary>
    public const double OutsideTerritoryGatheringMultiplier = .5;

    private readonly Dictionary<int, (long Claims, int X, int Y, int Nation, int Area)> _settlementAreas =
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
        return ClaimRadiusArea((RequireTown(id).Value.MaxClaimRadius + 1) / 2);
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
        if (!_settlements.TryGetValue(id, out var town))
            return 0;
        _territoryCounts.Bind(Current.Tiles);
        // 面积取决于归属与陆水转换；其他地方的起火及桥梁轴向变化不改变登记面积。
        if (_settlementAreas.TryGetValue(id, out var cached) && cached.Claims == _territoryCounts.Revision &&
            cached.X == town.Value.X && cached.Y == town.Value.Y &&
            cached.Nation == town.Value.NationId)
            return cached.Area;
        // 城镇加成须按本地独占登记陆地计算，避免借用国家总领土满足条件。
        var area = Circle(town.Value.X, town.Value.Y, 17).Count(i =>
            Distance(town.Value.X, town.Value.Y, i % Current.Width, i / Current.Width) <= 17 &&
            Current.Tiles[i].Value.ClaimedSettlementId == id
            && Current.Tiles[i].Value.NationId ==
            town.Value.NationId &&
            (Current.Tiles[i].Value.IsWalkable ||
             Current.Tiles[i].Value.Terrain ==
             TerrainType.Mountain) &&
            !IsWaterTerrain(Current.Tiles[i].Value.Terrain));
        _settlementAreas[id] = (_territoryCounts.Revision, town.Value.X, town.Value.Y,
            town.Value.NationId, area);
        return area;
    }

    /// <summary>判断建村交付已完成且独占陆地足以启用城镇加成。</summary>
    /// <param name="id">聚落的稳定 ID。</param>
    public bool IsSettlementActive(int id)
    {
        return _settlements.TryGetValue(id, out var town) && !town.Value.FoundationPending
                                                          && GetSettlementArea(id) >= SettlementActivationArea;
    }

    private int EffectiveSettlementRank(StateReference<Settlement> town)
    {
        return IsSettlementActive(town.Value.Id) ? (int)town.Value.Tier : 0;
    }

    private bool SettlementNeedsClaimArea(StateReference<Settlement> town)
    {
        return Current.Rules.Expansion && !town.Value.FoundationPending
                                       && (!IsSettlementActive(town.Value.Id) || (town.Value.Tier < SettlementTier.City &&
                                                                            town.Value.Population >=
                                                                            ExpansionPopulation(town.Value.Tier)
                                                                            && GetSettlementArea(town.Value.Id) <
                                                                            GetSettlementExpansionArea(town.Value.Id)));
    }

    /// <summary>查询居民在此地采集所用的本城镇领地内外倍率。</summary>
    /// <param name="residentId">居民 ID。</param>
    /// <param name="x">横向地格坐标。</param>
    /// <param name="y">纵向地格坐标。</param>
    public double GetGatheringTerritoryMultiplier(int residentId, int x, int y)
    {
        var person = GetResident(residentId);
        return person is null || !InBounds(x, y)
            ? OutsideTerritoryGatheringMultiplier
            : GatheringTerritoryMultiplier(person.SettlementId, person.NationId, Current.Tiles[Index(x, y)].Value);
    }

    private static double GatheringTerritoryMultiplier(int settlementId, int nationId, Tile source)
    {
        return source.ClaimedSettlementId == settlementId
               && source.NationId == nationId
            ? 1
            : OutsideTerritoryGatheringMultiplier;
    }

    /// <summary>检查聚落晋升的人口、领地、材料和项目条件；可晋升时返回空值，否则返回原因。</summary>
    /// <param name="id">聚落的稳定 ID。</param>
    public string? SettlementExpansionError(int id)
    {
        if (!_settlements.TryGetValue(id, out var town))
            return "聚落已不存在";
        if (town.Value.FoundationPending)
            return "先完成建村登记";
        if (town.Value.Tier == SettlementTier.City)
            return "已完成城级扩充";
        if (town.Value.IsExpanding)
            return "已有城镇扩充工程";
        if (town.Value.Population < ExpansionPopulation(town.Value.Tier))
            return $"人口 {town.Value.Population} / {ExpansionPopulation(town.Value.Tier)}";
        var area = GetSettlementArea(id);
        if (area < GetSettlementExpansionArea(id))
            return $"独占陆地 {area} / {GetSettlementExpansionArea(id)} 格，需占领最大半径一半的等价面积并实地登记";
        var center =
            Current.Buildings.FirstOrDefault(b => b.Value.SettlementId == id && b.Value.Kind == BuildingKind.TownCenter);
        if (center is null || !center.Value.IsCompleted || center.Value.IsUpgrading || center.Value.Health < 50
            || Current.Tiles[Index(town.Value.X, town.Value.Y)].Value.FireTicks > 0)
            return "需要可工作的城镇中心组织扩充";
        return MissingResources(town.Value.Resources, SettlementExpansionCost(town.Value.Tier));
    }

    /// <summary>扣除聚落材料并启动村镇城晋升施工。</summary>
    /// <param name="id">聚落的稳定 ID。</param>
    public void ExpandTown(int id)
    {
        if (SettlementExpansionError(id) is { } error)
            throw new InvalidOperationException(error);
        var town = RequireTown(id);
        town.Replace(town.Value.WithResources(Spend(town.Value.Resources, SettlementExpansionCost(town.Value.Tier))));
        town.Replace(town.Value with
        {
            ExpansionProgress = 0, ExpansionRequired = town.Value.Tier == SettlementTier.Village ? 60 : 120,
        });
        AddEvent(WorldEventKind.Construction, $"{town.Value.Name}投入扩充材料，居民到城镇中心施工后升为{SettlementTierName(town.Value.Tier + 1)}。",
            town.Value.X, town.Value.Y, EventAction.Started, town.Value.Id);
    }

    private bool WorkOnTownExpansion(StateReference<Settlement> town, double effort)
    {
        if (!town.Value.IsExpanding)
            return false;
        // 领地不足时暂停晋升完工，保留已经支付的材料和施工进度，避免重复收费。
        if (GetSettlementArea(town.Value.Id) < GetSettlementExpansionArea(town.Value.Id))
            return false;
        var before = town.Value;
        var after = before.AdvanceExpansion(effort, Current.Rules.DevelopmentRate);
        town.Replace(after);
        if (after.Tier == before.Tier)
            return true;
        RefreshSettlementName(town);
        AddEvent(WorldEventKind.Growth, $"{town.Value.Name}完成城镇扩充，公共组织与通信效率提高。", town.Value.X, town.Value.Y, EventAction.Completed,
            town.Value.Id);
        return true;
    }

    /// <summary>返回聚落的规模与发展条件摘要。</summary>
    /// <param name="id">聚落的稳定 ID。</param>
    public string GetSettlementSummary(int id)
    {
        var town = RequireTown(id);
        var rank = EffectiveSettlementRank(town);
        var area = GetSettlementArea(id);
        var text = $"城镇等级：{SettlementTierName(town.Value.Tier)}\n"
                   + $"城镇生效：{(IsSettlementActive(id) ? "已生效" : "领地不足，加成暂停")}\n"
                   + $"人口 {town.Value.Population}   住房 {GetHousingCapacity(id)}";
        if (town.Value.IsExpanding)
        {
            return text + $"\n扩充施工 {town.Value.ExpansionProgress:0.#} / {town.Value.ExpansionRequired:0}，需居民到场"
                        + (area < GetSettlementExpansionArea(id) ? "\n领地不足，扩充暂停" : "");
        }

        if (town.Value.Tier == SettlementTier.City)
            return text;
        return
            text + $"\n升{SettlementTierName(town.Value.Tier + 1)}要求：人口 {town.Value.Population}/{ExpansionPopulation(town.Value.Tier)}\n"
                 + "扩充材料：" + ResourceStock.Format(SettlementExpansionCost(town.Value.Tier)) + "\n" +
                 (SettlementExpansionError(id) ?? "条件已满足，可投入扩充");
    }
}
