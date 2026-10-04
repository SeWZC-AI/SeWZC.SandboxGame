namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    public string? ResearchPrerequisiteError(int settlementId, ResearchKind kind)
    {
        var definition = ResearchRules.For(kind);
        if (definition.Magic && !State.Society.MagicEnabled) return "世界规则已关闭新的魔法发展";
        var missing = definition.Prerequisites.Where(p => !HasResearch(settlementId, p)).Select(ResearchName).ToArray();
        return missing.Length == 0 ? null : "需要先掌握" + string.Join("、", missing);
    }

    public static string ResearchDescription(ResearchKind kind)
    {
        var r = ResearchRules.For(kind);
        return $"分支：{r.Branch}\n阶段：{r.Stage}\n前置：{(r.Prerequisites.Length == 0 ? "无" : string.Join("、", r.Prerequisites.Select(ResearchName)))}\n{r.Effect}";
    }

    public string GetAdvancementStage(int settlementId)
    {
        string Stage(bool magic) => HasResearch(settlementId, magic ? ResearchKind.MagicalEmpire : ResearchKind.TechnologicalEmpire)
            ? magic ? "魔法帝国" : "科技帝国"
            : AdvancementRules.All.LastOrDefault(a => a.Magic == magic && HasResearch(settlementId, a.Research))?.Stage
                ?? (magic ? HasResearch(settlementId, ResearchKind.ArcaneArts) ? "基础奥术" : "未发展" : "古代");
        return $"科技：{Stage(false)}\n魔法：{Stage(true)}";
    }

    public static string ProductionRecipe(BuildingKind kind)
    {
        var a = AdvancementRules.For(kind);
        return a is null ? "" : $"每批原料：{AdvancementRules.Stock(a.Input)}{(a.Mana > 0 ? $"\n施作者魔力消耗：{a.Mana:0}" : "")}\n每批基础产出：{ResourceStock.Name(a.Output)} {a.Yield:0.##}\n工人领料、现场加工，再将产物运回仓库。"
            + (a.Output == ResourceKind.Food ? "粮食产量受肥力与干旱影响。" : "");
    }

    private string? ProductionRequirement(Building building, Advancement a)
    {
        if (!BuildingGroundOwned(building)) return "所在地已脱离城镇占领区域，暂停运营";
        if (!building.Enabled) return "玩家已停用，恢复运营后才会安排工作";
        if (!building.IsCompleted) return "等待施工完成";
        if (building.IsUpgrading) return "正在升级或改向，暂停生产";
        if (building.Health < 50) return "设施受损，需要修复后运营";
        if (State.Tiles[Index(building.X, building.Y)].FireTicks > 0) return "设施所在地正在燃烧，暂停生产";
        if (!HasResearch(building.SettlementId, a.Research)
            || a.Prerequisites.Any(p => !HasResearch(building.SettlementId, p))) return "缺少当地运营知识：" + ResearchName(a.Research) + "及其前置";
        return null;
    }

    private double ProductionYield(Building building, Advancement a)
    {
        var multiplier = (HasResearch(building.SettlementId, a.Magic ? ResearchKind.MagicalEmpire : ResearchKind.TechnologicalEmpire) ? 1.25 : 1)
            * (a.Output == ResourceKind.Food && HasResearch(building.SettlementId, ResearchKind.Irrigation) ? 1.25 : 1)
            * (building.Kind is BuildingKind.Foundry or BuildingKind.DwarvenForge && HasResearch(building.SettlementId, ResearchKind.EfficientSmelting) ? 1.25 : 1)
            * (building.Kind == BuildingKind.PowerPlant && HasResearch(building.SettlementId, ResearchKind.EnergyRecycling) ? 1.5 : 1)
            * (building.Kind is BuildingKind.Crystallizer or BuildingKind.AetherForge && HasResearch(building.SettlementId, ResearchKind.Leylines) ? 1.25 : 1);
        if (a.Output != ResourceKind.Food) return a.Yield * building.Efficiency * multiplier;
        var tile = State.Tiles[Index(building.X, building.Y)];
        return a.Yield * building.Efficiency * multiplier * tile.Fertility / 100d * (tile.DroughtTicks > 0 ? .18 : 1);
    }

    public string GetProductionStatus(int buildingId)
    {
        var building = State.Society.Buildings.FirstOrDefault(b => b.Id == buildingId);
        var a = building is null ? null : AdvancementRules.For(building.Kind);
        if (building is null) return "建筑已不存在";
        if (!BuildingGroundOwned(building)) return "所在地已脱离城镇占领区域，暂停运营";
        if (building.Health <= 0) return "建筑已损毁，等待重建";
        if (building.Health < 50) return "建筑受损，需要修复后工作";
        if (State.Tiles[Index(building.X, building.Y)].FireTicks > 0) return "正在燃烧，暂停工作";
        var workers = State.Tick - building.LastWorkedTick <= 1 ? building.Workers.Count : 0;
        if (!building.IsCompleted) return $"施工：{building.ConstructionProgress / building.ConstructionRequired:P0}   到场工人 {workers}/{building.WorkSlots}";
        if (building.IsUpgrading) return $"{(building.PendingDirection.HasValue ? "改向" : "升级")}：{building.UpgradeProgress:0.#} / {building.UpgradeRequired:0}\n等待居民到场施工";
        if (!building.Enabled) return "已停用";
        if (!CanBuildRacialFacility(building.SettlementId, building.Kind)) return "缺少该族成年居民，暂停运营";
        if (a is null)
        {
            var town = RequireTown(building.SettlementId);
            if (building.Kind == BuildingKind.TownCenter && town.IsExpanding)
                return $"组织城镇扩充：{town.ExpansionProgress:0.#} / {town.ExpansionRequired:0}\n到场工人 {workers}/{building.WorkSlots}";
            var research = State.Society.Research.First(r => r.SettlementId == town.Id);
            var activity = building.Kind switch
            {
                BuildingKind.Farm => $"产出：粮食   累计采收 {State.Tiles[Index(building.X, building.Y)].Harvested:0.#}",
                BuildingKind.Workshop => "产出：附近实际可采的木材、石材与矿石",
                BuildingKind.Academy => research.ActiveProject is { } kind ? $"正在研究：{ResearchName(kind)}   {research.Progress / research.RequiredProgress:P0}" : "等待当地研究立项",
                BuildingKind.TownCenter => $"家园粮仓：粮食 {town.Resources.Food:0.#}   木材 {town.Resources.Wood:0.#}",
                BuildingKind.ArcaneSanctum => "功能：训练法术，提升到场居民的魔法熟练度",
                BuildingKind.Infirmary => "功能：治疗附近受伤与患病居民",
                BuildingKind.SignalTower => "功能：值守无线通信，传递实际收到的消息",
                BuildingKind.Waystation => "功能：值守驿站，改善附近信使通行",
                _ => BuildingDescription(building.Kind)
            };
            return activity + (PassiveFacility(building) || building.Kind is BuildingKind.TownCenter or BuildingKind.Bridge or BuildingKind.MountainPass ? "" : $"\n到场工作 {workers}/{building.WorkSlots} 人");
        }
        if (!CanBuildRacialFacility(building.SettlementId, building.Kind)) return "缺少该族成年居民，暂停运营";
        var requirement = ProductionRequirement(building, a);
        if (requirement is not null) return requirement;
        if (ProductionYield(building, a) <= 0) return "土地无法产粮，需要恢复肥力";
        var townStock = RequireTown(building.SettlementId);
        var reserve = _localWorkQueriesActive ? _productionReserves.GetValueOrDefault(townStock.Id) : LocalDevelopmentReserve(townStock);
        var reserved = AdvancementRules.Resources.Where(k => a.Input.Get(k) > 0 && (reserve?.Get(k) ?? 0) > 0
            && townStock.Resources.Get(k) < a.Input.Get(k) + reserve!.Get(k)).Select(k => ResourceStock.Name(k) + " " + reserve!.Get(k).ToString("0.#")).ToArray();
        var missing = MissingResources(townStock.Resources, a.Input);
        if (a.Output != ResourceKind.Food && townStock.Resources.Get(a.Output) >= 80) missing = $"{ResourceStock.Name(a.Output)}库存已达补货目标 80，暂停新的领料";
        else if (reserved.Length > 0) missing = "为下一发展项目预留：" + string.Join("、", reserved);
        return $"产出：{ResourceStock.Name(a.Output)} {ProductionYield(building, a):0.#} / 批   累计 {building.ProductionBatches} 批\n" + (missing is not null ? missing
            : a.Magic ? "需要天赋 ≥25、训练 ≥8 且魔力足够的到场施作者" : "原料可用，等待工人取料并到场加工");
    }

    private bool CanProduce(Building building, Resident person, Advancement a)
    {
        if (BuildingRace(building.Kind) is { } race && person.Race != race) return false;
        if (ProductionRequirement(building, a) is not null || ProductionYield(building, a) <= 0
            || a.Magic && (person.MagicTalent < 25 || person.MagicTraining < 8 || person.Mana < a.Mana)) return false;
        if (person.Inventory.Get(a.Output) + ProductionYield(building, a) > 1_000_000) return false;
        return MissingResources(person.Inventory, a.Input) is null
            || _settlements.TryGetValue(building.SettlementId, out var town) && WarehouseCanSupply(town, person, a);
    }

    private bool WarehouseCanSupply(Settlement town, Resident person, Advancement a)
    {
        if (a.Output != ResourceKind.Food && town.Resources.Get(a.Output) >= 80) return false;
        if (MissingResources(town.Resources, a.Input) is not null) return false;
        var reserve = _localWorkQueriesActive ? _productionReserves.GetValueOrDefault(town.Id) : LocalDevelopmentReserve(town);
        return AdvancementRules.Resources.Where(k => a.Input.Get(k) > 0).All(k => town.Resources.Get(k) + 0.000001 >= Math.Max(0, a.Input.Get(k) - person.Inventory.Get(k)) + (reserve?.Get(k) ?? 0));
    }

    private bool ActOnProduction(Resident person, Settlement home)
    {
        var goal = person.Agent.Goal;
        if (goal.Kind != AgentGoalKind.Work) return false;
        var building = FindBuilding(goal.TargetEntityId);
        var a = building is null ? null : AdvancementRules.For(building.Kind);
        if (building is null || building.SettlementId != home.Id || a is null || !building.IsCompleted || building.IsUpgrading) return false;
        if (!CanProduce(building, person, a))
        {
            goal.Reason = GetProductionStatus(building.Id); person.Agent.NextThinkTick = State.Tick + 1;
            return true;
        }
        if (MissingResources(person.Inventory, a.Input) is not null)
        {
            goal.TargetX = home.X; goal.TargetY = home.Y;
            goal.Reason = "前往家园取料，亲自运至" + BuildingName(building.Kind);
            if (Distance(person.X, person.Y, home.X, home.Y) > 1)
            { MoveAgentTowards(person, home.X, home.Y); person.Activity = ResidentActivity.Delivering; return true; }
            foreach (var kind in AdvancementRules.Resources)
            {
                var amount = Math.Max(0, a.Input.Get(kind) - person.Inventory.Get(kind));
                home.Resources.Set(kind, Math.Max(0, home.Resources.Get(kind) - amount));
                person.Inventory.Set(kind, person.Inventory.Get(kind) + amount);
            }
        }
        goal.TargetX = building.X; goal.TargetY = building.Y;
        goal.Reason = "携带实际原料，前往" + BuildingName(building.Kind) + "加工";
        if (Distance(person.X, person.Y, building.X, building.Y) > 1)
        { MoveAgentTowards(person, building.X, building.Y); person.Activity = ResidentActivity.Delivering; return true; }
        if (TryWorkAtBuilding(person))
        {
            person.Activity = ResidentActivity.Working;
            person.Agent.Goal = new AgentGoal { Kind = AgentGoalKind.ReturnHome, TargetX = home.X, TargetY = home.Y,
                TargetSettlementId = home.Id, StartedTick = State.Tick, ReviewTick = State.Tick + 100,
                Reason = "加工完成，亲自把产物运回家园入库" };
            person.Agent.NextThinkTick = State.Tick + 100;
        }
        return true;
    }

    private bool Produce(Building building, Resident person, Advancement a)
    {
        if (!CanProduce(building, person, a) || MissingResources(person.Inventory, a.Input) is not null) return false;
        Spend(person.Inventory, a.Input);
        person.Mana -= a.Mana;
        person.Inventory.Set(a.Output, person.Inventory.Get(a.Output) + ProductionYield(building, a));
        RecordHarvest(State.Tiles[Index(building.X, building.Y)], ProductionYield(building, a));
        building.ProductionBatches = Math.Min(1_000_000_000, building.ProductionBatches + 1);
        if (building.ProductionBatches == 1)
        {
            var entry = AddEvent(WorldEventKind.Construction, $"{RequireTown(building.SettlementId).Name}的{BuildingName(building.Kind)}完成首批加工；产物正由{person.Name}运回仓库。",
                building.X, building.Y, EventAction.Delivery, building.SettlementId, person.Id, causeEventId: building.Observation.StartEventId);
            RecordLife(person, "完成" + BuildingName(building.Kind) + "首批实际加工。", entry, PersonalExperienceKind.Achievement);
        }
        return true;
    }
}
