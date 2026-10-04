namespace SeWZC.WorldBox.Core;

public enum ResourceVisibility { Researched, All, None }

public sealed partial class WorldEngine
{
    public static string ProfessionName(Profession job) => job switch
    {
        Profession.Child => "孩童", Profession.Farmer => "农民", Profession.Lumberjack => "伐木工",
        Profession.Miner => "矿工", Profession.Soldier => "战士", Profession.Builder => "建造者",
        Profession.Trader => "商人", Profession.Messenger => "信使", Profession.Representative => "代表",
        Profession.Fisher => "渔民", Profession.Scholar => "学者", Profession.Mage => "法师", _ => "未知职业"
    };

    public bool IsDepositVisible(Tile tile, ResourceVisibility visibility) => tile.Deposit is { } kind
        && (visibility == ResourceVisibility.All || visibility == ResourceVisibility.Researched
            && (tile.DepositDiscovered || DepositResearch(kind) is { } research && State.Society.Research.Any(r => r.Completed.Contains(research))));

    public static string BuildingDescription(BuildingKind kind) => kind switch
    {
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
        BuildingKind.Granary => "完工且健康时改善家园补给组织，居民在中心附近休息恢复每级提高 10%；同类建筑取最强。",
        BuildingKind.Housing => "提供实际住房，每级容纳 20 名居民；完工、健康且启用才计入人口容量。",
        BuildingKind.Market => "人员到场值守后，在集市 3 格内的居民可与最多相距 3 格的人交换已有消息；消耗少量当地粮食。",
        BuildingKind.Watchtower => "完工且健康时，为在塔 2 格内的同聚落居民提供观察点；亲眼观察火灾的范围每级增加 1 格。",
        BuildingKind.Farm => "居民到场耕作，将粮食装入随身库存并运回家园。肥力、干旱、农业研究和政策影响收成。",
        BuildingKind.Workshop => "为附近伐木和采矿提供劳动岗位，需要附近存在实际可采材料；材料由劳动者随身携带并运回。",
        BuildingKind.Academy => "到场学者推进当地已经立项的研究。研究需预付材料并满足前置知识，成果通过消息传播。",
        BuildingKind.Waystation => "到场人员值守的交通驿站，改善附近信使行程；每次工作消耗少量当地粮食。",
        BuildingKind.SignalTower => "无线通信设施，需要电气化和信号网络、健康的建筑与实际值守人员。接入距离 12 格，塔间 24 格，山脉阻挡信号。",
        BuildingKind.ArcaneSanctum => "具备天赋的居民到场训练魔法，消耗粮食并受开放魔法发展规则限制。",
        BuildingKind.Infirmary => "工作人员在现场治疗附近受伤或患病居民，治疗受粮食和实际距离限制。",
        BuildingKind.Bridge => "只允许沿左右或上下方向通行。各等级有离自然岸距离上限，同向逐段施工，不能借桥段重置离岸距离。可升级或安排改向。",
        BuildingKind.MountainPass => "完工后开放山地步行通道，山路仍比平地耗时，连接相邻可通行道路才能使用。",
        _ => "居民从家园领取实际原料，抵达设施加工，产物随身带回。运营需要当地掌握对应研究及其前置。\n" + ProductionRecipe(kind)
    };

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

    public string GetResidentActionSummary(int id)
    {
        var person = GetResident(id);
        if (person is null || person.Health <= 0) return "已离世，保留生平记录";
        var goal = person.Agent.Goal;
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
            return $"现在：{action}\n后续：{(pickingUp ? "领取原料后运至设施加工，再" : "完成加工后")}" +
                $"将{ResourceStock.Name(recipe.Output)}亲自运回家园入库\n行动依据：{goal.Reason}";
        }
        var destination = facility is not null ? BuildingName(facility.Kind)
            : _settlements.GetValueOrDefault(goal.TargetSettlementId)?.Name ?? "目标地块";
        var workingRange = facility is not null && (!facility.IsCompleted || facility.IsUpgrading
            || IsWaterfrontBuilding(facility.Kind) || facility.Kind == BuildingKind.TownCenter) ? 1 : goal.TargetEntityId == 0 ? 1 : 0;
        var moving = person.MoveStartedTick + person.MoveDurationTicks > State.Tick
            || Distance(person.X, person.Y, goal.TargetX, goal.TargetY) > workingRange;
        var current = State.Tick < goal.NavigationRetryTick ? "路线受阻，正在等待通道或重新选择任务" : moving ? $"正在前往{destination}（{TravelModeName(person.TravelMode)}）" : goal.Kind switch
        {
            AgentGoalKind.Explore => exploringRoutes ? "正在实地寻找其他聚落与可通行路线" : "正在实地勘察可采材料",
            AgentGoalKind.Work => facility is null ? person.Profession == Profession.Lumberjack ? "正在采伐木材" : person.Profession == Profession.Miner ? "正在采收石材与矿石" : "正在采收粮食" : facility.IsUpgrading ? "正在升级或改向" + BuildingName(facility.Kind) : facility.IsCompleted ? facility.Kind == BuildingKind.Academy ? "正在研究" + (State.Society.Research.First(r => r.SettlementId == facility.SettlementId).ActiveProject is { } active ? ResearchName(active) : "当地待立项课题") : facility.Kind == BuildingKind.Farm ? "正在农场耕作和采收粮食" : "正在" + BuildingName(facility.Kind) + "执行" + (person.Profession == Profession.Lumberjack ? "伐木任务" : person.Profession == Profession.Miner ? "采矿任务" : "岗位任务") : "正在施工" + BuildingName(facility.Kind),
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
        return $"现在：{current}\n后续：{next}\n行动依据：{goal.Reason}";
    }
}
