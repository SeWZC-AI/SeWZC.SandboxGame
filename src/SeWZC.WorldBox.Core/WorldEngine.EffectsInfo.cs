namespace SeWZC.WorldBox.Core;

public readonly record struct EffectInfo(string Name, string Effect, string Source, long? RemainingDays = null, bool Active = true)
{
    public override string ToString() => $"{Name}：{Effect}\n来源：{Source}" + (RemainingDays.HasValue ? $"   剩余 {RemainingDays} 日" : "") + (Active ? "" : "\n当前未生效：需要完工、健康与运营条件");
}

public sealed partial class WorldEngine
{
    private static double ThirstWorkMultiplier(Resident person) => person.Thirst > 80 ? .75 : 1;
    private static double GatheringCondition(Resident person) => (person.SicknessTicks > 0 ? .4 : 1)
        * (person.Hunger > 60 ? .55 : 1) * ThirstWorkMultiplier(person);
    private static double LaborCondition(Resident person) => (person.SicknessTicks > 0 ? .45 : 1) * ThirstWorkMultiplier(person);
    private double HomeRestMultiplier(Resident person)
    {
        if (!_settlements.TryGetValue(person.SettlementId, out var home) || Distance(person.X, person.Y, home.X, home.Y) > 1) return 1;
        IEnumerable<Building>? buildings = _localWorkQueriesActive ? _localWorkBuildings.GetValueOrDefault(home.Id) : State.Society.Buildings;
        if (buildings is null) return 1;
        foreach (var building in buildings)
            if (building.SettlementId == home.Id && building.Kind == BuildingKind.TownCenter && building.Level > 1 && IsFacilityOperating(building)) return building.Efficiency;
        return 1;
    }

    public IReadOnlyList<EffectInfo> GetResidentEffects(int id)
    {
        var person = GetResident(id); var effects = new List<EffectInfo>();
        if (person is null) return effects;
        if (person.SicknessTicks > 0) effects.Add(new("疫病", "采集效率 ×0.40   施工与岗位劳动效率 ×0.45", "身体状态", person.SicknessTicks));
        if (person.Hunger > 60) effects.Add(new("饥饿", "现场采集效率 ×0.55；超过 80 时每日生命 -0.30", "身体状态"));
        if (person.Thirst > 80) effects.Add(new("缺水", "采集与岗位劳动效率 ×0.75；超过 95 时每日生命 -0.25", "身体状态"));
        if (person.Race == RaceKind.Elf) effects.Add(new("精灵采伐", "野外伐木产出 ×1.20", "种族"));
        if (person.Race == RaceKind.Dwarf) effects.Add(new("矮人采矿", "野外石矿产出 ×1.30", "种族"));
        effects.Add(new("勤勉", $"野外采集效率 ×{.75 + person.Agent.Personality.Diligence * .5:0.00}", "人格"));
        foreach (var town in State.Settlements)
        {
            if (town.NationId != person.NationId || Distance(town.X, town.Y, person.X, person.Y) > 5) continue;
            if (town.ShieldTicks > 0) effects.Add(new("护盾", "附近伤害 ×0.60，同类保护取最强", town.Name, town.ShieldTicks));
            if (GetLocalPolicy(town.Id) == PolicyKind.Defense) effects.Add(new("防御政策", "附近伤害 ×0.88，与当地护盾相乘", town.Name));
        }
        if (_settlements.TryGetValue(person.SettlementId, out var home))
        {
            var instruction = LatestAgentFact(person.Agent.Memory, AgentFactKind.Policy, home.Id);
            if (instruction?.Value == (int)PolicyKind.FoodSecurity)
                effects.Add(new("已获知粮食政策", $"个人粮食采集倍率 ×{AgentFoodPolicyMultiplier(person):0.00}", home.Name + "实际收到的政策"));
        }
        var resting = HomeRestMultiplier(person);
        if (resting > 1) effects.Add(new("城镇中心", $"附近返乡休息恢复 ×{resting:0.00}", "家园城镇中心"));
        return effects;
    }

    public IReadOnlyList<EffectInfo> GetBuildingEffects(int id)
    {
        var building = State.Society.Buildings.FirstOrDefault(b => b.Id == id); var effects = new List<EffectInfo>();
        if (building is null) return effects;
        var ground = State.Tiles[Index(building.X, building.Y)];
        var active = building.Kind == BuildingKind.Bridge ? ground.Improvement == LandImprovement.Bridge
            : building.Kind == BuildingKind.MountainPass ? ground.Improvement == LandImprovement.MountainPass : IsBuildingOperational(building);
        var source = BuildingName(building.Kind) + $" {building.Level} 级";
        var factor = building.Efficiency;
        var effect = building.Kind switch
        {
            BuildingKind.Farm or BuildingKind.Workshop => $"到场生产效率 ×{factor:0.00}",
            BuildingKind.Academy => $"现场研究效率 ×{factor:0.00}",
            BuildingKind.ArcaneSanctum => $"现场训练效率 ×{factor:0.00}",
            BuildingKind.Infirmary => $"现场治疗生命恢复 ×{factor:0.00}，范围 3 格",
            BuildingKind.Bridge => $"仅沿{BridgeDirectionName(building.Direction)}通行，步行耗时系数 {1.2 / factor:0.00}；离自然岸最多 {BridgeShoreLimit(building.Level)} 格",
            BuildingKind.MountainPass => $"山地步行耗时系数 {3.5 / factor:0.00}",
            BuildingKind.TownCenter => $"同聚落居民在 1 格内返乡休息恢复 ×{factor:0.00}",
            BuildingKind.Waystation => $"同国信使在 3 格内速度 ×{1.25 + (building.Level - 1) * .15:0.00}；同类取最强，需要实际值守",
            BuildingKind.SignalTower => $"信号接入 {12 + (building.Level - 1) * 4} 格、塔间至多 {24 + (building.Level - 1) * 8} 格（取双方较低等级）；需要值守、知识和连通",
            _ => $"每批加工产出 ×{factor:0.00}；原料、知识与到场条件仍需满足"
        };
        effects.Add(new("建筑提供的加成", effect, source, Active: active));
        effects.Add(new("岗位容量", $"最多 {building.WorkSlots} 名到场工作人员", source, Active: IsBuildingOperational(building)));
        effects.Add(new("建筑耐火", $"可燃性 {BuildingFlammability(building):0.00} / 1；火中每日生命 -{1.5 * BuildingFlammability(building):0.00}，升级降低可燃性", source));
        if (building.Level < 3) effects.Add(new("下一级", $"等级 {building.Level + 1}，岗位增加 1" + (building.Kind is BuildingKind.Waystation or BuildingKind.SignalTower ? "；提升运输速度／信号覆盖" : $"；适用效率倍率 ×{1 + building.Level * .25:0.00}"), "升级完工后", Active: false));
        if (_settlements.TryGetValue(building.SettlementId, out var town))
        {
            if (building.Kind == BuildingKind.Farm && HasResearch(town.Id, ResearchKind.Agriculture)) effects.Add(new("农业知识", "农场粮食产出 ×1.35", town.Name));
            if (building.Kind == BuildingKind.Farm && town.FertilityBoostTicks > 0) effects.Add(new("丰饶", "农场粮食产出 ×1.35", town.Name, town.FertilityBoostTicks));
            if (building.Kind == BuildingKind.Academy && GetLocalPolicy(town.Id) == PolicyKind.Scholarship) effects.Add(new("学术政策", "研究效率 ×1.35", town.Name));
            if (building.Kind == BuildingKind.Farm) effects.Add(new("当地生产政策", $"农场产出 ×{GetPolicyProductionMultiplier(town.Id):0.00}", town.Name));
        }
        return effects;
    }

    public IReadOnlyList<EffectInfo> GetTileEffects(int x, int y)
    {
        var effects = new List<EffectInfo>(); if (!InBounds(x, y)) return effects;
        var tile = State.Tiles[Index(x, y)];
        effects.Add(new("地形可燃性", $"{TerrainFlammability(tile):0.00} / 1；受植被、剩余资源、供水与干旱影响", "当地地形；建筑可燃性另计"));
        if (tile.FireTicks > 0) effects.Add(new("燃烧", "停止生产与取水，居民每日灼伤 4", "当地火灾", tile.FireTicks));
        if (tile.DroughtTicks > 0) effects.Add(new("干旱", "野外食物产出 ×0.15   农场粮食 ×0.18   天然供水 ×0.20", "当地干旱", tile.DroughtTicks));
        if (tile.RoadLevel > 0 || tile.Improvement != LandImprovement.None)
            effects.Add(new("通行改造", $"步行耗时系数 {GetTerrainMoveCost(x, y):0.00}" + (tile.Improvement == LandImprovement.Bridge ? $"，仅沿{BridgeDirectionName(tile.BridgeDirection)}通行" : ""), ImprovementName(tile.Improvement)));
        foreach (var building in State.Society.Buildings)
            if (building.X == x && building.Y == y) effects.AddRange(GetBuildingEffects(building.Id));
        return effects;
    }
}
