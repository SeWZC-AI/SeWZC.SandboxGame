namespace SeWZC.WorldBox.Core;

public sealed partial record Resident
{
    // 取水与返仓仍由引擎处理实际位置和公共库存；每日需求只转换居民自身状态。
    internal Resident AdvanceNeeds(WorldRules rules, long tick)
    {
        var inventory = Inventory;
        var thirst = Thirst;
        var hunger = Hunger;
        var health = Health;
        var deathCause = DeathCause;
        var deathTick = DeathTick;
        var socialNeed = Math.Min(100, Agent.SocialNeed + .07);

        void Damage(double amount, DeathCause cause)
        {
            if (health <= 0) return;
            health = Math.Max(0, health - amount);
            if (health <= 0)
            {
                deathCause = cause;
                deathTick = tick;
            }
        }

        if (rules.Thirst)
        {
            var use = WorldEngine.WaterUse(this);
            var drink = Math.Min(use, inventory.Water);
            inventory = inventory with { Water = inventory.Water - drink };
            thirst = Math.Clamp(thirst + (drink >= use - .000001 ? -3 : .6 * (1 - drink / use)), 0, 100);
            if (thirst > 95) Damage(.25, DeathCause.Dehydration);
        }
        else thirst = 0;

        // 脱水先于进食；当天因脱水死亡的居民不再消耗粮食或承受饥饿伤害。
        if (health > 0)
        {
            var use = rules.Hunger ? WorldEngine.FoodUse(this) : 0;
            var meal = Math.Min(use, inventory.Food);
            inventory = inventory with { Food = inventory.Food - meal };
            hunger = Math.Clamp(hunger + (meal >= use - .000001 ? -3 : .8 * (1 - meal / use)), 0, 100);
            if (rules.Hunger && hunger > 80) Damage(.30, DeathCause.Starvation);
        }

        if (inventory == Inventory && thirst == Thirst && hunger == Hunger && health == Health &&
            deathCause == DeathCause && deathTick == DeathTick && socialNeed == Agent.SocialNeed)
            return this;
        return this with
        {
            Inventory = inventory,
            Thirst = thirst,
            Hunger = hunger,
            Health = health,
            DeathCause = deathCause,
            DeathTick = deathTick,
            Agent = socialNeed == Agent.SocialNeed ? Agent : Agent with { SocialNeed = socialNeed },
        };
    }
}
