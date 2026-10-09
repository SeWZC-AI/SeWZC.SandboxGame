using SeWZC.WorldBox.Core.Runtime;

namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    private static double ThirstWorkMultiplier(double thirst)
    {
        return thirst > 80 ? .75 : 1;
    }

    private static double GatheringCondition(int sickness, double hunger, double thirst)
    {
        return (sickness > 0 ? .4 : 1)
               * (hunger > 60 ? .55 : 1) * ThirstWorkMultiplier(thirst);
    }

    private static double LaborCondition(int sickness, double thirst)
    {
        return (sickness > 0 ? .45 : 1) * ThirstWorkMultiplier(thirst);
    }

    private double HomeRestMultiplier(int settlementId, int x, int y)
    {
        if (!_settlements.TryGetValue(settlementId, out var home) ||
            Distance(x, y, home.Value.X, home.Value.Y) > 1)
            return 1;
        IEnumerable<StateReference<Building>>? buildings = _localWorkQueriesActive
            ? _localWorkBuildings.GetValueOrDefault(home.Value.Id)
            : Current.Buildings;
        var bonus = (1 + EffectiveSettlementRank(home) * .1) * GranaryRestBonus(home.Value.Id);
        if (buildings is null)
            return bonus;
        foreach (var building in buildings)
            if (building.Value.SettlementId == home.Value.Id && building.Value.Kind == BuildingKind.TownCenter && building.Value.Level > 1 &&
                IsSettlementActive(home.Value.Id) && IsFacilityOperating(building.Value))
                return bonus * building.Value.Efficiency;
        return bonus;
    }

    /// <summary>列出居民当前受到的效果。</summary>
    /// <param name="id">居民的稳定 ID。</param>
    public IReadOnlyList<EffectInfo> GetResidentEffects(int id)
    {
        var person = GetResident(id);
        var effects = new List<EffectInfo>();
        if (person is null)
            return effects;
        if (person.SicknessTicks > 0)
            effects.Add(new EffectInfo("疫病", "采集效率 ×0.40   施工与岗位劳动效率 ×0.45", "", person.SicknessTicks));
        if (person.Hunger > 60)
            effects.Add(new EffectInfo("饥饿", "现场采集效率 ×0.55；超过 80 时每日生命 -0.30", ""));
        if (person.Thirst > 80)
            effects.Add(new EffectInfo("缺水", "采集与岗位劳动效率 ×0.75；超过 95 时每日生命 -0.25", ""));
        if (person.Race == RaceKind.Elf)
            effects.Add(new EffectInfo("精灵采伐", "野外伐木产出 ×1.20", ""));
        if (person.Race == RaceKind.Dwarf)
            effects.Add(new EffectInfo("矮人采矿", "野外石矿产出 ×1.30", ""));
        var adaptation = RaceTerrainRules.For(person.Race, Current.Tiles[Index(person.X, person.Y)].Value.Terrain);
        effects.Add(new EffectInfo("地形适应",
            $"{(adaptation.Habitable ? "宜居" : "不宜居")}   地形移动耗时 ×{adaptation.Movement:0.00}   现场生产 ×{adaptation.Productivity:0.00}",
            "种族与当前地形"));
        effects.Add(new EffectInfo("勤勉", $"野外采集效率 ×{.75 + person.Agent.Personality.Diligence * .5:0.00}", ""));
        if (person.Agent.FamiliarTiles.Length > 0)
        {
            effects.Add(new EffectInfo("路线习惯", $"熟悉地格 {person.Agent.FamiliarTiles.Length} 格\n耗时相近时倾向熟路，仍避开阻路和火场",
                "本人的实际到访记录"));
        }

        if (GatheringTerritoryMultiplier(person.SettlementId, person.NationId, Current.Tiles[Index(person.X, person.Y)].Value) < 1)
        {
            effects.Add(new EffectInfo("领地外采集", $"食物、木材、石矿、矿藏、狩猎、捕鱼与取水速度 ×{OutsideTerritoryGatheringMultiplier:0.00}",
                "资源来源未登记给本城镇"));
        }

        foreach (var town in Current.Settlements)
        {
            if (town.Value.NationId != person.NationId || Distance(town.Value.X, town.Value.Y, person.X, person.Y) > 5)
                continue;
            if (town.Value.ShieldTicks > 0)
                effects.Add(new EffectInfo("护盾", "附近伤害 ×0.60，同类保护取最强", town.Value.Name, town.Value.ShieldTicks));
            if (GetLocalPolicy(town.Value.Id) == PolicyKind.Defense)
                effects.Add(new EffectInfo("防御政策", "附近伤害 ×0.88，与当地护盾相乘", town.Value.Name));
        }

        if (_settlements.TryGetValue(person.SettlementId, out var home))
        {
            var instruction = LatestAgentFact(person.Agent.Memory, AgentFactKind.Policy, home.Value.Id);
            if (instruction?.Value == (int)PolicyKind.FoodSecurity)
            {
                effects.Add(new EffectInfo("已获知粮食政策", $"个人粮食采集倍率 ×{AgentFoodPolicyMultiplier(person.Agent, person.SettlementId, person.X, person.Y):0.00}",
                    home.Value.Name + "实际收到的政策"));
            }
        }

        var resting = HomeRestMultiplier(person.SettlementId, person.X, person.Y);
        if (resting > 1)
            effects.Add(new EffectInfo("家园休息", $"附近返乡休息恢复 ×{resting:0.00}", "家园的城镇等级、城镇中心与运作粮仓"));
        return effects;
    }

    /// <summary>列出建筑当前受到的效果和运营限制。</summary>
    /// <param name="id">建筑的稳定 ID。</param>
    public IReadOnlyList<EffectInfo> GetBuildingEffects(int id)
    {
        var building = Current.Buildings.FirstOrDefault(b => b.Value.Id == id);
        var effects = new List<EffectInfo>();
        if (building is null)
            return effects;
        var ground = Current.Tiles[Index(building.Value.X, building.Value.Y)];
        var active = building.Value.Kind == BuildingKind.Bridge ? ground.Value.Improvement == LandImprovement.Bridge
            : building.Value.Kind == BuildingKind.MountainPass ? ground.Value.Improvement == LandImprovement.MountainPass
            : IsBuildingOperational(building.Value);
        var source = ""; // 标题已显示所选建筑及等级，效果来源无需重复。
        var factor = building.Value.Efficiency;
        var effect = building.Value.Kind switch
        {
            BuildingKind.Farm => "耕作收获粮食，装入随身库存后运回；收成受肥力、干旱和农业研究影响" + (factor > 1 ? $"；等级产量倍率 ×{factor:0.00}" : ""),
            BuildingKind.Workshop => "开采邻格实际可采材料（木材、石材、矿石），由工人携带返仓" + (factor > 1 ? $"；等级产量倍率 ×{factor:0.00}" : ""),
            BuildingKind.Academy => "到场推进已立项的研究，成果可通过消息传授" + (factor > 1 ? $"；等级研究倍率 ×{factor:0.00}" : ""),
            BuildingKind.Waystation => $"同国信使在 3 格内速度 ×{1.25 + (building.Value.Level - 1) * .15:0.00}；多个驿站取最高倍率",
            BuildingKind.SignalTower =>
                $"信号接入 {12 + (building.Value.Level - 1) * 4} 格、塔间至多 {24 + (building.Value.Level - 1) * 8} 格（取双方较低等级）；山脉阻挡信号",
            BuildingKind.ArcaneSanctum => "居民到场训练魔法并恢复魔力；天赋至少 25、训练未满 100，每次消耗仓库粮食 0.03" +
                                          (factor > 1 ? $"；等级训练与恢复倍率 ×{factor:0.00}" : ""),
            BuildingKind.Infirmary => $"治疗 3 格内同聚落伤病居民；每单位劳动恢复生命 {0.45 * factor:0.###}；每次治疗病程减少 1 日，每次消耗仓库粮食 0.05",
            BuildingKind.MountainPass => $"山地步行耗时系数 {3.5 / factor:0.00}",
            BuildingKind.Bridge =>
                $"仅沿{BridgeDirectionName(building.Value.Direction)}通行，步行耗时系数 {1.2 / factor:0.00}；离自然岸最多 {BridgeShoreLimit(building.Value.Level)} 格",
            BuildingKind.Dock => $"同国舟船 3 格内水上速度 ×{1 + .15 * building.Value.Level:0.00}；多个码头取最高倍率，由相邻岸边的居民值守",
            BuildingKind.TownCenter => "家园仓库与城镇扩充施工地点；居民领取补给、交付物资、交流消息" +
                                       (factor > 1 ? $"；同聚落居民在 1 格内返乡休息恢复 ×{factor:0.00}" : ""),
            BuildingKind.LumberCamp => "伐木工采收邻格木材，携带返仓" + (factor > 1 ? $"；等级产量倍率 ×{factor:0.00}" : ""),
            BuildingKind.Quarry => "矿工采收邻格石材与矿石，携带返仓" + (factor > 1 ? $"；等级产量倍率 ×{factor:0.00}" : ""),
            BuildingKind.Well => $"每日可打水量 {WellWaterYield(ground.Value):0.###}"
                                 + "\n每次取水至多 1，所有取水者共享水井日额度，装入随身库存后运回",
            BuildingKind.Granary => $"本聚落居民在中心 1 格内返乡休息恢复 ×{1 + .1 * building.Value.Level:0.00}；多个粮仓取最高倍率",
            BuildingKind.Housing => $"提供 {HousingCapacityPerLevel * building.Value.Level} 人住房",
            BuildingKind.Market => "值守时，集市 3 格内居民可与相距 3 格的人交换已有消息；每次值守消耗仓库粮食 0.01",
            BuildingKind.Watchtower => $"塔 2 格内的同聚落居民，观察火灾范围 3 至 {3 + building.Value.Level} 格；无需工作人员",
            BuildingKind.AssemblyHall => $"人类值守，每单位劳动缓解 2 格内同聚落居民社交需求 {factor:0.00}；3 格内居民交谈距离增至 3 格",
            BuildingKind.TradeGuild => $"人类值守，3 格内本聚落商人移动速度 ×{1.15 * factor:0.00}；3 格内居民交谈距离增至 3 格",
            BuildingKind.SacredGrove =>
                $"精灵现场训练，每单位劳动增加训练 {0.1 * factor:0.###}（另乘魔法训练速率）、恢复魔力 {0.3 * factor:0.###}；天赋至少 25、训练未满 100",
            BuildingKind.HerbGarden => $"精灵治疗 3 格内同聚落伤病居民，每单位劳动恢复生命 {0.6 * factor:0.###}；每次治疗病程减少 1 日",
            BuildingKind.MiningHall => $"矮人开采邻格石矿，每单位劳动采收 {0.3 * factor:0.###} 份资源，再携带石材、矿石返仓",
            BuildingKind.HuntingCamp => $"兽人狩猎本格食草动物，每单位劳动捕获 {0.25 * factor:0.###} 只，按猎物体型折算食物并携带返仓",
            BuildingKind.WarDrum => $"兽人击鼓，每单位劳动为 2 格内同聚落居民恢复体力 {factor:0.00}，为同国军队恢复士气 {0.3 * factor:0.###}",
            BuildingKind.Pasture or BuildingKind.Aquaculture =>
                $"养殖上限 {LivestockCapacity(building.Value):0.#}；投喂按存栏量消耗随身粮食与水（每次最多 0.12、0.03），保留至少 2 份繁殖群。连续 {SimulationTime.DaysPerMonth} 日无人照料后数量下降",
            _ => (factor > 1 ? $"等级产出倍率 ×{factor:0.00}\n" : "") + ProductionRecipe(building.Value.Kind),
        };
        if (ProductionRules.For(building.Value.Kind) is { Research.Magic: true })
            effect += "\n施作者要求：天赋至少 25、训练至少 8，并携带本批原料与所需魔力";
        if (building.Value.Kind == BuildingKind.TownCenter && !IsSettlementActive(building.Value.SettlementId))
        {
            effect =
                $"仓库领取补给、交付物资与建村施工仍可使用；城镇等级及中心等级的休息加成暂停，需独占陆地 {GetSettlementArea(building.Value.SettlementId)}/{SettlementActivationArea} 格";
        }

        if (BuildingRace(building.Value.Kind) is not null && ProductionRules.For(building.Value.Kind) is null)
        {
            var input = RacialWorkInput(building.Value.Kind);
            effect += "\n每次劳动消耗随身物资：" + string.Join("、", ResourceStock.Kinds.Where(k => input.Get(k) > 0)
                .Select(k => $"{ResourceStock.Name(k)} {input.Get(k):0.###}"));
        }

        effects.Add(new EffectInfo(building.Value.Kind == BuildingKind.Watchtower ? "火情观察" : "作用", effect, source,
            Active: active));
        if (!PassiveFacility(building.Value) &&
            building.Value.Kind is not (BuildingKind.MountainPass or BuildingKind.Bridge or BuildingKind.TownCenter))
        {
            effects.Add(new EffectInfo("岗位容量", $"最多 {building.Value.WorkSlots} 名到场工作人员", source,
                Active: IsBuildingOperational(building.Value)));
        }

        effects.Add(new EffectInfo("建筑耐火", $"着火时每 tick 损失生命 {1.5 * BuildingFlammability(building.Value):0.00}"
                                           + (building.Value.Level < 3 && BuildingFlammability(building.Value) > 0
                                               ? $"；升至 {building.Value.Level + 1} 级后为 {1.5 * BuildingFlammability(building.Value) * .75:0.00}"
                                               : ""), source));
        if (building.Value.Level < 3)
        {
            var next = building.Value.Level + 1;
            var improvement = building.Value.Kind switch
            {
                BuildingKind.Farm or BuildingKind.Workshop or BuildingKind.LumberCamp or BuildingKind.Quarry
                    or BuildingKind.MiningHall or BuildingKind.HuntingCamp
                    => $"采收量倍率 ×{1 + building.Value.Level * .25:0.00}，岗位增加 1",
                BuildingKind.Academy => $"研究效率 ×{1 + building.Value.Level * .25:0.00}，岗位增加 1",
                BuildingKind.Waystation =>
                    $"信使速度 ×{1.25 + (building.Value.Level - 1) * .15:0.00} 至 ×{1.25 + building.Value.Level * .15:0.00}，岗位增加 1",
                BuildingKind.SignalTower => $"信号接入 {12 + building.Value.Level * 4} 格，塔间 {24 + building.Value.Level * 8} 格，岗位增加 1",
                BuildingKind.ArcaneSanctum or BuildingKind.SacredGrove =>
                    $"训练与魔力恢复倍率 ×{1 + building.Value.Level * .25:0.00}，岗位增加 1",
                BuildingKind.Infirmary or BuildingKind.HerbGarden => $"治疗量倍率 ×{1 + building.Value.Level * .25:0.00}，岗位增加 1",
                BuildingKind.MountainPass => $"步行耗时系数 {3.5 / (1 + building.Value.Level * .25):0.00}",
                BuildingKind.Bridge =>
                    $"离岸上限 {BridgeShoreLimit(building.Value.Level)} 至 {BridgeShoreLimit(next)} 格，步行耗时系数 {1.2 / (1 + building.Value.Level * .25):0.00}",
                BuildingKind.Dock => $"附近水上舟船速度 ×{1 + .15 * next:0.00}，岗位增加 1",
                BuildingKind.TownCenter => $"返乡休息恢复 ×{1 + building.Value.Level * .25:0.00}",
                BuildingKind.Well => "岗位增加 1；仍共享水井每日可打水额度",
                BuildingKind.Granary => $"家园休息恢复 ×{1 + .1 * next:0.00}",
                BuildingKind.Housing => $"住房容量 {HousingCapacityPerLevel * next} 人",
                BuildingKind.Market => "岗位增加 1；交谈范围仍为 3 格",
                BuildingKind.Watchtower => $"观察火灾范围 {3 + building.Value.Level} 至 {3 + next} 格",
                BuildingKind.AssemblyHall => $"每单位劳动缓解社交需求 {1 + building.Value.Level * .25:0.00}，岗位增加 1",
                BuildingKind.TradeGuild => $"本聚落商人速度 ×{1.15 * (1 + building.Value.Level * .25):0.00}，岗位增加 1",
                BuildingKind.WarDrum =>
                    $"每单位劳动恢复体力 {1 + building.Value.Level * .25:0.00}、士气 {.3 * (1 + building.Value.Level * .25):0.###}，岗位增加 1",
                _ =>
                    $"{(ProductionRules.For(building.Value.Kind) is not null ? "每批产出" : "劳动效果")}倍率 ×{1 + building.Value.Level * .25:0.00}，岗位增加 1",
            };
            effects.Add(new EffectInfo("下一级", $"等级 {next}，{improvement}", "", Active: false));
        }

        if (_settlements.TryGetValue(building.Value.SettlementId, out var town))
        {
            if (building.Value.Kind == BuildingKind.Academy && town.Value.Tier > SettlementTier.Village)
            {
                effects.Add(new EffectInfo("城镇组织",
                    $"本地研究效率 ×{1 + EffectiveSettlementRank(town) * .1:0.00}" +
                    (IsSettlementActive(town.Value.Id) ? "" : "，占地不足，加成暂停"), town.Value.Name, Active: IsSettlementActive(town.Value.Id)));
            }

            if (building.Value.Kind == BuildingKind.Farm && HasResearch(town.Value.Id, Advancement.Agriculture))
                effects.Add(new EffectInfo("农业知识", "农场粮食产出 ×1.35", town.Value.Name));

            void Knowledge(Advancement kind, string description)
            {
                if (HasResearch(town.Value.Id, kind))
                    effects.Add(new EffectInfo(kind.Name, description, town.Value.Name));
            }

            if (building.Value.Kind == BuildingKind.Farm || ProductionRules.For(building.Value.Kind)?.Output == ResourceKind.Food)
                Knowledge(Advancement.Irrigation, "粮食实际产出 ×1.25");
            if (building.Value.Kind is BuildingKind.Workshop or BuildingKind.LumberCamp or BuildingKind.Quarry)
                Knowledge(Advancement.Forestry, "材料采集效率 ×1.25");
            if (building.Value.Kind == BuildingKind.Infirmary)
                Knowledge(Advancement.Medicine, "现场治疗 ×1.50");
            if (building.Value.Kind == BuildingKind.Academy)
            {
                Knowledge(Advancement.ScientificMethod, "研究效率 ×1.25");
                Knowledge(Advancement.ArcaneScholarship, "研究效率 ×1.25");
            }

            if (building.Value.Kind == BuildingKind.ArcaneSanctum)
            {
                Knowledge(Advancement.ArcaneScholarship, "训练效率 ×1.50");
                Knowledge(Advancement.ManaAttunement, "魔力恢复 ×1.50");
            }

            if (building.Value.Kind is BuildingKind.Foundry or BuildingKind.DwarvenForge)
                Knowledge(Advancement.EfficientSmelting, "每批合金产出 ×1.25");
            if (building.Value.Kind == BuildingKind.PowerPlant)
                Knowledge(Advancement.EnergyRecycling, "每批动力单元产出 ×1.50");
            if (building.Value.Kind is BuildingKind.Crystallizer or BuildingKind.AetherForge)
                Knowledge(Advancement.Leylines, "每批产出 ×1.25");
            if (building.Value.Kind == BuildingKind.Farm && town.Value.FertilityBoostTicks > 0)
                effects.Add(new EffectInfo("丰饶", "农场粮食产出 ×1.35", town.Value.Name, town.Value.FertilityBoostTicks));
            if (building.Value.Kind == BuildingKind.Academy && GetLocalPolicy(town.Value.Id) == PolicyKind.Scholarship)
                effects.Add(new EffectInfo("学术政策", "研究效率 ×1.35", town.Value.Name));
            if (building.Value.Kind == BuildingKind.Farm && GetPolicyProductionMultiplier(town.Value.Id) != 1)
            {
                effects.Add(new EffectInfo("当地生产政策", $"农场产出 ×{GetPolicyProductionMultiplier(town.Value.Id):0.00}",
                    town.Value.Name));
            }
        }

        return effects;
    }

    /// <summary>列出地格当前的环境效果。</summary>
    /// <param name="x">横向地格坐标。</param>
    /// <param name="y">纵向地格坐标。</param>
    public IReadOnlyList<EffectInfo> GetTileEffects(int x, int y)
    {
        var effects = new List<EffectInfo>();
        if (!InBounds(x, y))
            return effects;
        var tile = Current.Tiles[Index(x, y)];
        effects.Add(new EffectInfo("地形可燃性", $"{TerrainFlammability(tile.Value):0.00} / 1；受植被、剩余资源、供水与干旱影响", "当地地形；建筑可燃性另计"));
        if (tile.Value.FireTicks > 0)
            effects.Add(new EffectInfo("燃烧", "停止生产与取水，居民每 tick 灼伤 4", "", tile.Value.FireTicks));
        if (tile.Value.DroughtTicks > 0)
        {
            effects.Add(new EffectInfo("干旱", "野外食物产出 ×0.15   农场粮食 ×0.18"
                                             + (IsWaterTerrain(tile.Value.Terrain) ? "   河湖无限淡水不减少" : "   天然供水 ×0.20"), "",
                tile.Value.DroughtTicks));
        }

        if (tile.Value.RoadLevel > 0 || tile.Value.Improvement != LandImprovement.None)
        {
            effects.Add(new EffectInfo("通行改造",
                (double.IsFinite(GetTerrainMoveCost(x, y))
                    ? $"步行耗时系数 {GetTerrainMoveCost(x, y):0.00}"
                    : "无法步行通行，需桥梁、山路或载具")
                + (tile.Value.Improvement == LandImprovement.Bridge
                    ? $"，仅沿{BridgeDirectionName(tile.Value.BridgeDirection)}通行"
                    : ""), ImprovementName(tile.Value.Improvement)));
        }

        foreach (var building in Current.Buildings)
            if (building.Value.X == x && building.Value.Y == y)
            {
                var status = GetBuildingDetailStatus(building.Value.Id);
                if (status.Length > 0)
                    effects.Add(new EffectInfo(BuildingName(building.Value.Kind) + "状态", status, ""));
                effects.AddRange(GetBuildingEffects(building.Value.Id).Select(effect => string.IsNullOrEmpty(effect.Source)
                    ? effect with { Source = BuildingName(building.Value.Kind) + $"（{building.Value.X}, {building.Value.Y}）" }
                    : effect));
            }

        return effects;
    }
}
