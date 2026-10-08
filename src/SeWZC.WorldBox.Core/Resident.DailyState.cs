namespace SeWZC.WorldBox.Core;

public sealed partial record Resident
{
    /// <summary>一次身体与需求结算的字段结果，供引擎与后续动作合并冻结。</summary>
    /// <param name="Age">结算后的年龄。</param>
    /// <param name="Profession">结算后的职业。</param>
    /// <param name="Health">结算后的生命值。</param>
    /// <param name="Sickness">剩余疫病日数。</param>
    /// <param name="Immunity">免疫截止日序。</param>
    /// <param name="DeathCause">死亡原因。</param>
    /// <param name="DeathTick">死亡日序。</param>
    /// <param name="Activity">结算后的活动。</param>
    /// <param name="Mana">结算后的魔力。</param>
    /// <param name="Hunger">结算后的饥饿程度。</param>
    /// <param name="Thirst">结算后的口渴程度。</param>
    /// <param name="Inventory">扣除需求并计入补给后的随身库存。</param>
    /// <param name="Agent">结算后的认知与行动状态。</param>
    internal readonly record struct DailyState(double Age, Profession Profession, double Health, int Sickness,
        long Immunity, DeathCause DeathCause, long DeathTick, ResidentActivity Activity, double Mana,
        double Hunger, double Thirst, ResourceStock Inventory, AgentState Agent)
    {
        internal Resident Apply(Resident value)
        {
            if (Age == value.Age && Profession == value.Profession && Health == value.Health
                && Sickness == value.SicknessTicks && Immunity == value.DiseaseImmuneUntilTick
                && DeathCause == value.DeathCause && DeathTick == value.DeathTick && Activity == value.Activity
                && Mana == value.Mana && Hunger == value.Hunger && Thirst == value.Thirst
                && Inventory == value.Inventory && ReferenceEquals(Agent, value.Agent)) return value;
            return value with
            {
                Age = Age, Profession = Profession, Health = Health, SicknessTicks = Sickness,
                DiseaseImmuneUntilTick = Immunity, DeathCause = DeathCause, DeathTick = DeathTick,
                Activity = Activity, Mana = Mana, Hunger = Hunger, Thirst = Thirst, Inventory = Inventory, Agent = Agent,
            };
        }
    }
}
