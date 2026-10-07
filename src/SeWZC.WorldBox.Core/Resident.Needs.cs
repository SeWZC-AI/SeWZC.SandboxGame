namespace SeWZC.WorldBox.Core;

public sealed partial record Resident
{
    // 取水与返仓仍由引擎处理实际位置和公共库存；每日需求只转换居民自身状态。
    internal Resident AdvanceNeeds(WorldRules rules, long tick)
        => AdvanceNeeds(rules, tick, new VitalState(Age, Profession, Health, SicknessTicks, DiseaseImmuneUntilTick,
            DeathCause, DeathTick, Activity, Mana));

    private Resident AdvanceNeeds(WorldRules rules, long tick, VitalState vitals, double socialGrowth = .07,
        double deliveredWater = 0)
    {
        var food = Inventory.Food;
        var water = Inventory.Water + deliveredWater;
        var thirst = Thirst;
        var hunger = Hunger;
        var health = vitals.Health;
        var deathCause = vitals.DeathCause;
        var deathTick = vitals.DeathTick;
        var socialNeed = Math.Min(100, Agent.SocialNeed + socialGrowth);

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
            var use = vitals.Age < 14 ? .015 : .025;
            var drink = Math.Min(use, water);
            water -= drink;
            thirst = Math.Clamp(thirst + (drink >= use - .000001 ? -3 : .6 * (1 - drink / use)), 0, 100);
            if (thirst > 95) Damage(.25, DeathCause.Dehydration);
        }
        else thirst = 0;

        // 脱水先于进食；当天因脱水死亡的居民不再消耗粮食或承受饥饿伤害。
        if (health > 0)
        {
            var use = rules.Hunger ? vitals.Age < 14 ? .02 : Race == RaceKind.Orc ? .052 : .04 : 0;
            var meal = Math.Min(use, food);
            food -= meal;
            hunger = Math.Clamp(hunger + (meal >= use - .000001 ? -3 : .8 * (1 - meal / use)), 0, 100);
            if (rules.Hunger && hunger > 80) Damage(.30, DeathCause.Starvation);
        }

        var crisis = health > 0 && (rules.Hunger && Hunger <= 60 && hunger > 60 && food < .05
            || rules.Thirst && Thirst <= 80 && thirst > 80 && water < .025);

        if (food == Inventory.Food && water == Inventory.Water && thirst == Thirst && hunger == Hunger && health == Health &&
            deathCause == DeathCause && deathTick == DeathTick && socialNeed == Agent.SocialNeed && vitals.Age == Age && vitals.Profession == Profession
            && vitals.Sickness == SicknessTicks && vitals.Immunity == DiseaseImmuneUntilTick
            && vitals.Activity == Activity && vitals.Mana == Mana && !crisis)
            return this;
        var agent = socialNeed == Agent.SocialNeed ? Agent : Agent with { SocialNeed = socialNeed };
        if (crisis) agent = agent with { Goal = agent.Goal with { ReviewTick = tick }, NextThinkTick = tick };
        return this with
        {
            Age = vitals.Age,
            Profession = vitals.Profession,
            SicknessTicks = vitals.Sickness,
            DiseaseImmuneUntilTick = vitals.Immunity,
            Activity = vitals.Activity,
            Mana = vitals.Mana,
            Inventory = Inventory with { Food = food, Water = water },
            Thirst = thirst,
            Hunger = hunger,
            Health = health,
            DeathCause = deathCause,
            DeathTick = deathTick,
            Agent = agent,
        };
    }
}
