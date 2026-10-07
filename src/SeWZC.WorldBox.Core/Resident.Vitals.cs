namespace SeWZC.WorldBox.Core;

public sealed partial record Resident
{
    // 随机感染和成年职业由引擎提供；基础生命过程只根据输入产生新的居民状态。
    internal Resident AdvanceVitals(WorldRules rules, Tile tile, long tick, Profession profession, int infectionDuration)
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
        if (age == Age && profession == Profession && health == Health && sickness == SicknessTicks &&
            immunity == DiseaseImmuneUntilTick && deathCause == DeathCause && deathTick == DeathTick && activity == Activity)
            return this;

        return this with
        {
            Age = age,
            Profession = profession,
            Health = health,
            SicknessTicks = sickness,
            DiseaseImmuneUntilTick = immunity,
            DeathCause = deathCause,
            DeathTick = deathTick,
            Activity = activity,
        };
    }
}
