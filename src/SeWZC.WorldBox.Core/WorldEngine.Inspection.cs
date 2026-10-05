namespace SeWZC.WorldBox.Core;

public enum ResourceVisibility { Researched, All, None }

public sealed partial class WorldEngine
{
    public static string ProfessionName(Profession job) => job switch
    {
        Profession.Child => "孩童", Profession.Farmer => "农民", Profession.Lumberjack => "伐木工",
        Profession.Miner => "矿工", Profession.Soldier => "战士", Profession.Builder => "建造者",
        Profession.Trader => "商人", Profession.Messenger => "信使", Profession.Representative => "代表",
        Profession.Fisher => "渔民", Profession.Scholar => "学者", Profession.Mage => "法师",
        Profession.Engineer => "工程师", Profession.Physician => "医师", Profession.Firefighter => "消防员", Profession.Ranger => "游击射手",
        Profession.Archivist => "文献师", Profession.Battlemage => "战斗法师", Profession.Surveyor => "测绘员", Profession.Gardener => "园艺师", _ => "未知职业"
    };

    public bool IsDepositVisible(Tile tile, ResourceVisibility visibility) => tile.Deposit is { } kind
        && (visibility == ResourceVisibility.All || visibility == ResourceVisibility.Researched
            && (tile.DepositDiscovered || DepositResearch(kind) is { } research && State.Society.Research.Any(r => r.Completed.Contains(research))));

    public static string BuildingDescription(BuildingKind kind) => kind switch
    {
        BuildingKind.Pasture => "农民在牧场驯养当地草食动物，携带饲料和饮水到场；保留繁殖群，成熟后产出粮食。干旱或草场耗尽时停工。",
        BuildingKind.Aquaculture => "渔民从相邻河湖取得实际鱼群，投喂、繁殖与收获；需要工业和运输知识、饲料、饮水与现场劳动。",
        >= BuildingKind.Reservoir when AdvancementRules.For(kind) is null => ResearchRules.Unlocking(kind)?.Effect ?? "",
        BuildingKind.AssemblyHall => "人类议事厅：当地有成年的人类可建。人类到场携带粮水值守，为两格内居民缓解社交需求，并为附近居民提供三格当面交流范围。",
        BuildingKind.TradeGuild => "人类商贸公会：人类携带粮水到场值守，为三格内本地商人提高行走速度 15%，并提供三格当面交流范围。升级提高服务效率。",
        BuildingKind.SacredGrove => "精灵圣林：保留森林，需要奥术基础和开放魔法规则。精灵携带粮水到场训练，每单位劳动提高训练 0.1、恢复魔力 0.3。",
        BuildingKind.HerbGarden => "精灵草药园：精灵携带粮水到场，为附近实际患者治疗，每单位劳动恢复生命 0.6，并减少一日病程。",
        BuildingKind.DwarvenForge => "矮人锻炉：当地有成年矮人且掌握工业冶炼与前置知识。矮人实际从仓库领取木材 2、矿石 2，到场每批锻造合金 1.5，再亲自返仓。",
        BuildingKind.MiningHall => "矮人矿业工坊：矮人携带口粮到场，每单位劳动采收相邻真实石矿储量 0.3，携带石材和矿石返仓。",
        BuildingKind.HuntingCamp => "兽人狩猎营：兽人携带口粮到场，每单位劳动捕获本格食草动物 0.25，食物按猎物体型折算，实际减少动物并携带返仓。",
        BuildingKind.WarDrum => "兽人战鼓营：兽人携带粮水到场，为两格内同聚落居民恢复体力，为两格内同国军队恢复士气。",
        BuildingKind.TownCenter => "聚落的公共中心与家园粮仓。居民在附近领取口粮、交付采收、交流消息；代表在此汇集诉求。也是付费城镇扩充的施工地点，建筑等级与村、镇、城等级独立。定居时建立，受损后可由居民重建。",
        BuildingKind.Shipyard => "在近岸水中建造舟船。居民从相邻自然陆岸施工，实地取木材、加工，再携带舟船返仓；需要驿路运输知识。",
        BuildingKind.Dock => "在近岸水中提供舟船交通服务。居民从陆岸值守；同国舟船在 3 格内的水上速度每级提高 15%，不叠加多个码头。",
        BuildingKind.LumberCamp => "设在森林边缘，伐木工到场开采相邻实际木材，随身运回家园；资源耗尽时停工。",
        BuildingKind.Quarry => "设在山地或丘陵矿区旁，矿工到场开采相邻石材和矿石，随身运回家园；资源耗尽时停工。",
        BuildingKind.Well => "设在湿地或供水充足的陆地。工人到井边取用当地当日供水，携带返仓；与野外取水共享地块额度，干旱会减水。",
        BuildingKind.Granary => "居民返乡休息恢复每级提高 10%；多个粮仓取最高倍率。",
        BuildingKind.Housing => "每级增加 20 人住房容量。",
        BuildingKind.Market => "人员到场值守后，在集市 3 格内的居民可与最多相距 3 格的人交换已有消息；消耗少量当地粮食。",
        BuildingKind.Watchtower => "塔 2 格内的同聚落居民观察火灾范围增加：1／2／3 级分别为 4／5／6 格；无需工作人员。",
        BuildingKind.Farm => "居民到场耕作，将粮食装入随身库存并运回家园。肥力、干旱、农业研究和政策影响收成。",
        BuildingKind.Workshop => "为附近伐木和采矿提供劳动岗位，需要附近存在实际可采材料；材料由劳动者随身携带并运回。",
        BuildingKind.Academy => "到场学者推进当地已经立项的研究。研究需预付材料并满足前置知识，成果通过消息传播。",
        BuildingKind.Waystation => "到场人员值守的交通驿站，改善附近信使行程；每次工作消耗少量当地粮食。",
        BuildingKind.SignalTower => "无线通信设施，需要电气化、信号网络和在场值守人员。接入距离 12 格，塔间 24 格，山脉阻挡信号。",
        BuildingKind.ArcaneSanctum => "具备天赋的居民到场训练魔法，消耗粮食并受开放魔法发展规则限制。",
        BuildingKind.Infirmary => "工作人员在现场治疗附近受伤或患病居民，治疗受粮食和实际距离限制。",
        BuildingKind.Bridge => "只允许沿左右或上下方向通行。各等级有离自然岸距离上限，同向逐段施工，不能借桥段重置离岸距离。可升级或安排改向。",
        BuildingKind.MountainPass => "完工后开放山地步行通道，山路仍比平地耗时，连接相邻可通行道路才能使用。",
        _ => "居民从家园领取实际原料，抵达设施加工，产物随身带回。\n" + ProductionRecipe(kind)
    };

    // Detail status contains current obstacles and progress, never the static description.
    public string GetBuildingDetailStatus(int id)
    {
        var b = State.Society.Buildings.FirstOrDefault(building => building.Id == id);
        if (b is null) return "建筑已被移除";
        var tile = State.Tiles[Index(b.X, b.Y)];
        if (b.Kind is BuildingKind.Bridge or BuildingKind.MountainPass
            && tile.Improvement == (b.Kind == BuildingKind.Bridge ? LandImprovement.Bridge : LandImprovement.MountainPass))
            return b.IsUpgrading ? "施工期间保留原通道，完工后更新通行效果" : "";
        if (!BuildingGroundOwned(b)) return "暂停：所在地已脱离城镇占领区域，重新连接领地后可恢复运营";
        if (!b.IsCompleted) return $"施工进度 {b.ConstructionProgress:0.#} / {b.ConstructionRequired:0}，等待居民到场施工";
        if (b.Health < 50) return "暂停：生命低于 50，修复至至少 50 后恢复功能";
        if (!b.Enabled) return "已停用；点击“恢复运营”重新启用";
        if (b.IsUpgrading) return "升级施工中，暂停原有功能";
        if (tile.FireTicks > 0) return $"暂停：所在地着火，剩余 {tile.FireTicks} 日；可安排居民灭火";
        if (!BuildingTerrainValid(b.Kind, tile)) return "暂停：地形已改变，不再适合此设施；请恢复原地形或换址建设";
        if (b.Kind == BuildingKind.Well && WellWaterYield(tile) <= 0) return "暂停：当地供水不足，水井无法出水";
        var town = RequireTown(b.SettlementId);
        if (!CanBuildRacialFacility(town.Id, b.Kind)) return $"暂停：本聚落没有存活的成年{RaceNames[(int)BuildingRace(b.Kind)!.Value]}";
        if (AdvancementRules.For(b.Kind) is { } recipe)
        {
            var missingKnowledge = recipe.Prerequisites.Append(recipe.Research).Distinct().Where(k => !HasResearch(town.Id, k)).Select(ResearchName).ToArray();
            if (missingKnowledge.Length > 0) return "缺少本地研究：" + string.Join("、", missingKnowledge);
            if (ProductionYield(b, recipe) <= 0) return "无法产粮：土地肥力为 0";
            var missing = MissingResources(town.Resources, recipe.Input);
            return $"累计加工 {b.ProductionBatches} 批" + (missing is null ? "" : "\n仓库原料：" + missing + "；已携带原料的工人仍可加工");
        }
        if (b.Kind == BuildingKind.SignalTower)
        {
            var missing = new[] { ResearchKind.Electrification, ResearchKind.SignalNetwork }.Where(k => !HasResearch(town.Id, k)).Select(ResearchName).ToArray();
            if (missing.Length > 0) return "缺少本地研究：" + string.Join("、", missing);
        }
        if (IsHusbandry(b.Kind)) return GetProductionStatus(b.Id);
        if (b.Kind >= BuildingKind.Reservoir) return ExpansionFacilityStatus(b);
        if (b.Kind == BuildingKind.Academy)
        {
            var research = State.Society.Research.First(r => r.SettlementId == town.Id);
            return research.ActiveProject is { } project ? $"研究：{ResearchName(project)}\n进度 {research.Progress:0.#} / {research.RequiredProgress:0}" : "尚未立项；到聚落的研究页面选择项目并投入材料";
        }
        if (b.Kind == BuildingKind.TownCenter) return town.IsExpanding ? $"城镇扩充进度 {town.ExpansionProgress:0.#} / {town.ExpansionRequired:0}" : "";
        if (PassiveFacility(b)) return "";
        if (b.Kind is BuildingKind.ArcaneSanctum or BuildingKind.SacredGrove && !State.Society.MagicEnabled) return "暂停训练：世界规则已关闭新的魔法发展";
        if (b.Kind == BuildingKind.SacredGrove && !IsForestTerrain(tile.Terrain)) return "暂停训练：圣林须位于森林、疏林或雨林";
        if (b.Kind == BuildingKind.Well && AvailableWater(b.X, b.Y) <= 0) return "今日供水已用完，次日恢复额度";
        if (b.Kind == BuildingKind.LumberCamp && FindWorkshopResource(b, Profession.Lumberjack) < 0) return "附近木材已采尽";
        if (b.Kind == BuildingKind.Workshop && FindWorkshopResource(b, Profession.Lumberjack) < 0 && FindWorkshopResource(b, Profession.Miner) < 0) return "邻格没有可采木材、石材或矿石";
        if (b.Kind is BuildingKind.Quarry or BuildingKind.MiningHall && FindWorkshopResource(b, Profession.Miner) < 0) return "附近石材与矿石已采尽";
        if (b.Kind == BuildingKind.HuntingCamp && !WildlifeSiteProductive(tile, aquatic: false)) return "本格猎物稀少，等待种群恢复或另寻猎场";
        if (b.Kind is BuildingKind.Infirmary or BuildingKind.HerbGarden && FindLocalWorkPatient(b, firstOnly: true) is null) return "3 格内没有需要治疗的同聚落居民";
        if (b.Kind is BuildingKind.Waystation or BuildingKind.SignalTower or BuildingKind.Dock or BuildingKind.Market && town.Resources.Food < .01) return $"值守缺粮：仓库粮食 {town.Resources.Food:0.###}，每次需要 0.01";
        if (b.Kind == BuildingKind.ArcaneSanctum && town.Resources.Food < .03) return $"训练缺粮：仓库粮食 {town.Resources.Food:0.###}，每次需要 0.03";
        if (b.Kind == BuildingKind.Infirmary && town.Resources.Food < .05) return $"治疗缺粮：仓库粮食 {town.Resources.Food:0.###}，每次需要 0.05";
        return IsFacilityOperating(b) ? "" : "暂无在场工作人员";
    }

    public void SetBuildingEnabled(int id, bool enabled)
    {
        var building = State.Society.Buildings.FirstOrDefault(b => b.Id == id) ?? throw new ArgumentException("建筑已不存在。");
        if (building.Kind == BuildingKind.TownCenter) throw new InvalidOperationException("城镇中心是公共家园，不能停用。");
        building.Enabled = enabled; building.Workers.Clear();
        AddEvent(WorldEventKind.Editor, $"玩家{(enabled ? "启用" : "停用")}{BuildingName(building.Kind)}。", building.X, building.Y);
    }

    public void RestoreBuilding(int id, bool finish = false)
    {
        var building = State.Society.Buildings.FirstOrDefault(b => b.Id == id) ?? throw new ArgumentException("建筑已不存在。");
        building.Health = 100;
        if (finish) { building.ConstructionProgress = building.ConstructionRequired; CompleteLandImprovement(building); }
        AddEvent(WorldEventKind.Editor, $"玩家{(finish ? "赐予完工" : "修复")}{BuildingName(building.Kind)}。", building.X, building.Y);
    }

    public string GetResidentTaskSummary(int id)
    {
        var person = GetResident(id);
        if (person is null || person.Health <= 0) return "已离世，保留生平记录";
        var goal = person.Agent.Goal;
        var building = goal.Kind is AgentGoalKind.Work or AgentGoalKind.Study or AgentGoalKind.TrainMagic ? FindBuilding(goal.TargetEntityId) : null;
        var home = _settlements.GetValueOrDefault(person.SettlementId);
        var destination = _settlements.GetValueOrDefault(goal.TargetSettlementId)?.Name ?? "目标聚落";
        if (building is not null)
        {
            var name = BuildingName(building.Kind);
            if (!building.IsCompleted) return "建造" + name;
            if (building.IsUpgrading) return (building.PendingDirection.HasValue ? "改造桥梁方向" : "升级" + name);
            if (building.Kind == BuildingKind.TownCenter) return "在城镇中心扩充为" + (home is null ? "下一等级" : SettlementTierName(home.Tier + 1));
            if (AdvancementRules.For(building.Kind) is { } recipe) return "在" + name + "生产" + ResourceStock.Name(recipe.Output);
            return building.Kind switch
            {
                BuildingKind.Farm => "在农田耕作并收获粮食",
                BuildingKind.Workshop or BuildingKind.LumberCamp or BuildingKind.Quarry or BuildingKind.MiningHall => "在" + name + (person.Profession == Profession.Lumberjack ? "采伐木材" : "采收石材与矿石"),
                BuildingKind.Academy => "在学舍研究" + (State.Society.Research.FirstOrDefault(r => r.SettlementId == building.SettlementId)?.ActiveProject is { } research ? ResearchName(research) : "待立项课题"),
                BuildingKind.Well => "在水井打水并运回家园",
                BuildingKind.Infirmary or BuildingKind.HerbGarden => "治疗附近同聚落伤病居民",
                BuildingKind.ArcaneSanctum or BuildingKind.SacredGrove => "训练魔法并恢复魔力",
                BuildingKind.HuntingCamp => "在狩猎营捕获食草动物",
                BuildingKind.WarDrum => "击鼓恢复附近居民体力与军队士气",
                BuildingKind.AssemblyHall => "组织附近居民交流，缓解社交需求",
                BuildingKind.TradeGuild or BuildingKind.Market => "值守" + name + "，协助附近居民交流与贸易",
                BuildingKind.Waystation or BuildingKind.Dock or BuildingKind.SignalTower => "值守" + name + "，支持实际交通与通信",
                _ => "在" + name + "工作"
            };
        }
        return goal.Kind switch
        {
            AgentGoalKind.Work => person.Profession == Profession.Lumberjack ? "采伐木材并带回家园" : person.Profession == Profession.Miner ? "开采石矿或已发现矿藏并带回家园" : "采收粮食并带回家园",
            AgentGoalKind.Gather => "采集浆果、草籽或嫩叶并带回家园",
            AgentGoalKind.FetchWater => goal.TargetEntityId > 0 ? "到已发现的水源打水并带回家园" : "实地勘察可用水源",
            AgentGoalKind.Hunt => "狩猎可食动物并带回家园",
            AgentGoalKind.Fish => person.TravelMode == TravelMode.Boat ? "乘舟捕鱼并带回鱼获与舟船" : "到鱼群附近捕鱼并带回家园",
            AgentGoalKind.ClaimLand => "到场驻留登记" + (home?.Name ?? "家园") + "的相邻领地",
            AgentGoalKind.Eat => "返回家园领取口粮和饮水",
            AgentGoalKind.Rest => "返回家园休息恢复体力",
            AgentGoalKind.ReturnHome => home?.FoundationPending == true ? "携带建村物资抵达新家园并驻留建村" : "返回家园交付产物并补充粮水",
            AgentGoalKind.Socialize => "在家园与居民当面交流消息",
            AgentGoalKind.Trade => "前往" + destination + "交换并交付货物",
            AgentGoalKind.DeliverMessage => "将携带的消息实际送达" + destination,
            AgentGoalKind.Petition => "将已有诉求报告递送至" + destination,
            AgentGoalKind.Explore => person.Profession is Profession.Trader or Profession.Messenger or Profession.Representative ? "勘察聚落与可通行路线" : "勘察本职可采材料",
            AgentGoalKind.ExtinguishFire => "携带实际饮水扑灭附近火势",
            AgentGoalKind.Migrate => "步行迁居至" + destination,
            AgentGoalKind.March => "执行实际收到的军令",
            AgentGoalKind.Flee => "离开附近危险区域",
            AgentGoalKind.Study => "寻找可参与的研究课题",
            AgentGoalKind.TrainMagic => "寻找可参与的魔法训练",
            _ => "评估需求与附近可执行工作"
        };
    }

    public string GetResidentActionSummary(int id)
    {
        var person = GetResident(id);
        if (person is null || person.Health <= 0) return "已离世，保留生平记录";
        var goal = person.Agent.Goal;
        var task = GetResidentTaskSummary(id);
        var taskHeader = $"当前任务：{task}\n";
        var exploringRoutes = goal.Kind == AgentGoalKind.Explore && person.Profession is Profession.Trader or Profession.Messenger or Profession.Representative;
        var facility = goal.Kind is AgentGoalKind.Work or AgentGoalKind.Study or AgentGoalKind.TrainMagic ? State.Society.Buildings.FirstOrDefault(b => b.Id == goal.TargetEntityId) : null;
        if (goal.Kind == AgentGoalKind.Work && facility is { IsCompleted: true } && facility.SettlementId == person.SettlementId
            && AdvancementRules.For(facility.Kind) is { } recipe && _settlements.TryGetValue(person.SettlementId, out var home))
        {
            var pickingUp = MissingResources(person.Inventory, recipe.Input) is not null;
            var x = pickingUp ? home.X : facility.X; var y = pickingUp ? home.Y : facility.Y;
            var travelling = person.MoveStartedTick + person.MoveDurationTicks > State.Tick || Distance(person.X, person.Y, x, y) > 1;
            var action = !CanProduce(facility, person, recipe) ? "当前加工条件未满足：" + GetProductionStatus(facility.Id)
                : travelling ? pickingUp ? "正在返回" + home.Name + "的仓库取料" : "正在携带原料前往" + BuildingName(facility.Kind)
                : pickingUp ? "已到家园仓库，准备领取实际原料" : "已抵达" + BuildingName(facility.Kind) + "，正在加工" + ResourceStock.Name(recipe.Output);
            return taskHeader + $"当前劳作：{action}\n后续：{(pickingUp ? "领取原料后运至设施加工，再" : "完成加工后")}" +
                $"将{ResourceStock.Name(recipe.Output)}亲自运回家园入库\n行动依据：{goal.Reason}";
        }
        var destination = facility is not null ? BuildingName(facility.Kind)
            : _settlements.GetValueOrDefault(goal.TargetSettlementId)?.Name ?? "目标地块";
        var workingRange = AgentInteractionRange(person, _settlements.GetValueOrDefault(person.SettlementId));
        var moving = person.MoveStartedTick + person.MoveDurationTicks > State.Tick
            || Distance(person.X, person.Y, goal.TargetX, goal.TargetY) > workingRange;
        var current = State.Tick < goal.NavigationRetryTick ? "路线受阻，正在等待通道或重新选择任务" : moving ? $"正在前往{destination}执行“{task}”（{TravelModeName(person.TravelMode)}），到场后开始劳动" : goal.Kind switch
        {
            AgentGoalKind.Explore => exploringRoutes ? "正在实地寻找其他聚落与可通行路线" : "正在实地勘察可采材料",
            AgentGoalKind.Work => facility is null ? person.Profession == Profession.Lumberjack ? "正在采伐木材" : person.Profession == Profession.Miner ? "正在采收石材与矿石或已发现矿藏" : "正在采收粮食" : "正在" + task,
            AgentGoalKind.ExtinguishFire => "正在火场边缘持续用水扑救", AgentGoalKind.ClaimLand => "正在实地登记城镇地盘", AgentGoalKind.FetchWater => "正在河湖或湿地打水或实地勘察水源",
            AgentGoalKind.Hunt => "正在狩猎，实际消耗当地动物数量", AgentGoalKind.Fish => "正在岸边捕鱼，实际消耗鱼群数量",
            AgentGoalKind.Gather => "正在采集可食资源", AgentGoalKind.Eat => "正在家园领取口粮",
            AgentGoalKind.Rest => "正在休息恢复体力", AgentGoalKind.Socialize => "正在与附近居民交流消息",
            AgentGoalKind.Study => "正在学舍推进研究", AgentGoalKind.TrainMagic => "正在进行魔法训练",
            AgentGoalKind.Trade => "正在交易与交付货物", AgentGoalKind.DeliverMessage or AgentGoalKind.Petition => "正在递送消息或诉求",
            AgentGoalKind.ReturnHome => "正在交付随身物资并补充口粮", AgentGoalKind.Flee => "正在离开危险区域",
            AgentGoalKind.Migrate => "正在步行迁往新家园", AgentGoalKind.March => "正在执行实际收到的军令",
            _ => "正在重新选择可执行任务"
        };
        if (!moving && facility is not null && !BuildingHasWork(facility, person))
            current = "现场劳动受阻：" + (GetBuildingDetailStatus(facility.Id) is { Length: > 0 } status ? status : "岗位已满或当前角色不满足劳动条件");
        var next = goal.Kind switch
        {
            AgentGoalKind.Explore => exploringRoutes ? "发现聚落后记下实际位置，再按已知消息选择拜访或运输；口粮不足时返乡补给" : "看到材料后实地采集；勘察距离达到补给范围时先返乡",
            AgentGoalKind.Work or AgentGoalKind.Gather => "完成现场劳动后，将采收物资带回家园；疲劳或饥饿时先休息、进食",
            AgentGoalKind.Trade => "完成交易后携带实际收到的货物返乡",
            AgentGoalKind.DeliverMessage or AgentGoalKind.Petition => "送达消息后返回出发聚落",
            AgentGoalKind.Migrate => "抵达并确认接纳后改变家园归属",
            AgentGoalKind.Rest => "恢复体力后重新评估可执行工作",
            _ => "完成当前任务后，按自身需求、可见岗位与已知消息重新选择"
        };
        return taskHeader + $"当前劳作：{current}\n后续：{next}\n行动依据：{goal.Reason}";
    }
}
