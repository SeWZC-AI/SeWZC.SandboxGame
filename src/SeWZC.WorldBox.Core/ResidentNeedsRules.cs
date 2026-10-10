namespace SeWZC.WorldBox.Core;

/// <summary>居民睡眠、体力及成长的可配置规则。</summary>
public static class ResidentNeedsRules
{
    /// <summary>需求状态采用的百分比上限。</summary>
    public const double MaximumPercent = 100;
    /// <summary>普通人类的睡眠上限。</summary>
    public const double BaseSleepCapacity = 100;
    /// <summary>普通人类的体力上限。</summary>
    public const double BaseStaminaCapacity = 100;
    /// <summary>维持正常劳动效率所需的最低需求比例。</summary>
    public const double FullEfficiencyThreshold = .5;
    /// <summary>昏迷后恢复行动所需的睡眠与体力比例。</summary>
    public const double ConsciousRecoveryThreshold = FullEfficiencyThreshold;
    /// <summary>普通人类能维持正常效率的连续清醒 tick 数。</summary>
    public const int NormalAwakeTicks = 16;
    /// <summary>普通人类每日正常睡眠的 tick 数。</summary>
    public const int NormalSleepTicks = SimulationTime.TicksPerDay - NormalAwakeTicks;
    /// <summary>正常每日体力劳动的 tick 数。</summary>
    public const int NormalWorkTicks = SimulationTime.ReturnHomeTick - SimulationTime.WakeTick;
    /// <summary>普通人类每 tick 的持续睡眠消耗。</summary>
    public const double SleepConsumptionPerTick = BaseSleepCapacity * (1 - FullEfficiencyThreshold) / NormalAwakeTicks;
    /// <summary>普通人类每睡眠 tick 的恢复量，包含抵扣持续消耗的部分。</summary>
    public const double SleepRecoveryPerTick = SleepConsumptionPerTick * SimulationTime.TicksPerDay / NormalSleepTicks;
    /// <summary>普通人类每 tick 体力劳动的体力消耗。</summary>
    public const double PhysicalWorkCostPerTick = BaseStaminaCapacity * (1 - FullEfficiencyThreshold) / NormalWorkTicks;
    /// <summary>脑力劳动相对于体力劳动的体力消耗比例。</summary>
    public const double MentalWorkCostRatio = .2;
    /// <summary>普通休息每 tick 恢复的体力。</summary>
    public const double StaminaRecoveryPerTick = BaseStaminaCapacity * (1 - FullEfficiencyThreshold) / NormalSleepTicks;
    /// <summary>交谈休息相对于普通休息的恢复比例。</summary>
    public const double SocialRestRecoveryRatio = .5;
    /// <summary>未经救助卧床的昏迷恢复质量。</summary>
    public const double UnconsciousRecoveryQuality = .5;
    /// <summary>未进入住宅时的睡眠和休息质量。</summary>
    public const double OutdoorRestQuality = .75;
    /// <summary>步行一步消耗的体力。</summary>
    public const double WalkingCost = .15;
    /// <summary>睡眠耗尽时体力劳动效率的降低比例。</summary>
    public const double SleepPhysicalReduction = .5;
    /// <summary>睡眠耗尽时脑力劳动效率的降低比例。</summary>
    public const double SleepMentalReduction = 1;
    /// <summary>体力耗尽时体力劳动效率的降低比例。</summary>
    public const double StaminaPhysicalReduction = SleepMentalReduction;
    /// <summary>体力耗尽时脑力劳动效率的降低比例。</summary>
    public const double StaminaMentalReduction = SleepPhysicalReduction;
    /// <summary>允许自主出门的最低年龄。</summary>
    public const double MinimumOutdoorAge = 6;
    /// <summary>允许劳动的最低年龄。</summary>
    public const double MinimumWorkAge = 12;
    /// <summary>达到完整劳动效率的成年年龄。</summary>
    public const double AdultAge = 18;
    /// <summary>刚达到劳动年龄时的效率比例。</summary>
    public const double MinimumAgeEfficiency = .2;
    /// <summary>种族需求参数的基础差异幅度。</summary>
    public const double RaceVariation = .05;
    /// <summary>个人需求参数的最大差异幅度。</summary>
    public const double IndividualVariation = .025;
    /// <summary>个人差异的取样档数。</summary>
    private const int IndividualSteps = 20;

    /// <summary>计算居民的睡眠上限。</summary>
    /// <param name="person">居民。</param>
    public static double SleepCapacity(Resident person) => BaseSleepCapacity * SleepRaceFactor(person.Race) * IndividualFactor(person.Id, 0);

    /// <summary>计算居民的体力上限。</summary>
    /// <param name="person">居民。</param>
    public static double StaminaCapacity(Resident person) => BaseStaminaCapacity * StaminaRaceFactor(person.Race) * IndividualFactor(person.Id, 1);

    /// <summary>计算当前睡眠值。</summary>
    /// <param name="person">居民。</param>
    public static double SleepValue(Resident person) => SleepCapacity(person) * person.Agent.Sleep / MaximumPercent;

    /// <summary>计算当前体力值。</summary>
    /// <param name="person">居民。</param>
    public static double StaminaValue(Resident person) => StaminaCapacity(person) * (1 - person.Agent.Fatigue / MaximumPercent);

    /// <summary>计算年龄对应的劳动效率。</summary>
    /// <param name="age">模拟年龄。</param>
    public static double AgeEfficiency(double age) => age < MinimumWorkAge ? 0
        : MinimumAgeEfficiency + (1 - MinimumAgeEfficiency) * Math.Clamp((age - MinimumWorkAge) / (AdultAge - MinimumWorkAge), 0, 1);

    /// <summary>计算睡眠、体力及年龄共同影响的劳动效率。</summary>
    /// <param name="person">居民。</param>
    /// <param name="mental">是否为脑力劳动。</param>
    public static double WorkEfficiency(Resident person, bool mental = false) => AgeEfficiency(person.Age)
        * NeedEfficiency(person.Agent.Sleep / MaximumPercent, mental ? SleepMentalReduction : SleepPhysicalReduction)
        * NeedEfficiency(1 - person.Agent.Fatigue / MaximumPercent, mental ? StaminaMentalReduction : StaminaPhysicalReduction);

    /// <summary>判断居民是否因睡眠或体力耗尽而不能行动。</summary>
    /// <param name="person">居民。</param>
    public static bool IsUnconscious(Resident person) => IsUnconscious(person.Agent, person.Activity);

    internal static bool IsUnconscious(AgentState agent, ResidentActivity activity) => agent.Sleep <= 0 || agent.Fatigue >= MaximumPercent
        || (activity == ResidentActivity.Unconscious
            && (agent.Sleep < ConsciousRecoveryThreshold * MaximumPercent
                || agent.Fatigue > (1 - ConsciousRecoveryThreshold) * MaximumPercent));

    /// <summary>判断居民是否达到劳动年龄且能够行动。</summary>
    /// <param name="person">居民。</param>
    public static bool CanWork(Resident person) => person.Age >= MinimumWorkAge && !IsUnconscious(person);

    /// <summary>结算一 tick 持续睡眠消耗及当前休息恢复。</summary>
    /// <param name="person">本 tick 行动结束后的居民。</param>
    /// <param name="quality">睡眠或休息质量，普通质量为一。</param>
    internal static AgentState Advance(Resident person, double quality)
    {
        if (person.Activity == ResidentActivity.Unconscious && !person.BedRestAfterRescue)
            quality *= UnconsciousRecoveryQuality;
        var consumption = SleepConsumptionPerTick * MetabolismFactor(person);
        var recovery = person.Activity is ResidentActivity.Sleeping or ResidentActivity.Unconscious
            ? Math.Max(consumption, SleepRecoveryPerTick * RecoveryFactor(person) * quality) : 0;
        var sleep = Math.Clamp(person.Agent.Sleep + (recovery - consumption) / SleepCapacity(person) * MaximumPercent, 0, MaximumPercent);
        var rest = person.Activity is ResidentActivity.Resting or ResidentActivity.Sleeping or ResidentActivity.Unconscious
            ? StaminaRecoveryPerTick
            : person.Activity is ResidentActivity.Talking or ResidentActivity.Eating ? StaminaRecoveryPerTick * SocialRestRecoveryRatio : 0;
        var fatigue = Math.Max(0, person.Agent.Fatigue - rest * RecoveryFactor(person) * quality / StaminaCapacity(person) * MaximumPercent);
        return sleep == person.Agent.Sleep && fatigue == person.Agent.Fatigue
            ? person.Agent : person.Agent with { Sleep = sleep, Fatigue = fatigue };
    }

    /// <summary>扣除实际劳动或移动所需体力。</summary>
    /// <param name="person">居民。</param>
    /// <param name="cost">按普通人类计的体力消耗。</param>
    internal static double ExertionFatigue(Resident person, double cost) => Math.Min(MaximumPercent,
        person.Agent.Fatigue + cost * MetabolismFactor(person) / StaminaCapacity(person) * MaximumPercent);

    private static double NeedEfficiency(double fraction, double reduction) => 1 - reduction * (1 - Math.Clamp(fraction / FullEfficiencyThreshold, 0, 1));
    private static double SleepRaceFactor(RaceKind race) => race switch
    {
        RaceKind.Elf => 1 + RaceVariation,
        RaceKind.Orc => 1 - RaceVariation,
        _ => 1,
    };
    private static double StaminaRaceFactor(RaceKind race) => race switch
    {
        RaceKind.Elf => 1 - RaceVariation,
        RaceKind.Dwarf or RaceKind.Orc => 1 + RaceVariation,
        _ => 1,
    };
    private static double MetabolismFactor(Resident person) => (person.Race == RaceKind.Orc ? 1 + RaceVariation : 1) * IndividualFactor(person.Id, 2);
    private static double RecoveryFactor(Resident person) => (person.Race == RaceKind.Dwarf ? 1 + RaceVariation : 1) * IndividualFactor(person.Id, 3);
    private static double IndividualFactor(int id, int salt) => id == 0 ? 1
        : 1 + IndividualVariation * ((unchecked((uint)id * 2654435761u + (uint)salt * 374761393u) % (IndividualSteps + 1)) * 2d / IndividualSteps - 1);
}
