namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    private static double ThirstWorkMultiplier(Resident person)
    {
        return person.Thirst > 80 ? .75 : 1;
    }

    private static double GatheringCondition(Resident person)
    {
        return (person.SicknessTicks > 0 ? .4 : 1)
               * (person.Hunger > 60 ? .55 : 1) * ThirstWorkMultiplier(person);
    }

    private static double LaborCondition(Resident person)
    {
        return (person.SicknessTicks > 0 ? .45 : 1) * ThirstWorkMultiplier(person);
    }

    private double HomeRestMultiplier(Resident person)
    {
        if (!_settlements.TryGetValue(person.SettlementId, out var home) ||
            Distance(person.X, person.Y, home.X, home.Y) > 1) return 1;
        IEnumerable<Building>? buildings = _localWorkQueriesActive
            ? _localWorkBuildings.GetValueOrDefault(home.Id)
            : State.Society.Buildings;
        var bonus = (1 + EffectiveSettlementRank(home) * .1) * GranaryRestBonus(home.Id);
        if (buildings is null) return bonus;
        foreach (var building in buildings)
            if (building.SettlementId == home.Id && building.Kind == BuildingKind.TownCenter && building.Level > 1 &&
                IsSettlementActive(home.Id) && IsFacilityOperating(building))
                return bonus * building.Efficiency;
        return bonus;
    }

    /// <summary>列出居民当前的加成、减益、来源及生效条件。</summary>
    /// <param name="id">居民的稳定 ID。</param>
    public IReadOnlyList<EffectInfo> GetResidentEffects(int id)
    {
        var person = GetResident(id);
        var effects = new List<EffectInfo>();
        if (person is null) return effects;
        if (person.SicknessTicks > 0)
            effects.Add(new EffectInfo("疫病", "采集效率 ×0.40   施工与岗位劳动效率 ×0.45", "", person.SicknessTicks));
        if (person.Hunger > 60) effects.Add(new EffectInfo("饥饿", "现场采集效率 ×0.55；超过 80 时每日生命 -0.30", ""));
        if (person.Thirst > 80) effects.Add(new EffectInfo("缺水", "采集与岗位劳动效率 ×0.75；超过 95 时每日生命 -0.25", ""));
        if (person.Race == RaceKind.Elf) effects.Add(new EffectInfo("精灵采伐", "野外伐木产出 ×1.20", ""));
        if (person.Race == RaceKind.Dwarf) effects.Add(new EffectInfo("矮人采矿", "野外石矿产出 ×1.30", ""));
        var adaptation = RaceTerrainRules.For(person.Race, State.Tiles[Index(person.X, person.Y)].Terrain);
        effects.Add(new EffectInfo("地形适应",
            $"{(adaptation.Habitable ? "宜居" : "不宜居")}   地形移动耗时 ×{adaptation.Movement:0.00}   现场生产 ×{adaptation.Productivity:0.00}",
            "种族与当前地形"));
        effects.Add(new EffectInfo("勤勉", $"野外采集效率 ×{.75 + person.Agent.Personality.Diligence * .5:0.00}", ""));
        if (GatheringTerritoryMultiplier(person, State.Tiles[Index(person.X, person.Y)]) < 1)
            effects.Add(new EffectInfo("领地外采集", $"食物、木材、石矿、矿藏、狩猎、捕鱼与取水速度 ×{OutsideTerritoryGatheringMultiplier:0.00}",
                "资源来源未登记给本城镇"));
        foreach (var town in State.Settlements)
        {
            if (town.NationId != person.NationId || Distance(town.X, town.Y, person.X, person.Y) > 5) continue;
            if (town.ShieldTicks > 0)
                effects.Add(new EffectInfo("护盾", "附近伤害 ×0.60，同类保护取最强", town.Name, town.ShieldTicks));
            if (GetLocalPolicy(town.Id) == PolicyKind.Defense)
                effects.Add(new EffectInfo("防御政策", "附近伤害 ×0.88，与当地护盾相乘", town.Name));
        }

        if (_settlements.TryGetValue(person.SettlementId, out var home))
        {
            var instruction = LatestAgentFact(person.Agent.Memory, AgentFactKind.Policy, home.Id);
            if (instruction?.Value == (int)PolicyKind.FoodSecurity)
                effects.Add(new EffectInfo("已获知粮食政策", $"个人粮食采集倍率 ×{AgentFoodPolicyMultiplier(person):0.00}",
                    home.Name + "实际收到的政策"));
        }

        var resting = HomeRestMultiplier(person);
        if (resting > 1) effects.Add(new EffectInfo("家园休息", $"附近返乡休息恢复 ×{resting:0.00}", "家园的城镇等级、城镇中心与运作粮仓"));
        return effects;
    }

    /// <summary>列出建筑当前的加成、减益和运营限制。</summary>
    /// <param name="id">建筑的稳定 ID。</param>
    public IReadOnlyList<EffectInfo> GetBuildingEffects(int id)
    {
        var building = State.Society.Buildings.FirstOrDefault(b => b.Id == id);
        var effects = new List<EffectInfo>();
        if (building is null) return effects;
        var ground = State.Tiles[Index(building.X, building.Y)];
        var active = building.Kind == BuildingKind.Bridge ? ground.Improvement == LandImprovement.Bridge
            : building.Kind == BuildingKind.MountainPass ? ground.Improvement == LandImprovement.MountainPass
            : IsBuildingOperational(building);
        var source = ""; // 标题已显示所选建筑及等级，效果来源无需重复。
        var factor = building.Efficiency;
        var effect = building.Kind switch
        {
            BuildingKind.Pasture or BuildingKind.Aquaculture =>
                $"养殖上限 {LivestockCapacity(building):0.#}；投喂按存栏量消耗随身粮食与水（每次最多 0.12、0.03），保留至少 2 份繁殖群。连续 30 日无人照料后数量下降",
            BuildingKind.Farm => "耕作收获粮食，装入随身库存后运回；收成受肥力、干旱和农业研究影响" + (factor > 1 ? $"；等级产量倍率 ×{factor:0.00}" : ""),
            BuildingKind.Workshop => "开采邻格实际可采材料（木材、石材、矿石），由工人携带返仓" + (factor > 1 ? $"；等级产量倍率 ×{factor:0.00}" : ""),
            BuildingKind.LumberCamp => "伐木工采收邻格木材，携带返仓" + (factor > 1 ? $"；等级产量倍率 ×{factor:0.00}" : ""),
            BuildingKind.Quarry => "矿工采收邻格石材与矿石，携带返仓" + (factor > 1 ? $"；等级产量倍率 ×{factor:0.00}" : ""),
            BuildingKind.Well => $"供水量 {WellWaterYield(ground):0.###} / 日"
                                 + "\n每次取水至多 1，与本格野外取水共享额度，装入随身库存后运回",
            BuildingKind.Granary => $"本聚落居民在中心 1 格内返乡休息恢复 ×{1 + .1 * building.Level:0.00}；多个粮仓取最高倍率",
            BuildingKind.Housing => $"提供 {20 * building.Level} 人住房",
            BuildingKind.Market => "值守时，集市 3 格内居民可与相距 3 格的人交换已有消息；每次值守消耗仓库粮食 0.01",
            BuildingKind.Watchtower => $"塔 2 格内的同聚落居民，观察火灾范围 3 → {3 + building.Level} 格；无需工作人员",
            BuildingKind.Dock => $"同国舟船 3 格内水上速度 ×{1 + .15 * building.Level:0.00}；多个码头取最高倍率，由相邻岸边的居民值守",
            BuildingKind.Academy => "到场推进已立项的研究，成果可通过消息传授" + (factor > 1 ? $"；等级研究倍率 ×{factor:0.00}" : ""),
            BuildingKind.ArcaneSanctum => "居民到场训练魔法并恢复魔力；天赋至少 25、训练未满 100，每次消耗仓库粮食 0.03" +
                                          (factor > 1 ? $"；等级训练与恢复倍率 ×{factor:0.00}" : ""),
            BuildingKind.Infirmary => $"治疗 3 格内同聚落伤病居民；每单位劳动恢复生命 {0.45 * factor:0.###}、病程减少 1 日，每次消耗仓库粮食 0.05",
            BuildingKind.Bridge =>
                $"仅沿{BridgeDirectionName(building.Direction)}通行，步行耗时系数 {1.2 / factor:0.00}；离自然岸最多 {BridgeShoreLimit(building.Level)} 格",
            BuildingKind.MountainPass => $"山地步行耗时系数 {3.5 / factor:0.00}",
            BuildingKind.TownCenter => "家园仓库与城镇扩充施工地点；居民领取补给、交付物资、交流消息" +
                                       (factor > 1 ? $"；同聚落居民在 1 格内返乡休息恢复 ×{factor:0.00}" : ""),
            BuildingKind.Waystation => $"同国信使在 3 格内速度 ×{1.25 + (building.Level - 1) * .15:0.00}；多个驿站取最高倍率",
            BuildingKind.SignalTower =>
                $"信号接入 {12 + (building.Level - 1) * 4} 格、塔间至多 {24 + (building.Level - 1) * 8} 格（取双方较低等级）；山脉阻挡信号",
            BuildingKind.AssemblyHall => $"人类值守，每单位劳动缓解 2 格内同聚落居民社交需求 {factor:0.00}；3 格内居民交谈距离增至 3 格",
            BuildingKind.TradeGuild => $"人类值守，3 格内本聚落商人移动速度 ×{1.15 * factor:0.00}；3 格内居民交谈距离增至 3 格",
            BuildingKind.SacredGrove =>
                $"精灵现场训练，每单位劳动增加训练 {0.1 * factor:0.###}（另乘魔法训练速率）、恢复魔力 {0.3 * factor:0.###}；天赋至少 25、训练未满 100",
            BuildingKind.HerbGarden => $"精灵治疗 3 格内同聚落伤病居民，每单位劳动恢复生命 {0.6 * factor:0.###}、病程减少 1 日",
            BuildingKind.MiningHall => $"矮人开采邻格石矿，每单位劳动采收 {0.3 * factor:0.###} 份资源，再携带石材、矿石返仓",
            BuildingKind.HuntingCamp => $"兽人狩猎本格食草动物，每单位劳动捕获 {0.25 * factor:0.###} 只，按猎物体型折算食物并携带返仓",
            BuildingKind.WarDrum => $"兽人击鼓，每单位劳动为 2 格内同聚落居民恢复体力 {factor:0.00}，为同国军队恢复士气 {0.3 * factor:0.###}",
            _ => (factor > 1 ? $"等级产出倍率 ×{factor:0.00}\n" : "") + ProductionRecipe(building.Kind),
        };
        if (AdvancementRules.For(building.Kind) is { Magic: true })
            effect += "\n施作者要求：天赋至少 25、训练至少 8，并携带本批原料与所需魔力";
        if (building.Kind == BuildingKind.TownCenter && !IsSettlementActive(building.SettlementId))
            effect =
                $"仓库领取补给、交付物资与建村施工仍可使用；城镇等级及中心等级的休息加成暂停，需独占陆地 {GetSettlementArea(building.SettlementId)}/{SettlementActivationArea} 格";
        if (BuildingRace(building.Kind) is not null && AdvancementRules.For(building.Kind) is null)
        {
            var input = RacialWorkInput(building.Kind);
            effect += "\n每次劳动消耗随身物资：" + string.Join("、", AdvancementRules.Resources.Where(k => input.Get(k) > 0)
                .Select(k => $"{ResourceStock.Name(k)} {input.Get(k):0.###}"));
        }

        effects.Add(new EffectInfo(building.Kind == BuildingKind.Watchtower ? "火情观察" : "作用", effect, source,
            Active: active));
        if (!PassiveFacility(building) &&
            building.Kind is not (BuildingKind.TownCenter or BuildingKind.Bridge or BuildingKind.MountainPass))
            effects.Add(new EffectInfo("岗位容量", $"最多 {building.WorkSlots} 名到场工作人员", source,
                Active: IsBuildingOperational(building)));
        effects.Add(new EffectInfo("建筑耐火", $"着火时每日损失生命 {1.5 * BuildingFlammability(building):0.00}"
                                           + (building.Level < 3 && BuildingFlammability(building) > 0
                                               ? $"；升至 {building.Level + 1} 级后为 {1.5 * BuildingFlammability(building) * .75:0.00}"
                                               : ""), source));
        if (building.Level < 3)
        {
            var next = building.Level + 1;
            var improvement = building.Kind switch
            {
                BuildingKind.Housing => $"住房容量 {20 * next} 人",
                BuildingKind.Granary => $"家园休息恢复 ×{1 + .1 * next:0.00}",
                BuildingKind.Watchtower => $"观察火灾范围 {3 + building.Level} → {3 + next} 格",
                BuildingKind.Dock => $"附近水上舟船速度 ×{1 + .15 * next:0.00}，岗位增加 1",
                BuildingKind.Well => "岗位增加 1；仍共享当地每日供水额度",
                BuildingKind.Market => "岗位增加 1；交谈范围仍为 3 格",
                BuildingKind.Waystation =>
                    $"信使速度 ×{1.25 + (building.Level - 1) * .15:0.00} → ×{1.25 + building.Level * .15:0.00}，岗位增加 1",
                BuildingKind.SignalTower => $"信号接入 {12 + building.Level * 4} 格，塔间 {24 + building.Level * 8} 格，岗位增加 1",
                BuildingKind.Bridge =>
                    $"离岸上限 {BridgeShoreLimit(building.Level)} → {BridgeShoreLimit(next)} 格，步行耗时系数 {1.2 / (1 + building.Level * .25):0.00}",
                BuildingKind.MountainPass => $"步行耗时系数 {3.5 / (1 + building.Level * .25):0.00}",
                BuildingKind.TownCenter => $"返乡休息恢复 ×{1 + building.Level * .25:0.00}",
                BuildingKind.Academy => $"研究效率 ×{1 + building.Level * .25:0.00}，岗位增加 1",
                BuildingKind.TradeGuild => $"本聚落商人速度 ×{1.15 * (1 + building.Level * .25):0.00}，岗位增加 1",
                BuildingKind.AssemblyHall => $"每单位劳动缓解社交需求 {1 + building.Level * .25:0.00}，岗位增加 1",
                BuildingKind.WarDrum =>
                    $"每单位劳动恢复体力 {1 + building.Level * .25:0.00}、士气 {.3 * (1 + building.Level * .25):0.###}，岗位增加 1",
                BuildingKind.ArcaneSanctum or BuildingKind.SacredGrove =>
                    $"训练与魔力恢复倍率 ×{1 + building.Level * .25:0.00}，岗位增加 1",
                BuildingKind.Infirmary or BuildingKind.HerbGarden => $"治疗量倍率 ×{1 + building.Level * .25:0.00}，岗位增加 1",
                BuildingKind.Farm or BuildingKind.Workshop or BuildingKind.LumberCamp or BuildingKind.Quarry
                    or BuildingKind.MiningHall or BuildingKind.HuntingCamp
                    => $"采收量倍率 ×{1 + building.Level * .25:0.00}，岗位增加 1",
                _ =>
                    $"{(AdvancementRules.For(building.Kind) is not null ? "每批产出" : "劳动效果")}倍率 ×{1 + building.Level * .25:0.00}，岗位增加 1",
            };
            effects.Add(new EffectInfo("下一级", $"等级 {next}，{improvement}", "", Active: false));
        }

        if (_settlements.TryGetValue(building.SettlementId, out var town))
        {
            if (building.Kind == BuildingKind.Academy && town.Tier > SettlementTier.Village)
                effects.Add(new EffectInfo("城镇组织",
                    $"本地研究效率 ×{1 + EffectiveSettlementRank(town) * .1:0.00}" +
                    (IsSettlementActive(town.Id) ? "" : "，占地不足，加成暂停"), town.Name, Active: IsSettlementActive(town.Id)));
            if (building.Kind == BuildingKind.Farm && HasResearch(town.Id, ResearchKind.Agriculture))
                effects.Add(new EffectInfo("农业知识", "农场粮食产出 ×1.35", town.Name));

            void Knowledge(ResearchKind kind, string description)
            {
                if (HasResearch(town.Id, kind)) effects.Add(new EffectInfo(ResearchName(kind), description, town.Name));
            }

            if (building.Kind == BuildingKind.Farm || AdvancementRules.For(building.Kind)?.Output == ResourceKind.Food)
                Knowledge(ResearchKind.Irrigation, "粮食实际产出 ×1.25");
            if (building.Kind is BuildingKind.Workshop or BuildingKind.LumberCamp or BuildingKind.Quarry)
                Knowledge(ResearchKind.Forestry, "材料采集效率 ×1.25");
            if (building.Kind == BuildingKind.Infirmary) Knowledge(ResearchKind.Medicine, "现场治疗 ×1.50");
            if (building.Kind == BuildingKind.Academy)
            {
                Knowledge(ResearchKind.ScientificMethod, "研究效率 ×1.25");
                Knowledge(ResearchKind.ArcaneScholarship, "研究效率 ×1.25");
            }

            if (building.Kind == BuildingKind.ArcaneSanctum)
            {
                Knowledge(ResearchKind.ArcaneScholarship, "训练效率 ×1.50");
                Knowledge(ResearchKind.ManaAttunement, "魔力恢复 ×1.50");
            }

            if (building.Kind is BuildingKind.Foundry or BuildingKind.DwarvenForge)
                Knowledge(ResearchKind.EfficientSmelting, "每批合金产出 ×1.25");
            if (building.Kind == BuildingKind.PowerPlant) Knowledge(ResearchKind.EnergyRecycling, "每批动力单元产出 ×1.50");
            if (building.Kind is BuildingKind.Crystallizer or BuildingKind.AetherForge)
                Knowledge(ResearchKind.Leylines, "每批产出 ×1.25");
            if (building.Kind == BuildingKind.Farm && town.FertilityBoostTicks > 0)
                effects.Add(new EffectInfo("丰饶", "农场粮食产出 ×1.35", town.Name, town.FertilityBoostTicks));
            if (building.Kind == BuildingKind.Academy && GetLocalPolicy(town.Id) == PolicyKind.Scholarship)
                effects.Add(new EffectInfo("学术政策", "研究效率 ×1.35", town.Name));
            if (building.Kind == BuildingKind.Farm && GetPolicyProductionMultiplier(town.Id) != 1)
                effects.Add(new EffectInfo("当地生产政策", $"农场产出 ×{GetPolicyProductionMultiplier(town.Id):0.00}",
                    town.Name));
        }

        return effects;
    }

    /// <summary>列出地格当前的环境、资源、灾害和通行效果。</summary>
    /// <param name="x">横向地格坐标。</param>
    /// <param name="y">纵向地格坐标。</param>
    public IReadOnlyList<EffectInfo> GetTileEffects(int x, int y)
    {
        var effects = new List<EffectInfo>();
        if (!InBounds(x, y)) return effects;
        var tile = State.Tiles[Index(x, y)];
        effects.Add(new EffectInfo("地形可燃性", $"{TerrainFlammability(tile):0.00} / 1；受植被、剩余资源、供水与干旱影响", "当地地形；建筑可燃性另计"));
        if (tile.FireTicks > 0) effects.Add(new EffectInfo("燃烧", "停止生产与取水，居民每日灼伤 4", "", tile.FireTicks));
        if (tile.DroughtTicks > 0)
        {
            effects.Add(new EffectInfo("干旱", "野外食物产出 ×0.15   农场粮食 ×0.18"
                                             + (IsWaterTerrain(tile.Terrain) ? "   河湖无限淡水不减少" : "   天然供水 ×0.20"), "",
                tile.DroughtTicks));
        }

        if (tile.RoadLevel > 0 || tile.Improvement != LandImprovement.None)
        {
            effects.Add(new EffectInfo("通行改造",
                (double.IsFinite(GetTerrainMoveCost(x, y))
                    ? $"步行耗时系数 {GetTerrainMoveCost(x, y):0.00}"
                    : "无法步行通行，需桥梁、山路或载具")
                + (tile.Improvement == LandImprovement.Bridge
                    ? $"，仅沿{BridgeDirectionName(tile.BridgeDirection)}通行"
                    : ""), ImprovementName(tile.Improvement)));
        }

        foreach (var building in State.Society.Buildings)
            if (building.X == x && building.Y == y)
            {
                var status = GetBuildingDetailStatus(building.Id);
                if (status.Length > 0) effects.Add(new EffectInfo(BuildingName(building.Kind) + "状态", status, ""));
                effects.AddRange(GetBuildingEffects(building.Id).Select(effect => string.IsNullOrEmpty(effect.Source)
                    ? effect with { Source = BuildingName(building.Kind) + $"（{building.X}, {building.Y}）" }
                    : effect));
            }

        return effects;
    }
}
