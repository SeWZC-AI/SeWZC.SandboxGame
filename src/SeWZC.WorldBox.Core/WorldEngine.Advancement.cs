using System.Collections.Immutable;
using SeWZC.WorldBox.Core.Runtime;

namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    /// <summary>检查魔法开关和本地研究前置条件，满足时返回空值，否则返回限制原因。</summary>
    /// <param name="settlementId">聚落 ID。</param>
    /// <param name="research">研究项目。</param>
    public string? ResearchPrerequisiteError(int settlementId, Advancement research)
    {
        if (research.Magic && !Current.Society.MagicEnabled)
            return "世界规则已关闭新的魔法发展";
        List<string>? missing = null;
        foreach (var prerequisite in research.Prerequisites.AsSpan())
            if (!HasResearch(settlementId, prerequisite))
                (missing ??= []).Add(prerequisite.Name);
        return missing is null ? null : "需要先掌握" + string.Join("、", missing);
    }

    private bool HasResearchPrerequisites(int settlementId, ImmutableArray<Advancement> prerequisites)
    {
        foreach (var prerequisite in prerequisites.AsSpan())
            if (!HasResearch(settlementId, prerequisite))
                return false;
        return true;
    }

    /// <summary>根据本地研究和文明达成情况返回发展阶段名称。</summary>
    /// <param name="settlementId">聚落 ID。</param>
    public string GetAdvancementStage(int settlementId)
    {
        string Stage(bool magic)
        {
            return GetCivilizationProgress(settlementId, magic).Achieved
                ? magic ? "魔法帝国" : "科技帝国"
                : (magic
                      ? new[] { Advancement.AetherMastery, Advancement.RunicEngineering, Advancement.Crystalcraft }
                      : new[]
                      {
                          Advancement.AdvancedComputing, Advancement.Aviation, Advancement.Automation,
                          Advancement.Electrification, Advancement.Industry,
                      })
                  .Where(k => HasResearch(settlementId, k)).Select(k => k.Stage).FirstOrDefault()
                  ?? (magic ? HasResearch(settlementId, Advancement.ArcaneArts) ? "基础奥术" : "未发展" : "古代");
        }

        return $"科技：{Stage(false)}\n魔法：{Stage(true)}";
    }

    /// <summary>返回设施的生产配方说明。</summary>
    /// <param name="kind">设施类别。</param>
    public static string ProductionRecipe(BuildingKind kind)
    {
        return ProductionRules.For(kind)?.Description ?? "";
    }

    private string? ProductionRequirement(BuildingCursor building, ProductionRecipe recipe)
    {
        if (!BuildingGroundOwned(building))
            return "所在地已脱离城镇占领区域，暂停运营";
        if (!building.Enabled)
            return "玩家已停用，恢复运营后才会安排工作";
        if (!building.IsCompleted)
            return "等待施工完成";
        if (building.IsUpgrading)
            return "正在升级或改向，暂停生产";
        if (building.Health < 50)
            return "设施受损，需要修复后运营";
        if (Current.Tiles[Index(building.X, building.Y)].FireTicks > 0)
            return "设施所在地正在燃烧，暂停生产";
        if (!HasResearch(building.SettlementId, recipe.Research)
            || !HasResearchPrerequisites(building.SettlementId, recipe.Research.Prerequisites))
            return "缺少当地运营知识：" + recipe.Research.Name + "及其前置";
        return null;
    }

    private double ProductionYield(BuildingCursor building, ProductionRecipe recipe)
    {
        var multiplier = (recipe.Output == ResourceKind.Food &&
                          HasResearch(building.SettlementId, Advancement.Irrigation)
                             ? 1.25
                             : 1)
                         * (building.Kind is BuildingKind.Foundry or BuildingKind.DwarvenForge &&
                            HasResearch(building.SettlementId, Advancement.EfficientSmelting)
                             ? 1.25
                             : 1)
                         * (building.Kind == BuildingKind.PowerPlant &&
                            HasResearch(building.SettlementId, Advancement.EnergyRecycling)
                             ? 1.5
                             : 1)
                         * (building.Kind is BuildingKind.Crystallizer or BuildingKind.AetherForge &&
                            HasResearch(building.SettlementId, Advancement.Leylines)
                             ? 1.25
                             : 1);
        if (recipe.Output != ResourceKind.Food)
            return recipe.Yield * building.Efficiency * multiplier;
        var tile = Current.Tiles[Index(building.X, building.Y)];
        return recipe.Yield * building.Efficiency * multiplier * tile.Fertility / 100d *
               (tile.DroughtTicks > 0 ? .18 : 1);
    }

    /// <summary>返回设施当前的生产状态说明。</summary>
    /// <param name="buildingId">待操作建筑的稳定 ID。</param>
    public string GetProductionStatus(int buildingId)
    {
        var building = Current.Buildings.FirstOrDefault(b => b.Id == buildingId);
        var recipe = building is null ? null : ProductionRules.For(building.Kind);
        if (building is null)
            return "建筑已不存在";
        if (!BuildingGroundOwned(building))
            return "所在地已脱离城镇占领区域，暂停运营";
        if (building.Health <= 0)
            return "建筑已损毁，等待重建";
        if (building.Health < 50)
            return "建筑受损，需要修复后工作";
        if (Current.Tiles[Index(building.X, building.Y)].FireTicks > 0)
            return "正在燃烧，暂停工作";
        var workers = Current.Tick - building.LastWorkedTick <= 1 ? building.Workers.Count : 0;
        if (!building.IsCompleted)
        {
            return
                $"施工：{building.ConstructionProgress / building.ConstructionRequired:P0}   到场工人 {workers}/{building.WorkSlots}";
        }

        if (building.IsUpgrading)
        {
            return
                $"{(building.PendingDirection.HasValue ? "改向" : "升级")}：{building.UpgradeProgress:0.#} / {building.UpgradeRequired:0}\n等待居民到场施工";
        }

        if (!building.Enabled)
            return "已停用";
        if (!CanBuildRacialFacility(building.SettlementId, building.Kind))
            return "缺少该族成年居民，暂停运营";
        if (IsHusbandry(building.Kind))
        {
            return
                $"养殖：{(building.LivestockKind == WildlifeKind.None ? "等待取得种群" : WildlifeName(building.LivestockKind))}  {building.LivestockPopulation:0.##} / {LivestockCapacity(building):0.#}\n" +
                BuildingDescription(building.Kind);
        }

        if (recipe is null)
        {
            var town = RequireTown(building.SettlementId);
            if (building.Kind == BuildingKind.TownCenter && town.IsExpanding)
            {
                return
                    $"组织城镇扩充：{town.ExpansionProgress:0.#} / {town.ExpansionRequired:0}\n到场工人 {workers}/{building.WorkSlots}";
            }

            var research = Current.Society.Research.First(r => r.SettlementId == town.Id);
            var activity = building.Kind switch
            {
                BuildingKind.Farm => $"产出：粮食   累计采收 {Current.Tiles[Index(building.X, building.Y)].Harvested:0.#}",
                BuildingKind.Workshop => "产出：附近实际可采的木材、石材与矿石",
                BuildingKind.Academy => research.ActiveProject is { } kind
                    ? $"正在研究：{kind.Name}   {research.Progress / research.RequiredProgress:P0}"
                    : "等待当地研究立项",
                BuildingKind.Waystation => "功能：值守驿站，改善附近信使通行",
                BuildingKind.SignalTower => "功能：值守无线通信，传递实际收到的消息",
                BuildingKind.ArcaneSanctum => "功能：训练法术，提升到场居民的魔法熟练度",
                BuildingKind.Infirmary => "功能：治疗附近受伤与患病居民",
                BuildingKind.TownCenter => $"家园粮仓：粮食 {town.Resources.Food:0.#}   木材 {town.Resources.Wood:0.#}",
                _ => BuildingDescription(building.Kind),
            };
            return activity +
                   (PassiveFacility(building) ||
                    building.Kind is BuildingKind.MountainPass or BuildingKind.Bridge or BuildingKind.TownCenter
                       ? ""
                       : $"\n到场工作 {workers}/{building.WorkSlots} 人");
        }

        if (!CanBuildRacialFacility(building.SettlementId, building.Kind))
            return "缺少该族成年居民，暂停运营";
        var requirement = ProductionRequirement(building, recipe);
        if (requirement is not null)
            return requirement;
        if (ProductionYield(building, recipe) <= 0)
            return "土地无法产粮，需要恢复肥力";
        var townStock = RequireTown(building.SettlementId);
        var reserve = LocalDevelopmentReserve(townStock);
        var reserved = ResourceStock.Kinds.Where(k => recipe.Input.Get(k) > 0 && reserve.Get(k) > 0
                                                                              && townStock.Resources.Get(k) <
                                                                              recipe.Input.Get(k) + reserve.Get(k))
            .Select(k => ResourceStock.Name(k) + " " + reserve.Get(k).ToString("0.#")).ToArray();
        var missing = MissingResources(townStock.Resources, recipe.Input);
        if (building.ProductionBatches > 0 &&
            townStock.Resources.Get(recipe.Output) >= ProductionStockTarget(townStock, recipe.Output))
            missing = $"{ResourceStock.Name(recipe.Output)}库存已充足，暂停新的领料";
        else if (reserved.Length > 0)
            missing = "为下一发展项目预留：" + string.Join("、", reserved);
        return
            $"产出：{ResourceStock.Name(recipe.Output)} {ProductionYield(building, recipe):0.#} / 批   累计 {building.ProductionBatches} 批\n" +
            (missing is not null ? missing
                : recipe.Research.Magic ? "需要天赋 ≥25、训练 ≥8 且魔力足够的到场施作者" : "原料可用，等待工人取料并到场加工");
    }

    private bool CanProduce(BuildingCursor building, ResidentCursor person, ProductionRecipe recipe)
    {
        if (BuildingRace(building.Kind) is { } race && person.Race != race)
            return false;
        if (ProductionRequirement(building, recipe) is not null || ProductionYield(building, recipe) <= 0
                                                                || (recipe.Research.Magic && (person.MagicTalent < 25 ||
                                                                    person.MagicTraining < 8 ||
                                                                    person.Mana < recipe.Mana)))
            return false;
        if (person.Inventory.Get(recipe.Output) + ProductionYield(building, recipe) > 1_000_000)
            return false;
        return HasProductionInputs(person.Inventory, recipe)
               || (_settlements.TryGetValue(building.SettlementId, out var town) &&
                   WarehouseCanSupply(town, person, recipe, building.ProductionBatches == 0));
    }

    private static double ProductionStockTarget(SettlementCursor town, ResourceKind kind)
    {
        return kind switch
        {
            ResourceKind.Food => Math.Max(60, town.Population * 3),
            ResourceKind.Boats or ResourceKind.Aircraft => 4,
            ResourceKind.Tools => Math.Max(12, town.Population * .1),
            ResourceKind.Medicine => Math.Max(8, town.Population * .1),
            ResourceKind.Ammunition => 32,
            _ => 80,
        };
    }

    private bool WarehouseCanSupply(SettlementCursor town, ResidentCursor person, ProductionRecipe recipe,
        bool firstBatch)
    {
        if (!firstBatch && town.Resources.Get(recipe.Output) >= ProductionStockTarget(town, recipe.Output))
            return false;
        var reserve = LocalDevelopmentReserve(town);
        for (var i = 0; i < recipe.InputResources.Length; i++)
        {
            var k = recipe.InputResources[i];
            var available = town.Resources.Get(k) + .000001;
            if (available < recipe.Input.Get(k) ||
                available < Math.Max(0, recipe.Input.Get(k) - person.Inventory.Get(k)) + reserve.Get(k))
                return false;
        }

        return true;
    }

    private static bool HasProductionInputs(in ResourceStock stock, ProductionRecipe recipe)
    {
        for (var i = 0; i < recipe.InputResources.Length; i++)
        {
            var kind = recipe.InputResources[i];
            if (stock.Get(kind) + .000001 < recipe.Input.Get(kind))
                return false;
        }

        return true;
    }

    private bool ActOnProduction(ResidentCursor person, SettlementCursor home)
    {
        var goal = person.Agent.Goal;
        if (goal.Kind != AgentGoalKind.Work)
            return false;
        var building = FindBuilding(goal.TargetEntityId);
        var recipe = building is null ? null : ProductionRules.For(building.Kind);
        if (building is null || building.SettlementId != home.Id || recipe is null || !building.IsCompleted ||
            building.IsUpgrading)
            return false;
        if (!CanProduce(building, person, recipe))
        {
            person.Agent = person.Agent.WithGoal(goal = goal with { Reason = GetProductionStatus(building.Id) });
            person.Agent = person.Agent with { NextThinkTick = Current.Tick + 1 };
            return true;
        }

        if (!HasProductionInputs(person.Inventory, recipe))
        {
            person.Agent = person.Agent.WithGoal(goal = goal with
            {
                TargetX = home.X, TargetY = home.Y, Reason = "前往家园取料，亲自运至" + BuildingName(building.Kind),
            });
            if (Distance(person.X, person.Y, home.X, home.Y) > 1)
            {
                MoveAgentTowards(person, home.X, home.Y);
                person.Activity = ResidentActivity.Delivering;
                return true;
            }

            // 每趟只携带有限批次原料，并为下一项本地计划保留库存；每次结算仍须实际到场并持有原料。
            var reserve = LocalDevelopmentReserve(home);
            var batches = 4d;
            foreach (var kind in recipe.InputResources)
                batches = Math.Min(batches,
                    Math.Floor(Math.Max(0, home.Resources.Get(kind) - reserve.Get(kind)) /
                               recipe.Input.Get(kind)));
            batches = Math.Max(1, batches);
            foreach (var kind in recipe.InputResources)
            {
                var personalReserve = kind == ResourceKind.Food ? TravelReserve(person) :
                    kind == ResourceKind.Water ? WaterReserve(person) : 0;
                var amount = Math.Min(home.Resources.Get(kind),
                    Math.Max(0, recipe.Input.Get(kind) * batches + personalReserve - person.Inventory.Get(kind)));
                home.Resources = home.Resources.WithAmount(kind, Math.Max(0, home.Resources.Get(kind) - amount));
                person.Inventory = person.Inventory.WithAmount(kind, person.Inventory.Get(kind) + amount);
            }
        }

        person.Agent = person.Agent.WithGoal(goal = goal with
        {
            TargetX = building.X, TargetY = building.Y, Reason = "携带实际原料，前往" + BuildingName(building.Kind) + "加工",
        });
        if (Distance(person.X, person.Y, building.X, building.Y) > 1)
        {
            MoveAgentTowards(person, building.X, building.Y);
            person.Activity = ResidentActivity.Delivering;
            return true;
        }

        if (TryWorkAtBuilding(person))
        {
            person.Activity = ResidentActivity.Working;
            if (HasProductionInputs(person.Inventory, recipe) && person.Inventory.Get(recipe.Output) <
                                                              ProductionYield(building, recipe) * 4
                                                              && (!recipe.Research.Magic || person.Mana >= recipe.Mana))
            {
                person.Agent = person.Agent with { NextThinkTick = Current.Tick + 4 };
                return true;
            }

            var previous = person.Agent.Goal;
            person.Agent = person.Agent.WithGoal(new AgentGoal
            {
                Kind = AgentGoalKind.ReturnHome,
                TargetX = home.X,
                TargetY = home.Y,
                TargetSettlementId = home.Id,
                StartedTick = Current.Tick,
                ReviewTick = Current.Tick + 100,
                Reason = "加工完成，亲自把产物运回家园入库",
            });
            ChangeWorkReservation(previous, person.Agent.Goal);
            person.Agent = person.Agent with { NextThinkTick = Current.Tick + 100 };
        }

        return true;
    }

    private bool Produce(BuildingCursor building, ResidentCursor person, ProductionRecipe recipe)
    {
        if (!CanProduce(building, person, recipe) || !HasProductionInputs(person.Inventory, recipe))
            return false;
        var batches = 1;
        foreach (var kind in recipe.InputResources)
            batches = Math.Min(batches, (int)Math.Min(batches,
                Math.Floor((person.Inventory.Get(kind) + .000001) / recipe.Input.Get(kind))));
        if (recipe.Mana > 0)
            batches = Math.Min(batches, (int)Math.Min(batches, Math.Floor(person.Mana / recipe.Mana)));
        var yield = ProductionYield(building, recipe);
        var netYield = yield - recipe.Input.Get(recipe.Output);
        if (netYield > 0)
        {
            batches = Math.Min(batches, (int)Math.Min(batches,
                Math.Floor((1_000_000 - person.Inventory.Get(recipe.Output)) / netYield)));
        }

        if (batches <= 0)
            return false;
        var inventory = Spend(person.Inventory, recipe.Input.ToStock().Scale(batches));
        inventory = inventory.WithAmount(recipe.Output, inventory.Get(recipe.Output) + yield * batches);
        person.Inventory = inventory;
        person.Mana -= recipe.Mana * batches;
        RecordHarvest(Current.Tiles[Index(building.X, building.Y)], yield * batches);
        var firstBatch = building.ProductionBatches == 0;
        building.ProductionBatches = Math.Min(1_000_000_000, building.ProductionBatches + batches);
        if (firstBatch)
        {
            var entry = AddEvent(WorldEventKind.Construction,
                $"{RequireTown(building.SettlementId).Name}的{BuildingName(building.Kind)}完成首批加工；产物正由{person.Name}运回仓库。",
                building.X, building.Y, EventAction.Delivery, building.SettlementId, person.Id,
                building.Observation.StartEventId);
            RecordLife(person, "完成" + BuildingName(building.Kind) + "首批实际加工。", entry,
                PersonalExperienceKind.Achievement);
        }

        return true;
    }
}
