namespace SeWZC.WorldBox.Core;

public sealed partial record Resident
{
    // 纯身体计算只返回变动字段，引擎可与后续动作合并冻结，不产生临时居民实体。
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
