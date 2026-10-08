namespace SeWZC.WorldBox.Core;

public sealed partial record Resident
{
    // 随机感染和成年职业由引擎提供；基础生命过程只根据输入产生新的居民状态。
    internal Resident AdvanceVitals(WorldRules rules, Tile tile, long tick, Profession profession, int infectionDuration,
        double manaRecovery = 0)
    {
        var vitals = CalculateVitals(rules, tile, tick, profession, infectionDuration, manaRecovery);
        return CalculateDailyVitals(vitals).Apply(this);
    }

    internal Resident AdvanceDay(WorldRules rules, Tile tile, long tick, Profession profession, int infectionDuration,
        double manaRecovery, bool consumeNeeds, double socialGrowth = .07, double deliveredWater = 0,
        int arrivedTile = -1, ResourceStock? suppliedInventory = null)
        => CalculateDay(rules, tile, tick, profession, infectionDuration, manaRecovery, consumeNeeds,
            socialGrowth, deliveredWater, arrivedTile, suppliedInventory).Apply(this);

    internal DailyState CalculateDay(WorldRules rules, Tile tile, long tick, Profession profession, int infectionDuration,
        double manaRecovery, bool consumeNeeds, double socialGrowth = .07, double deliveredWater = 0,
        int arrivedTile = -1, ResourceStock? suppliedInventory = null)
    {
        var vitals = CalculateVitals(rules, tile, tick, profession, infectionDuration, manaRecovery);
        var arrivedAgent = arrivedTile >= 0 ? Agent.RememberRouteTile(arrivedTile) : Agent;
        var next = consumeNeeds && vitals.Health > 0 ? CalculateNeeds(rules, tick, vitals, socialGrowth, deliveredWater, arrivedAgent, suppliedInventory)
            : CalculateDailyVitals(vitals, vitals.Health > 0 ? socialGrowth : 0, deliveredWater, arrivedAgent, suppliedInventory);
        if (consumeNeeds && vitals.Health > 0 && !Agent.Goal.PlayerDirected
            && (SicknessTicks == 0 && vitals.Sickness > 0 || Health >= 40 && vitals.Health < 40))
            next = next with { Agent = next.Agent with
            {
                Goal = next.Agent.Goal with { ReviewTick = tick }, NextThinkTick = tick,
            } };
        return next;
    }

    private VitalState CalculateVitals(WorldRules rules, Tile tile, long tick, Profession profession,
        int infectionDuration, double manaRecovery)
    {
        var age = rules.Aging ? Math.Min(1000, Age + 1d / 120) : Age;
        var health = Health;
        var sickness = SicknessTicks;
        var immunity = DiseaseImmuneUntilTick;
        var deathCause = DeathCause;
        var deathTick = DeathTick;
        var maxAge = WorldEngine.Lifespan(Race);

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

        if (rules.Aging && age > maxAge) Damage(.5, DeathCause.OldAge);
        if ((!rules.Hunger || Hunger <= 80) && health > 0 && sickness == 0 && age <= maxAge &&
            (!rules.Thirst || Thirst <= 95))
            health = Math.Min(100, health + .15);
        if (tile.FireTicks > 0) Damage(4, DeathCause.Fire);
        if (sickness > 0)
        {
            sickness--;
            if (rules.Disease) Damage(.2, DeathCause.Disease);
            if (sickness == 0) immunity = tick + 180;
        }
        else if (infectionDuration > 0) sickness = infectionDuration;

        var activity = health > 0 && sickness > 0 ? ResidentActivity.Sick : Activity;
        var mana = health > 0 ? Math.Min(100, Mana + manaRecovery) : Mana;
        return new(age, profession, health, sickness, immunity, deathCause, deathTick, activity, mana);
    }

    private DailyState CalculateDailyVitals(VitalState vitals, double socialGrowth = 0, double deliveredWater = 0,
        AgentState? arrivedAgent = null, ResourceStock? suppliedInventory = null)
    {
        var agent = arrivedAgent ?? Agent;
        var socialNeed = Math.Min(100, agent.SocialNeed + socialGrowth);
        var inventory = suppliedInventory ?? Inventory;
        return new(vitals.Age, vitals.Profession, vitals.Health, vitals.Sickness, vitals.Immunity, vitals.DeathCause,
            vitals.DeathTick, vitals.Activity, vitals.Mana, Hunger, Thirst,
            deliveredWater > 0 ? inventory with { Water = inventory.Water + deliveredWater } : inventory,
            socialNeed == agent.SocialNeed ? agent : agent with { SocialNeed = socialNeed });
    }

    /// <summary>一次身体结算的不可变结果，供需求结算合并提交。</summary>
    /// <param name="Age">结算后的年龄。</param>
    /// <param name="Profession">成年后的职业。</param>
    /// <param name="Health">结算后的生命值。</param>
    /// <param name="Sickness">剩余疫病日数。</param>
    /// <param name="Immunity">免疫截止日序。</param>
    /// <param name="DeathCause">死亡原因。</param>
    /// <param name="DeathTick">死亡日序。</param>
    /// <param name="Activity">身体状态对应的活动。</param>
    /// <param name="Mana">恢复后的魔力。</param>
    private readonly record struct VitalState(double Age, Profession Profession, double Health, int Sickness,
        long Immunity, DeathCause DeathCause, long DeathTick, ResidentActivity Activity, double Mana);
}
