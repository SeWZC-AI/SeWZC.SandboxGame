namespace SeWZC.WorldBox.Core;

public sealed partial record Resident
{
    // 取水与返仓仍由引擎处理实际位置和公共库存；每日需求只转换居民自身状态。
    internal Resident AdvanceNeeds(WorldRules rules, long tick, Tile? tile = null, double elapsedDays = 1,
        bool assistedFeeding = false)
    {
        return CalculateNeeds(rules, tick, new VitalState(Age, Profession, Health, SicknessTicks,
            DiseaseImmuneUntilTick,
            DeathCause, DeathTick, Activity, Mana), tile: tile, elapsedDays: elapsedDays,
            assistedFeeding: assistedFeeding).Apply(this);
    }

    internal Resident AdvanceRecovery(WorldRules rules, long tick, double quality)
    {
        var agent = ResidentNeedsRules.Advance(this, quality);
        var restored = Math.Max(0, Agent.Fatigue - agent.Fatigue) / ResidentNeedsRules.MaximumPercent
                       * ResidentNeedsRules.StaminaCapacity(this);
        var hunger = rules.Hunger
            ? Math.Min(ResidentNeedsRules.MaximumPercent, Hunger + restored * ResidentNeedsRules.RecoveryHungerPerStamina) : 0;
        var thirst = rules.Thirst
            ? Math.Min(ResidentNeedsRules.MaximumPercent, Thirst + restored * ResidentNeedsRules.RecoveryThirstPerStamina) : 0;
        if (Health > 0)
            agent = ReviewNutritionCrisis(rules, tick, agent, hunger, thirst, Inventory);
        return (hunger == Hunger && thirst == Thirst ? this : this with { Hunger = hunger, Thirst = thirst }).WithAgent(agent);
    }

    private AgentState ReviewNutritionCrisis(WorldRules rules, long tick, AgentState agent, double hunger,
        double thirst, ResourceStock supplies)
    {
        var crisis = (rules.Hunger && Hunger <= 60 && hunger > 60 && supplies.Food < .05)
                     || (rules.Thirst && Thirst <= 80 && thirst > 80 && supplies.Water < .025);
        return crisis ? agent with { Goal = agent.Goal with { ReviewTick = tick }, NextThinkTick = tick } : agent;
    }

    internal bool CanConsumeSupplies(long tick, ResidentActivity activity, bool unconscious, bool assistedFeeding,
        double hunger, double thirst) => Health > 0 && tick - MoveStartedTick >= MoveDurationTicks
        && (!unconscious || assistedFeeding)
        && (activity != ResidentActivity.Sleeping || hunger >= ResidentNeedsRules.UrgentNutritionThreshold
            || thirst >= ResidentNeedsRules.UrgentNutritionThreshold);

    private DailyState CalculateNeeds(WorldRules rules, long tick, VitalState vitals, double socialGrowth = .07,
        double deliveredWater = 0, AgentState? arrivedAgent = null, ResourceStock? suppliedInventory = null,
        Tile? tile = null, double elapsedDays = 1, bool assistedFeeding = false)
    {
        var beforeAgent = arrivedAgent ?? Agent;
        var inventory = suppliedInventory ?? Inventory;
        var food = inventory.Food;
        var water = inventory.Water + deliveredWater;
        var hungerGrowth = ResidentNeedsRules.HungerPerDay * elapsedDays;
        var thirstGrowth = ResidentNeedsRules.ThirstGrowthPerDay(vitals.Age, tile) * elapsedDays;
        var thirst = rules.Thirst ? Thirst + thirstGrowth : 0;
        var hunger = rules.Hunger ? Hunger + hungerGrowth : 0;
        var health = vitals.Health;
        var deathCause = vitals.DeathCause;
        var deathTick = vitals.DeathTick;
        var socialNeed = Math.Min(100, beforeAgent.SocialNeed + socialGrowth);
        var unconscious = ResidentNeedsRules.IsUnconscious(beforeAgent, vitals.Activity, Hunger, Thirst);
        var canConsume = CanConsumeSupplies(tick, vitals.Activity, unconscious, assistedFeeding, hunger, thirst)
                         && (tile is null || tile.FireTicks == 0);
        var consumed = false;
        var activity = vitals.Activity == ResidentActivity.Eating ? ResidentActivity.Resting : vitals.Activity;

        void Damage(double amount, DeathCause cause)
        {
            if (health <= 0)
                return;
            health = Math.Max(0, health - amount);
            if (health <= 0)
            {
                deathCause = cause;
                deathTick = tick;
            }
        }

        if (rules.Thirst)
        {
            var reliefPerWater = ResidentNeedsRules.ThirstPerDay
                                 / (WorldEngine.WaterUse(vitals.Age) * ResidentNeedsRules.NormalWaterRequirementRatio);
            var drink = canConsume && thirst >= ResidentNeedsRules.DrinkThreshold
                ? Math.Min(water, Math.Min(thirst / reliefPerWater,
                    ResidentNeedsRules.WaterPerDrink(vitals.Age))) : 0;
            water -= drink;
            thirst -= drink * reliefPerWater;
            consumed |= drink > 0;
            var depletedDays = Math.Clamp((thirst - ResidentNeedsRules.MaximumPercent) / thirstGrowth, 0, 1) * elapsedDays;
            Damage(ResidentNeedsRules.DehydrationDamagePerDay * depletedDays, DeathCause.Dehydration);
        }

        // 脱水先于进食；当天因脱水死亡的居民不再消耗粮食或承受饥饿伤害。
        if (health > 0)
        {
            var dailyUse = WorldEngine.FoodUse(vitals.Age, Race);
            var reliefPerFood = ResidentNeedsRules.HungerPerDay / dailyUse;
            var meal = rules.Hunger && canConsume && hunger >= ResidentNeedsRules.MealThreshold
                ? Math.Min(food, Math.Min(hunger / reliefPerFood, dailyUse / ResidentNeedsRules.MealsPerDay)) : 0;
            food -= meal;
            hunger -= meal * reliefPerFood;
            consumed |= meal > 0;
            if (rules.Hunger)
            {
                var depletedDays = Math.Clamp((hunger - ResidentNeedsRules.MaximumPercent) / hungerGrowth, 0, 1) * elapsedDays;
                Damage(ResidentNeedsRules.StarvationDamagePerDay * depletedDays, DeathCause.Starvation);
            }
        }
        else
            hunger = Hunger;

        hunger = Math.Clamp(hunger, 0, ResidentNeedsRules.MaximumPercent);
        thirst = Math.Clamp(thirst, 0, ResidentNeedsRules.MaximumPercent);
        if (health > 0 && ResidentNeedsRules.IsUnconscious(beforeAgent, activity, hunger, thirst))
            activity = ResidentActivity.Unconscious;
        else if (consumed && !unconscious)
            activity = ResidentActivity.Eating;

        var supplies = inventory with { Food = food, Water = water };
        var agent = socialNeed == beforeAgent.SocialNeed ? beforeAgent : beforeAgent with { SocialNeed = socialNeed };
        if (health > 0)
            agent = ReviewNutritionCrisis(rules, tick, agent, hunger, thirst, supplies);
        return new DailyState(vitals.Age, vitals.Profession, health, vitals.Sickness, vitals.Immunity, deathCause,
            deathTick,
            activity, vitals.Mana, hunger, thirst, supplies, agent);
    }
}
