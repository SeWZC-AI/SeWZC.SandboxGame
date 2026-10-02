namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    public string? ResearchPrerequisiteError(int settlementId, ResearchKind kind)
    {
        if (kind == ResearchKind.SignalNetwork && !HasResearch(settlementId, ResearchKind.Logistics)) return "需要先掌握驿路运输";
        var advancement = AdvancementRules.For(kind);
        if ((kind == ResearchKind.ArcaneArts || advancement?.Magic == true) && !State.Society.MagicEnabled)
            return "世界规则已关闭新的魔法发展";
        if (advancement is null) return null;
        var missing = advancement.Prerequisites.Where(p => !HasResearch(settlementId, p)).Select(ResearchName).ToArray();
        return missing.Length == 0 ? null : "需要先掌握" + string.Join("、", missing);
    }

    public static string ResearchDescription(ResearchKind kind)
    {
        var a = AdvancementRules.For(kind);
        return a is null ? kind == ResearchKind.SignalNetwork ? "前置：驿路运输；解锁信号塔通信。" : "在已建成学舍由实际到场人员推进。"
            : $"{(a.Magic ? "魔法" : "科技")}路线 · {a.Stage}\n前置：{string.Join("、", a.Prerequisites.Select(ResearchName))}\n解锁{a.FacilityName}：{ProductionRecipe(a.Facility)}";
    }

    public string GetAdvancementStage(int settlementId)
    {
        string Stage(bool magic) => AdvancementRules.All.LastOrDefault(a => a.Magic == magic && HasResearch(settlementId, a.Research))?.Stage
            ?? (magic ? HasResearch(settlementId, ResearchKind.ArcaneArts) ? "基础奥术" : "未发展" : "古代");
        return $"科技：{Stage(false)} · 魔法：{Stage(true)}";
    }

    public static string ProductionRecipe(BuildingKind kind)
    {
        var a = AdvancementRules.For(kind);
        return a is null ? "" : $"每批 {AdvancementRules.Stock(a.Input)}{(a.Mana > 0 ? $"、施作者魔力 {a.Mana:0}" : "")} → {ResourceStock.Name(a.Output)} {a.Yield:0}\n工人先到粮仓取料，现场加工后实地运回；农业产出仍受肥力与干旱影响。";
    }

    private string? ProductionRequirement(Building building, Advancement a)
    {
        if (!building.IsCompleted) return "等待施工完成";
        if (building.Health < 50 || State.Tiles[Index(building.X, building.Y)].FireTicks > 0) return "设施损坏或正在燃烧";
        if (!HasResearch(building.SettlementId, a.Research)
            || a.Prerequisites.Any(p => !HasResearch(building.SettlementId, p))) return "缺少当地运营知识：" + ResearchName(a.Research) + "及其前置";
        return null;
    }

    private double ProductionYield(Building building, Advancement a)
    {
        if (a.Output != ResourceKind.Food) return a.Yield;
        var tile = State.Tiles[Index(building.X, building.Y)];
        return a.Yield * tile.Fertility / 100d * (tile.DroughtTicks > 0 ? .18 : 1);
    }

    public string GetProductionStatus(int buildingId)
    {
        var building = State.Society.Buildings.FirstOrDefault(b => b.Id == buildingId);
        var a = building is null ? null : AdvancementRules.For(building.Kind);
        if (a is null || building is null) return "";
        var requirement = ProductionRequirement(building, a);
        if (requirement is not null) return requirement;
        if (ProductionYield(building, a) <= 0) return "土地无法产粮，需要恢复肥力";
        var missing = MissingResources(RequireTown(building.SettlementId).Resources, a.Input);
        return $"累计加工 {building.ProductionBatches} 批 · " + (missing is not null ? missing + "；等待材料实际运到"
            : a.Magic ? "需要天赋 ≥25、训练 ≥8 且魔力足够的到场施作者" : "原料可用，等待工人取料并到场加工");
    }

    private bool CanProduce(Building building, Resident person, Advancement a)
    {
        if (ProductionRequirement(building, a) is not null || ProductionYield(building, a) <= 0
            || a.Magic && (person.MagicTalent < 25 || person.MagicTraining < 8 || person.Mana < a.Mana)) return false;
        if (person.Inventory.Get(a.Output) + ProductionYield(building, a) > 1_000_000) return false;
        return MissingResources(person.Inventory, a.Input) is null
            || _settlements.TryGetValue(building.SettlementId, out var town) && MissingResources(town.Resources, a.Input) is null;
    }

    private bool ActOnProduction(Resident person, Settlement home)
    {
        var goal = person.Agent.Goal;
        if (goal.Kind != AgentGoalKind.Work) return false;
        var building = State.Society.Buildings.FirstOrDefault(b => b.Id == goal.TargetEntityId && b.SettlementId == home.Id);
        var a = building is null ? null : AdvancementRules.For(building.Kind);
        if (building is null || a is null || !building.IsCompleted) return false;
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
