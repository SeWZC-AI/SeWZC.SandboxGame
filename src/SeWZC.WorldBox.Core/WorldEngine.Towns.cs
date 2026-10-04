using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

public enum SettlementTier { Village, Town, City }

public sealed partial class Settlement
{
    [JsonRequired] public SettlementTier Tier { get; set; }
    public double ExpansionProgress { get; set; }
    public double ExpansionRequired { get; set; }
    [JsonIgnore] public bool IsExpanding => ExpansionRequired > 0;
}

public sealed partial class WorldEngine
{
    public const int MinimumSettlementDistance = 16;
    public static ResourceStock VillageFoundingCost => new() { Food = 160, Water = 60, Wood = 80, Stone = 40 };
    public static string SettlementTierName(SettlementTier tier) => tier switch
    { SettlementTier.City => "城", SettlementTier.Town => "镇", _ => "村" };
    public static int ExpansionPopulation(SettlementTier tier) => tier == SettlementTier.Village ? 60 : 160;
    public const int SettlementActivationArea = 25;
    public const double OutsideTerritoryGatheringMultiplier = .5;
    public static int ClaimRadiusArea(int radius) => 1 + 2 * radius * (radius + 1);
    public int GetSettlementExpansionArea(int id) => ClaimRadiusArea((RequireTown(id).MaxClaimRadius + 1) / 2);
    public static ResourceStock SettlementExpansionCost(SettlementTier tier) => tier switch
    {
        SettlementTier.Village => new() { Food = 80, Wood = 80, Stone = 40 },
        SettlementTier.Town => new() { Food = 180, Wood = 180, Stone = 120, Ore = 30 },
        _ => new()
    };

    private readonly Dictionary<int, (long Claims, long Terrain, int X, int Y, int Nation, int Area)> _settlementAreas = [];
    public int GetSettlementArea(int id)
    {
        if (!_settlements.TryGetValue(id, out var town)) return 0;
        _territoryCounts.Bind(State.Tiles);
        if (_settlementAreas.TryGetValue(id, out var cached) && cached.Claims == _territoryCounts.Revision
            && cached.Terrain == _territoryCounts.TraversalRevision && cached.X == town.X && cached.Y == town.Y && cached.Nation == town.NationId)
            return cached.Area;
        // Count exclusively registered plots in the local footprint, never a nation's shared total.
        var area = Circle(town.X, town.Y, 17).Count(i => Distance(town.X, town.Y, i % State.Width, i / State.Width) <= 17 && State.Tiles[i].ClaimedSettlementId == id
            && State.Tiles[i].NationId == town.NationId && (State.Tiles[i].IsWalkable || State.Tiles[i].Terrain == TerrainType.Mountain) && !IsWaterTerrain(State.Tiles[i].Terrain));
        _settlementAreas[id] = (_territoryCounts.Revision, _territoryCounts.TraversalRevision, town.X, town.Y, town.NationId, area);
        return area;
    }

    public bool IsSettlementActive(int id) => _settlements.TryGetValue(id, out var town) && !town.FoundationPending
        && GetSettlementArea(id) >= SettlementActivationArea;
    private int EffectiveSettlementRank(Settlement town) => IsSettlementActive(town.Id) ? (int)town.Tier : 0;
    private bool SettlementNeedsClaimArea(Settlement town) => State.Rules.Expansion && !town.FoundationPending
        && (!IsSettlementActive(town.Id) || town.Tier < SettlementTier.City && town.Population >= ExpansionPopulation(town.Tier)
            && GetSettlementArea(town.Id) < GetSettlementExpansionArea(town.Id));
    public double GetGatheringTerritoryMultiplier(int residentId, int x, int y)
    {
        var person = GetResident(residentId);
        return person is null || !InBounds(x, y) ? OutsideTerritoryGatheringMultiplier : GatheringTerritoryMultiplier(person, State.Tiles[Index(x, y)]);
    }
    private static double GatheringTerritoryMultiplier(Resident person, Tile source) => source.ClaimedSettlementId == person.SettlementId
        && source.NationId == person.NationId ? 1 : OutsideTerritoryGatheringMultiplier;

    public string? SettlementExpansionError(int id)
    {
        if (!_settlements.TryGetValue(id, out var town)) return "聚落已不存在";
        if (town.FoundationPending) return "先完成建村登记";
        if (town.Tier == SettlementTier.City) return "已完成城级扩充";
        if (town.IsExpanding) return "已有城镇扩充工程";
        if (town.Population < ExpansionPopulation(town.Tier)) return $"人口 {town.Population} / {ExpansionPopulation(town.Tier)}";
        var area = GetSettlementArea(id);
        if (area < GetSettlementExpansionArea(id)) return $"独占陆地 {area} / {GetSettlementExpansionArea(id)} 格，需占领最大半径一半的等价面积并实地登记";
        var center = State.Society.Buildings.FirstOrDefault(b => b.SettlementId == id && b.Kind == BuildingKind.TownCenter);
        if (center is null || !center.IsCompleted || center.IsUpgrading || center.Health < 50
            || State.Tiles[Index(town.X, town.Y)].FireTicks > 0) return "需要可工作的城镇中心组织扩充";
        return MissingResources(town.Resources, SettlementExpansionCost(town.Tier));
    }

    public void ExpandTown(int id)
    {
        if (SettlementExpansionError(id) is { } error) throw new InvalidOperationException(error);
        var town = RequireTown(id);
        Spend(town.Resources, SettlementExpansionCost(town.Tier));
        town.ExpansionProgress = 0; town.ExpansionRequired = town.Tier == SettlementTier.Village ? 60 : 120;
        AddEvent(WorldEventKind.Construction, $"{town.Name}投入扩充材料，居民到城镇中心施工后升为{SettlementTierName(town.Tier + 1)}。",
            town.X, town.Y, EventAction.Started, town.Id);
    }

    private bool WorkOnTownExpansion(Settlement town, double effort)
    {
        if (!town.IsExpanding) return false;
        // Lost territory suspends completion; paid materials and work remain available.
        if (GetSettlementArea(town.Id) < GetSettlementExpansionArea(town.Id)) return false;
        town.ExpansionProgress = Math.Min(town.ExpansionRequired, town.ExpansionProgress + effort * State.Rules.DevelopmentRate);
        if (town.ExpansionProgress < town.ExpansionRequired) return true;
        town.Tier++; town.ExpansionProgress = town.ExpansionRequired = 0;
        RefreshSettlementName(town);
        AddEvent(WorldEventKind.Growth, $"{town.Name}完成城镇扩充，公共组织与通信效率提高。", town.X, town.Y, EventAction.Completed, town.Id);
        return true;
    }

    public string GetSettlementSummary(int id)
    {
        var town = RequireTown(id); var rank = EffectiveSettlementRank(town); var area = GetSettlementArea(id);
        var text = $"城镇等级：{SettlementTierName(town.Tier)}   独占陆地 {GetSettlementArea(id)} 格\n"
            + $"城镇生效：{(IsSettlementActive(id) ? "已生效" : "领地不足，加成暂停")}   占地 {area}/{SettlementActivationArea} 格（半径 3 格的等价面积）\n"
            + $"返乡休息 ×{1 + rank * .1:0.00}   本地研究 ×{1 + rank * .1:0.00}\n"
            + $"家园 3 格内：同国信使速度 ×{1 + rank * .15:0.00}   每次交谈最多传递 {3 + rank * 2} 条消息\n本城镇占领地外采集效率 ×{OutsideTerritoryGatheringMultiplier:0.00}\n城镇中心等级加成同样需要城镇生效";
        if (town.IsExpanding) return text + $"\n扩充施工 {town.ExpansionProgress:0.#} / {town.ExpansionRequired:0}，需居民到场"
            + (area < GetSettlementExpansionArea(id) ? "\n领地不足，扩充暂停" : "");
        if (town.Tier == SettlementTier.City) return text;
        return text + $"\n升{SettlementTierName(town.Tier + 1)}要求：人口 {town.Population}/{ExpansionPopulation(town.Tier)}   独占陆地 {area}/{GetSettlementExpansionArea(id)} 格\n最大占领半径 {town.MaxClaimRadius} 格，需半径 {(town.MaxClaimRadius + 1) / 2} 格的等价面积\n"
            + "扩充材料：" + AdvancementRules.Stock(SettlementExpansionCost(town.Tier)) + "\n" + (SettlementExpansionError(id) ?? "条件已满足，可投入扩充");
    }
}
