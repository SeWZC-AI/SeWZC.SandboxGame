namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    /// <summary>返回职业的中文名称。</summary>
    /// <param name="job">职业。</param>
    public static string ProfessionName(Profession job)
    {
        return job switch
        {
            Profession.Child => "孩童",
            Profession.Farmer => "农民",
            Profession.Lumberjack => "伐木工",
            Profession.Miner => "矿工",
            Profession.Soldier => "战士",
            Profession.Builder => "建造者",
            Profession.Trader => "商人",
            Profession.Messenger => "信使",
            Profession.Representative => "代表",
            Profession.Scholar => "学者",
            Profession.Mage => "法师",
            Profession.Fisher => "渔民",
            Profession.Engineer => "工程师",
            Profession.Physician => "医师",
            Profession.Firefighter => "消防员",
            Profession.Ranger => "游击射手",
            Profession.Archivist => "文献师",
            Profession.Battlemage => "战斗法师",
            Profession.Surveyor => "测绘员",
            Profession.Gardener => "园艺师",
            Profession.Laborer => "劳动者",
            _ => "未知职业",
        };
    }

    /// <summary>按观察者显示策略、发现标记和已有研究判断矿藏是否可见。</summary>
    /// <param name="tile">要检查矿藏可见性的地格。</param>
    /// <param name="visibility">观察者的矿藏显示策略。</param>
    public bool IsDepositVisible(Tile tile, ResourceVisibility visibility)
    {
        return tile.Deposit is { } kind
               && (visibility == ResourceVisibility.All || (visibility == ResourceVisibility.Researched
                                                            && (tile.DepositDiscovered || (DepositResearch(kind) is
                                                                    { } research &&
                                                                Society.Research.Any(r =>
                                                                    r.Completed.Contains(research))))));
    }

    /// <summary>返回建筑的实际用途和运营条件说明。</summary>
    /// <param name="kind">设施类别。</param>
    public static string BuildingDescription(BuildingKind kind)
    {
        return kind switch
        {
            BuildingKind.Pasture => "农民在牧场驯养当地草食动物，携带饲料和饮水到场；保留繁殖群，成熟后产出粮食。干旱或草场耗尽时停工。",
            BuildingKind.Aquaculture => "渔民从相邻河湖取得实际鱼群，投喂、繁殖与收获；需要工业和运输知识、饲料、饮水与现场劳动。",
            >= BuildingKind.Reservoir when ProductionRules.For(kind) is null =>
                ResearchRules.Unlocking(kind)?.Effect ?? "",
            BuildingKind.Farm => "居民到场耕作，将粮食装入随身库存并运回家园。肥力、干旱、农业研究和政策影响收成。",
            BuildingKind.Workshop => "为附近伐木和采矿提供劳动岗位，需要附近存在实际可采材料；材料由劳动者随身携带并运回。",
            BuildingKind.Academy => "到场学者推进当地已经立项的研究。研究需预付材料并满足前置知识，成果通过消息传播。",
            BuildingKind.Waystation => "到场人员值守的交通驿站，改善附近信使行程；每次工作消耗少量当地粮食。",
            BuildingKind.SignalTower => "无线通信设施，需要电气化、信号网络和在场值守人员。接入距离 12 格，塔间 24 格，山脉阻挡信号。",
            BuildingKind.ArcaneSanctum => "具备天赋的居民到场训练魔法，消耗粮食并受开放魔法发展规则限制。",
            BuildingKind.Infirmary => "工作人员在现场治疗附近受伤或患病居民，治疗受粮食和实际距离限制。",
            BuildingKind.MountainPass => "完工后开放山地步行通道，山路仍比平地耗时，连接相邻可通行道路才能使用。",
            BuildingKind.Bridge => "只允许沿左右或上下方向通行。各等级有离自然岸距离上限，同向逐段施工，不能借桥段重置离岸距离。可升级或安排改向。",
            BuildingKind.Dock => "在近岸水中提供舟船交通服务。居民从陆岸值守；同国舟船在 3 格内的水上速度每级提高 15%，不叠加多个码头。",
            BuildingKind.TownCenter =>
                "聚落的公共中心与家园粮仓。居民在附近领取口粮、交付采收、交流消息；代表在此汇集诉求。也是付费城镇扩充的施工地点，建筑等级与村、镇、城等级独立。定居时建立，受损后可由居民重建。",
            BuildingKind.Shipyard => "在近岸水中建造舟船。居民从相邻自然陆岸施工，实地取木材、加工，再携带舟船返仓；需要驿路运输知识。",
            BuildingKind.LumberCamp => "设在森林边缘，伐木工到场开采相邻实际木材，随身运回家园；资源耗尽时停工。",
            BuildingKind.Quarry => "设在山地或丘陵矿区旁，矿工到场开采相邻石材和矿石，随身运回家园；资源耗尽时停工。",
            BuildingKind.Well => "设在供水充足的陆地。地块供水量决定每日可打水量，居民与工人到井边取水，共享水井日额度并携带返仓；干旱会减少井水。",
            BuildingKind.Granary => "居民返乡休息恢复每级提高 10%；多个粮仓取最高倍率。",
            BuildingKind.Housing => $"每级增加 {HousingCapacityPerLevel} 人住房容量。",
            BuildingKind.Market => "人员到场值守后，在集市 3 格内的居民可与最多相距 3 格的人交换已有消息；消耗少量当地粮食。",
            BuildingKind.Watchtower => "塔 2 格内的同聚落居民观察火灾范围增加：1／2／3 级分别为 4／5／6 格；无需工作人员。",
            BuildingKind.AssemblyHall => "人类议事厅：当地有成年的人类可建。人类到场携带粮水值守，为两格内居民缓解社交需求，并为附近居民提供三格当面交流范围。",
            BuildingKind.TradeGuild => "人类商贸公会：人类携带粮水到场值守，为三格内本地商人提高行走速度 15%，并提供三格当面交流范围。升级提高服务效率。",
            BuildingKind.SacredGrove => "精灵圣林：保留森林，需要奥术基础和开放魔法规则。精灵携带粮水到场训练，每单位劳动提高训练 0.1、恢复魔力 0.3。",
            BuildingKind.HerbGarden => "精灵草药园：精灵携带粮水到场，为附近实际患者治疗，每单位劳动恢复生命 0.6，每次治疗减少一日病程。",
            BuildingKind.DwarvenForge => "矮人锻炉：当地有成年矮人且掌握工业冶炼与前置知识。矮人实际从仓库领取木材 2、矿石 2，到场每批锻造合金 1.5，再亲自返仓。",
            BuildingKind.MiningHall => "矮人矿业工坊：矮人携带口粮到场，每单位劳动采收相邻真实石矿储量 0.3，携带石材和矿石返仓。",
            BuildingKind.HuntingCamp => "兽人狩猎营：兽人携带口粮到场，每单位劳动捕获本格食草动物 0.25，食物按猎物体型折算，实际减少动物并携带返仓。",
            BuildingKind.WarDrum => "兽人战鼓营：兽人携带粮水到场，为两格内同聚落居民恢复体力，为两格内同国军队恢复士气。",
            _ => "居民从家园领取实际原料，抵达设施加工，产物随身带回。\n" + ProductionRecipe(kind),
        };
    }

    /// <summary>返回建筑的详细状态说明。</summary>
    /// <param name="id">建筑的稳定 ID。</param>
    public string GetBuildingDetailStatus(int id)
    {
        var b = Buildings.FirstOrDefault(building => building.Value.Id == id);
        if (b is null)
            return "建筑已被移除";
        var tile = Tiles[Index(b.Value.X, b.Value.Y)];
        if (b.Value.Kind is BuildingKind.MountainPass or BuildingKind.Bridge
            && tile.Value.Improvement ==
            (b.Value.Kind == BuildingKind.Bridge ? LandImprovement.Bridge : LandImprovement.MountainPass))
            return b.Value.IsUpgrading ? "施工期间保留原通道，完工后更新通行效果" : "";
        if (!BuildingGroundOwned(b.Value))
            return "暂停：所在地已脱离城镇占领区域，重新连接领地后可恢复运营";
        if (!b.Value.IsCompleted)
            return $"施工进度 {b.Value.ConstructionProgress:0.#} / {b.Value.ConstructionRequired:0}，等待居民到场施工";
        if (b.Value.Health < 50)
            return "暂停：生命低于 50，修复至至少 50 后恢复功能";
        if (!b.Value.Enabled)
            return "已停用；点击“恢复运营”重新启用";
        if (b.Value.IsUpgrading)
            return "升级施工中，暂停原有功能";
        if (tile.Value.FireTicks > 0)
            return $"暂停：所在地着火，剩余 {tile.Value.FireTicks / (double)SimulationTime.TicksPerDay:0.##} 日；可安排居民灭火";
        if (!BuildingTerrainValid(b.Value.Kind, tile.Value))
            return "暂停：地形已改变，不再适合此设施；请恢复原地形或换址建设";
        if (b.Value.Kind == BuildingKind.Well && WellWaterYield(tile.Value) <= 0)
            return "暂停：当地供水不足，水井无法出水";
        var town = RequireTown(b.Value.SettlementId);
        if (!CanBuildRacialFacility(town.Value.Id, b.Value.Kind))
            return $"暂停：本聚落没有存活的成年{RaceNames[(int)BuildingRace(b.Value.Kind)!.Value]}";
        if (ProductionRules.For(b.Value.Kind) is { } recipe)
        {
            var missingKnowledge = recipe.Research.Prerequisites.Append(recipe.Research).Distinct()
                .Where(k => !HasResearch(town.Value.Id, k)).Select(research => research.Name).ToArray();
            if (missingKnowledge.Length > 0)
                return "缺少本地研究：" + string.Join("、", missingKnowledge);
            if (ProductionYield(b.Value, recipe) <= 0)
                return "无法产粮：土地肥力为 0";
            var missing = MissingResources(town.Value.Resources, recipe.Input);
            return $"累计加工 {b.Value.ProductionBatches} 批" + (missing is null ? "" : "\n仓库原料：" + missing + "；已携带原料的工人仍可加工");
        }

        if (b.Value.Kind == BuildingKind.SignalTower)
        {
            var missing = new[] { Advancement.Electrification, Advancement.SignalNetwork }
                .Where(k => !HasResearch(town.Value.Id, k)).Select(research => research.Name).ToArray();
            if (missing.Length > 0)
                return "缺少本地研究：" + string.Join("、", missing);
        }

        if (IsHusbandry(b.Value.Kind))
            return GetProductionStatus(b.Value.Id);
        if (b.Value.Kind >= BuildingKind.Reservoir)
            return ExpansionFacilityStatus(b.Value);
        if (b.Value.Kind == BuildingKind.Academy)
        {
            var research = Society.Research.First(r => r.SettlementId == town.Value.Id);
            return research.ActiveProject is { } project
                ? $"研究：{project.Name}\n进度 {research.Progress:0.#} / {research.RequiredProgress:0}"
                : "尚未立项；到聚落的研究页面选择项目并投入材料";
        }

        if (b.Value.Kind == BuildingKind.TownCenter)
            return town.Value.IsExpanding ? $"城镇扩充进度 {town.Value.ExpansionProgress:0.#} / {town.Value.ExpansionRequired:0}" : "";
        if (PassiveFacility(b.Value))
            return "";
        if (b.Value.Kind is BuildingKind.ArcaneSanctum or BuildingKind.SacredGrove && !Society.MagicEnabled)
            return "暂停训练：世界规则已关闭新的魔法发展";
        if (b.Value.Kind == BuildingKind.SacredGrove && !IsForestTerrain(tile.Value.Terrain))
            return "暂停训练：圣林须位于森林、疏林或雨林";
        if (b.Value.Kind == BuildingKind.Well && AvailableWater(b.Value.X, b.Value.Y) <= 0)
            return "今日可打水量已用完，次日恢复额度";
        if (b.Value.Kind == BuildingKind.LumberCamp && FindWorkshopResource(b.Value, Profession.Lumberjack) < 0)
            return "附近木材已采尽";
        if (b.Value.Kind == BuildingKind.Workshop && FindWorkshopResource(b.Value, Profession.Lumberjack) < 0 &&
            FindWorkshopResource(b.Value, Profession.Miner) < 0)
            return "邻格没有可采木材、石材或矿石";
        if (b.Value.Kind is BuildingKind.Quarry or BuildingKind.MiningHall && FindWorkshopResource(b.Value, Profession.Miner) < 0)
            return "附近石材与矿石已采尽";
        if (b.Value.Kind == BuildingKind.HuntingCamp && !WildlifeSiteProductive(tile, false))
            return "本格猎物稀少，等待种群恢复或另寻猎场";
        if (b.Value.Kind is BuildingKind.Infirmary or BuildingKind.HerbGarden && FindLocalWorkPatient(b.Value, true) is null)
            return "3 格内没有需要治疗的同聚落居民";
        if (b.Value.Kind is BuildingKind.Waystation or BuildingKind.SignalTower or BuildingKind.Dock or BuildingKind.Market &&
            town.Value.Resources.Food < .01)
            return $"值守缺粮：仓库粮食 {town.Value.Resources.Food:0.###}，每次需要 0.01";
        if (b.Value.Kind == BuildingKind.ArcaneSanctum && town.Value.Resources.Food < .03)
            return $"训练缺粮：仓库粮食 {town.Value.Resources.Food:0.###}，每次需要 0.03";
        if (b.Value.Kind == BuildingKind.Infirmary && town.Value.Resources.Food < .05)
            return $"治疗缺粮：仓库粮食 {town.Value.Resources.Food:0.###}，每次需要 0.05";
        return IsFacilityOperating(b.Value) ? "" : "暂无在场工作人员";
    }

    /// <summary>设置建筑是否允许运营，并记录玩家干预。</summary>
    /// <param name="id">建筑的稳定 ID。</param>
    /// <param name="enabled">是否允许运营该建筑。</param>
    public void SetBuildingEnabled(int id, bool enabled)
    {
        var building = Buildings.FirstOrDefault(b => b.Value.Id == id) ??
                       throw new ArgumentException("建筑已不存在。");
        if (building.Value.Kind == BuildingKind.TownCenter)
            throw new InvalidOperationException("城镇中心是公共家园，不能停用。");
        building.Replace(building.Value with { Enabled = enabled });
        building.Replace(building.Value with { Workers = building.Value.Workers.Clear() });
        if (building.Value.Kind == BuildingKind.Housing)
        {
            AssignResidentHomes();
            SynchronizeResidentRescues();
        }
        AddEvent(WorldEventKind.Editor, $"玩家{(enabled ? "启用" : "停用")}{BuildingName(building.Value.Kind)}。", building.Value.X,
            building.Value.Y);
    }

    /// <summary>直接恢复建筑生命，可选择同时赐予建造完工。</summary>
    /// <param name="id">建筑的稳定 ID。</param>
    /// <param name="finish">是否同时将建造进度设为完工；升级项目仍按原状态保留。</param>
    public void RestoreBuilding(int id, bool finish = false)
    {
        var building = Buildings.FirstOrDefault(b => b.Value.Id == id) ??
                       throw new ArgumentException("建筑已不存在。");
        building.Replace(building.Value with { Health = 100 });
        if (finish)
        {
            building.Replace(building.Value with { ConstructionProgress = building.Value.ConstructionRequired });
            CompleteLandImprovement(building.Value);
        }

        AddEvent(WorldEventKind.Editor, $"玩家{(finish ? "赐予完工" : "修复")}{BuildingName(building.Value.Kind)}。", building.Value.X,
            building.Value.Y);
        if (building.Value.Kind == BuildingKind.Housing)
            AssignResidentHomes();
    }

    /// <summary>返回居民当前任务的说明。</summary>
    /// <param name="id">居民的稳定 ID。</param>
    public string GetResidentTaskSummary(int id)
    {
        var person = GetResident(id);
        if (person is null || person.Health <= 0)
            return "已离世，保留生平记录";
        var goal = person.Agent.Goal;
        var building = goal.Kind is AgentGoalKind.Work or AgentGoalKind.Study or AgentGoalKind.TrainMagic
            ? FindBuilding(goal.TargetEntityId)
            : null;
        var home = _settlements.GetValueOrDefault(person.SettlementId);
        var dwelling = ResidentHome(person);
        var homeTarget = dwelling is not null && goal.TargetX == dwelling.Value.X && goal.TargetY == dwelling.Value.Y;
        var destination = _settlements.GetValueOrDefault(goal.TargetSettlementId)?.Value.Name ?? "目标聚落";
        if (building is not null)
        {
            var name = BuildingName(building.Value.Kind);
            if (!building.Value.IsCompleted)
                return "建造" + name;
            if (building.Value.IsUpgrading)
                return building.Value.PendingDirection.HasValue ? "改造桥梁方向" : "升级" + name;
            if (building.Value.Kind == BuildingKind.TownCenter)
                return "在城镇中心扩充为" + (home is null ? "下一等级" : SettlementTierName(home.Value.Tier + 1));
            if (ProductionRules.For(building.Value.Kind) is { } recipe)
                return "在" + name + "生产" + ResourceStock.Name(recipe.Output);
            return building.Value.Kind switch
            {
                BuildingKind.Farm => "在农田耕作并收获粮食",
                BuildingKind.Workshop or BuildingKind.LumberCamp or BuildingKind.Quarry or BuildingKind.MiningHall =>
                    "在" + name + (person.Profession == Profession.Lumberjack ? "采伐木材" : "采收石材与矿石"),
                BuildingKind.Academy => "在学舍研究" +
                                        (Society.Research
                                            .FirstOrDefault(r => r.SettlementId == building.Value.SettlementId)
                                            ?.ActiveProject is { } research
                                            ? research.Name
                                            : "待立项课题"),
                BuildingKind.Waystation or BuildingKind.SignalTower or BuildingKind.Dock => "值守" + name + "，支持实际交通与通信",
                BuildingKind.ArcaneSanctum or BuildingKind.SacredGrove => "训练魔法并恢复魔力",
                BuildingKind.Infirmary or BuildingKind.HerbGarden => "治疗附近同聚落伤病居民",
                BuildingKind.Well => "在水井打水并运回家园",
                BuildingKind.Market or BuildingKind.TradeGuild => "值守" + name + "，协助附近居民交流与贸易",
                BuildingKind.AssemblyHall => "组织附近居民交流，缓解社交需求",
                BuildingKind.HuntingCamp => "在狩猎营捕获食草动物",
                BuildingKind.WarDrum => "击鼓恢复附近居民体力与军队士气",
                _ => "在" + name + "工作",
            };
        }

        return goal.Kind switch
        {
            AgentGoalKind.Eat => "返回家园领取口粮和饮水",
            AgentGoalKind.Gather => "采集浆果、草籽或嫩叶并带回家园",
            AgentGoalKind.Work => person.Profession == Profession.Lumberjack ? "采伐木材并带回家园" :
                person.Profession == Profession.Miner ? "开采石矿或已发现矿藏并带回家园" : "采收粮食并带回家园",
            AgentGoalKind.Rest => FindBuilding(goal.TargetEntityId) is { } restFacility
                && goal.TargetX == restFacility.Value.X && goal.TargetY == restFacility.Value.Y
                ? "前往" + BuildingName(restFacility.Value.Kind) + "休养"
                : homeTarget ? "返回家园休息恢复体力" : "前往指定地点休息恢复体力",
            AgentGoalKind.Flee => "离开附近危险区域",
            AgentGoalKind.Socialize => "在家园与居民当面交流消息",
            AgentGoalKind.DeliverMessage => "将携带的消息实际送达" + destination,
            AgentGoalKind.Trade => "前往" + destination + "交换并交付货物",
            AgentGoalKind.Petition => "将已有诉求报告递送至" + destination,
            AgentGoalKind.Study => "寻找可参与的研究课题",
            AgentGoalKind.TrainMagic => "寻找可参与的魔法训练",
            AgentGoalKind.March => "执行实际收到的军令",
            AgentGoalKind.ReturnHome => home?.Value.FoundationPending == true ? "携带建村物资抵达新家园并驻留建村" : "返回家园交付产物并补充粮水",
            AgentGoalKind.Migrate => "步行迁居至" + destination,
            AgentGoalKind.Explore => person.Profession is Profession.Trader or Profession.Messenger
                or Profession.Representative
                ? "勘察聚落与可通行路线"
                : "勘察本职可采材料",
            AgentGoalKind.ClaimLand => "到场驻留登记" + (home?.Value.Name ?? "家园") + "的相邻领地",
            AgentGoalKind.FetchWater => goal.TargetEntityId > 0 ? "到已发现的水源打水并带回家园" : "实地勘察可用水源",
            AgentGoalKind.Hunt => "狩猎可食动物并带回家园",
            AgentGoalKind.Fish => person.TravelMode == TravelMode.Boat ? "乘舟捕鱼并带回鱼获与舟船" : "到鱼群附近捕鱼并带回家园",
            AgentGoalKind.ExtinguishFire => "携带实际饮水扑灭附近火势",
            AgentGoalKind.Rescue => "将" + (GetResident(goal.TargetEntityId)?.Name ?? "需要帮助的居民") + "背负送回分配的住宅",
            AgentGoalKind.Sleep => goal.PlayerDirected && SimulationTick < goal.ReviewTick
                ? homeTarget ? "在家按玩家安排睡眠" : "前往指定地点按玩家安排睡眠"
                : dwelling is null ? "暂无住所，按作息休息与露宿"
                : ReturningHomeAfterDawn(goal) ? "返回住宅并恢复白天活动" : "返回住宅并按作息睡眠",
            _ => "评估需求与附近可执行工作",
        };
    }

    /// <summary>返回居民正在执行的动作摘要。</summary>
    /// <param name="id">居民的稳定 ID。</param>
    public string GetResidentActionSummary(int id)
    {
        var person = GetResident(id);
        if (person is null || person.Health <= 0)
            return "已离世，保留生平记录";
        if (ResidentNeedsRules.IsUnconscious(person))
            return $"睡眠或体力耗尽，强制昏迷\n" +
                   (person.CarriedByResidentId != 0 ? "正由" + (GetResident(person.CarriedByResidentId)?.Name ?? "其他居民") + "携带，等待进入住宅或安全落地"
                       : person.BedRestAfterRescue ? "已获救并在住宅床位休养" : "以低质量睡眠缓慢恢复") +
                   $"\n睡眠与体力均恢复到 {ResidentNeedsRules.ConsciousRecoveryThreshold:P0} 后恢复行动";
        var goal = person.Agent.Goal;
        var directed = goal.PlayerDirected && SimulationTick < goal.ReviewTick;
        var finishingSleepMove = goal.Kind == AgentGoalKind.Sleep && goal.PlayerDirected && !directed
                                 && person.MoveStartedTick < goal.ReviewTick
                                 && person.MoveStartedTick + person.MoveDurationTicks > SimulationTick;
        var restingHome = ResidentHome(person);
        // 自主睡眠按家园移动，不沿用已到期的指定地点。
        if (goal.Kind == AgentGoalKind.Sleep && !directed && restingHome is not null)
            goal = goal with { TargetX = restingHome.Value.X, TargetY = restingHome.Value.Y,
                TargetEntityId = restingHome.Value.Id, TargetSettlementId = restingHome.Value.SettlementId };
        var task = GetResidentTaskSummary(id);
        var taskHeader = $"当前任务：{task}\n";
        var facility = goal.Kind is AgentGoalKind.Work or AgentGoalKind.Rest or AgentGoalKind.Study or AgentGoalKind.TrainMagic
            ? FindBuilding(goal.TargetEntityId)
            : null;
        var homeTarget = restingHome is not null && goal.TargetX == restingHome.Value.X && goal.TargetY == restingHome.Value.Y;
        var targetFacility = facility is not null && goal.TargetX == facility.Value.X && goal.TargetY == facility.Value.Y ? facility : null;
        var returningAfterDawn = ReturningHomeAfterDawn(goal);
        var sleepPurpose = returningAfterDawn ? "恢复白天活动" : "休息与睡眠";
        var sleepNext = directed ? "继续执行玩家指定的睡眠安排"
            : returningAfterDawn ? "抵达家园后恢复白天活动" : "清晨醒来后恢复白天活动";
        var recoveryNext = targetFacility?.Value.Kind is BuildingKind.Infirmary or BuildingKind.HerbGarden or BuildingKind.Hospital
            ? "继续医疗休养，等待现场治疗与伤病恢复；按需休息或补觉"
            : "按需休息或补觉，随后按当前安排行动";
        if (person.Activity is ResidentActivity.Resting or ResidentActivity.Sleeping)
        {
            var atHome = person.IsInsideHome && restingHome is not null;
            var location = goal.Kind == AgentGoalKind.Rest && facility is not null
                    && Distance(person.X, person.Y, facility.Value.X, facility.Value.Y) <= AgentInteractionRange(goal, false)
                    && person.MoveStartedTick + person.MoveDurationTicks <= SimulationTick
                ? "在" + BuildingName(facility.Value.Kind) + "休息"
                : atHome ? "在家休息"
                : directed && goal.Kind is AgentGoalKind.Rest or AgentGoalKind.Sleep
                    && Distance(person.X, person.Y, goal.TargetX, goal.TargetY) <= AgentInteractionRange(goal, false)
                    && person.MoveStartedTick + person.MoveDurationTicks <= SimulationTick ? "在指定地点休息"
                : person.Activity == ResidentActivity.Sleeping ? "途中宿营" : "原地休息";
            if (person.Activity == ResidentActivity.Sleeping)
            {
                var afterSleep = goal.Kind == AgentGoalKind.Sleep ? sleepNext
                    : goal.Kind == AgentGoalKind.Rest ? recoveryNext
                    : "醒来后继续当前任务";
                return taskHeader + $"当前活动：正在睡眠（{location}），恢复睡眠与体力\n后续：{afterSleep}\n行动依据：{goal.Reason}";
            }

            var resting = goal.Kind == AgentGoalKind.Sleep && atHome && !returningAfterDawn
                ? "正在家园休息，等待入睡" : $"正在休息恢复体力（{location}）";
            var afterRest = goal.Kind == AgentGoalKind.Sleep
                ? directed || returningAfterDawn ? sleepNext : (atHome ? "按作息入睡，" : "返家后按作息入睡，") + sleepNext
                : goal.Kind == AgentGoalKind.Rest ? recoveryNext : "休息后继续当前任务";
            return taskHeader + $"当前活动：{resting}\n后续：{afterRest}\n行动依据：{goal.Reason}";
        }

        var cursor = RequireResident(id);
        var exploringRoutes = goal.Kind == AgentGoalKind.Explore &&
                              person.Profession is Profession.Trader or Profession.Messenger
                                  or Profession.Representative;
        if (goal.Kind == AgentGoalKind.Work && facility?.Value is { IsCompleted: true } &&
            facility.Value.SettlementId == person.SettlementId
            && ProductionRules.For(facility.Value.Kind) is { } recipe &&
            _settlements.TryGetValue(person.SettlementId, out var home))
        {
            var pickingUp = MissingResources(person.Inventory, recipe.Input) is not null;
            var x = pickingUp ? home.Value.X : facility.Value.X;
            var y = pickingUp ? home.Value.Y : facility.Value.Y;
            var travelling = person.MoveStartedTick + person.MoveDurationTicks > SimulationTick ||
                             Distance(person.X, person.Y, x, y) > 1;
            var action = !CanProduce(facility.Value, cursor, recipe) ? "当前加工条件未满足：" + GetProductionStatus(facility.Value.Id)
                : travelling ? pickingUp ? "正在返回" + home.Value.Name + "的仓库取料" : "正在携带原料前往" + BuildingName(facility.Value.Kind)
                : pickingUp ? "已到家园仓库，准备领取实际原料"
                : "已抵达" + BuildingName(facility.Value.Kind) + "，正在加工" + ResourceStock.Name(recipe.Output);
            return taskHeader + $"当前劳作：{action}\n后续：{(pickingUp ? "领取原料后运至设施加工，再" : "完成加工后")}" +
                   $"将{ResourceStock.Name(recipe.Output)}亲自运回家园入库\n行动依据：{goal.Reason}";
        }

        var destination = goal.Kind is AgentGoalKind.Rest or AgentGoalKind.Sleep
            ? targetFacility is not null
                ? BuildingName(targetFacility.Value.Kind)
                : homeTarget ? "分配的住宅" : $"指定地点（{goal.TargetX}，{goal.TargetY}）"
            : facility is not null ? BuildingName(facility.Value.Kind)
            : _settlements.GetValueOrDefault(goal.TargetSettlementId)?.Value.Name ?? "目标地块";
        var workingRange = AgentInteractionRange(goal, _settlements.GetValueOrDefault(person.SettlementId)?.Value.FoundationPending == true);
        var moveInProgress = person.MoveStartedTick + person.MoveDurationTicks > SimulationTick;
        var moving = moveInProgress || Distance(person.X, person.Y, goal.TargetX, goal.TargetY) > workingRange;
        var current = finishingSleepMove ? $"正在完成当前移动（{TravelModeName(person.TravelMode)}），随后返家{sleepPurpose}" :
            SimulationTick < goal.NavigationRetryTick ? "路线受阻，正在等待通道或重新选择任务" :
            goal.Kind is AgentGoalKind.Rest or AgentGoalKind.Sleep && !moveInProgress
                ? goal.Kind == AgentGoalKind.Rest ? "已安排休息，尚未开始休息"
                : returningAfterDawn ? "等待恢复白天活动" : "已安排睡眠，尚未开始休息" :
            moving ? goal.Kind switch
            {
                AgentGoalKind.Rest => $"正在前往{destination}休息（{TravelModeName(person.TravelMode)}）",
                AgentGoalKind.Sleep => homeTarget
                    ? $"正在返家，前往{destination}{sleepPurpose}（{TravelModeName(person.TravelMode)}）"
                    : $"正在前往{destination}睡眠（{TravelModeName(person.TravelMode)}）",
                AgentGoalKind.Rescue => IsCarryingResident(person.Id) ? "正在背负居民返回住宅" : "正在前往需要帮助的居民身边",
                _ => $"正在前往{destination}执行“{task}”（{TravelModeName(person.TravelMode)}），到场后开始劳动",
            } : goal.Kind switch
            {
                AgentGoalKind.Eat => "正在家园领取口粮",
                AgentGoalKind.Gather => "正在采集可食资源",
                AgentGoalKind.Work => facility is null
                    ? person.Profession == Profession.Lumberjack ? "正在采伐木材" :
                    person.Profession == Profession.Miner ? "正在采收石材与矿石或已发现矿藏" : "正在采收粮食"
                    : "正在" + task,
                AgentGoalKind.Flee => "正在离开危险区域",
                AgentGoalKind.Socialize => "正在与附近居民交流消息",
                AgentGoalKind.DeliverMessage or AgentGoalKind.Petition => "正在递送消息或诉求",
                AgentGoalKind.Trade => "正在交易与交付货物",
                AgentGoalKind.Study => "正在学舍推进研究",
                AgentGoalKind.TrainMagic => "正在进行魔法训练",
                AgentGoalKind.March => "正在执行实际收到的军令",
                AgentGoalKind.ReturnHome => "正在交付随身物资并补充口粮",
                AgentGoalKind.Migrate => "正在步行迁往新家园",
                AgentGoalKind.Explore => exploringRoutes ? "正在实地寻找其他聚落与可通行路线" : "正在实地勘察可采材料",
                AgentGoalKind.ClaimLand => "正在实地登记城镇地盘",
                AgentGoalKind.FetchWater => "正在河湖或运营水井打水，或实地勘察水源",
                AgentGoalKind.Hunt => "正在狩猎，实际消耗当地动物数量",
                AgentGoalKind.Fish => "正在岸边捕鱼，实际消耗鱼群数量",
                AgentGoalKind.ExtinguishFire => "正在火场边缘持续用水扑救",
                _ => "正在重新选择可执行任务",
            };
        if (!moving && facility is not null && goal.Kind != AgentGoalKind.Rest && !BuildingHasWork(facility.Value, cursor))
        {
            current = "现场劳动受阻：" + (GetBuildingDetailStatus(facility.Value.Id) is { Length: > 0 } status
                ? status
                : "岗位已满或当前角色不满足劳动条件");
        }

        var next = goal.Kind switch
        {
            AgentGoalKind.Gather or AgentGoalKind.Work => "完成现场劳动后，将采收物资带回家园；疲劳或饥饿时先休息、进食",
            AgentGoalKind.Rest => recoveryNext,
            AgentGoalKind.DeliverMessage or AgentGoalKind.Petition => "送达消息后返回出发聚落",
            AgentGoalKind.Trade => "完成交易后携带实际收到的货物返乡",
            AgentGoalKind.Migrate => "抵达并确认接纳后改变家园归属",
            AgentGoalKind.Explore => exploringRoutes
                ? "发现聚落后记下实际位置，再按已知消息选择拜访或运输；口粮不足时返乡补给"
                : "看到材料后实地采集；勘察距离达到补给范围时先返乡",
            AgentGoalKind.Sleep => directed || returningAfterDawn ? sleepNext : "返家后按作息入睡，" + sleepNext,
            _ => "完成当前任务后，按自身需求、可见岗位与已知消息重新选择",
        };
        return taskHeader + $"当前劳作：{current}\n后续：{next}\n行动依据：{goal.Reason}";
    }

    private bool ReturningHomeAfterDawn(AgentGoal goal)
    {
        var time = SimulationTime.TimeOfDay(SimulationTick);
        return goal.Kind == AgentGoalKind.Sleep && !(goal.PlayerDirected && SimulationTick < goal.ReviewTick)
               && time >= SimulationTime.WakeTick && time < SimulationTime.ReturnHomeTick;
    }
}
