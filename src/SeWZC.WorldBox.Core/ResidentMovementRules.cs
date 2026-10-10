namespace SeWZC.WorldBox.Core;

/// <summary>居民移动速度与负重的可配置规则。</summary>
public static class ResidentMovementRules
{
    /// <summary>普通居民在平地每刻行走的格数。</summary>
    public const double BaseTilesPerTick = 2;
    /// <summary>单刻移动区段最多经过的地格数。</summary>
    public const int MaximumTilesPerTick = 8;
    /// <summary>单格移动的最长耗时。</summary>
    public const int MaximumTicksPerTile = 8;
    /// <summary>体力耗尽时移动速度的最大降低比例。</summary>
    public const double MaximumStaminaReduction = .5;
    /// <summary>不降低速度的随身物资重量。</summary>
    public const double FreeCarryWeight = 5;
    /// <summary>达到最大负重减速所需的额外重量。</summary>
    public const double FullLoadWeight = 60;
    /// <summary>负重造成的最大速度降低比例。</summary>
    public const double MaximumLoadReduction = .5;
    /// <summary>背负一名居民计入的重量。</summary>
    public const double ResidentCarryWeight = 60;
    /// <summary>患病时的移动速度倍率。</summary>
    public const double SicknessMultiplier = .65;
    /// <summary>重伤时的移动速度倍率。</summary>
    public const double InjuryMultiplier = .75;

    /// <summary>计算体力对移动速度的倍率。</summary>
    /// <param name="person">居民。</param>
    public static double StaminaMultiplier(Resident person) => 1 - MaximumStaminaReduction *
        (1 - Math.Clamp((1 - person.Agent.Fatigue / ResidentNeedsRules.MaximumPercent) /
                        ResidentNeedsRules.FullEfficiencyThreshold, 0, 1));

    /// <summary>计算随身物资和被背负居民造成的负重倍率。</summary>
    /// <param name="person">居民。</param>
    /// <param name="carryingResident">是否正在背负另一名居民。</param>
    public static double LoadMultiplier(Resident person, bool carryingResident = false)
    {
        var weight = carryingResident ? ResidentCarryWeight : 0;
        foreach (var kind in ResourceStock.Kinds)
            weight += person.Inventory.Get(kind);
        return 1 - MaximumLoadReduction * Math.Clamp((weight - FreeCarryWeight) / FullLoadWeight, 0, 1);
    }

    /// <summary>计算身体状况和负重共同造成的速度倍率。</summary>
    /// <param name="person">居民。</param>
    /// <param name="carryingResident">是否正在背负另一名居民。</param>
    public static double ConditionMultiplier(Resident person, bool carryingResident = false) =>
        StaminaMultiplier(person) * LoadMultiplier(person, carryingResident)
        * (person.SicknessTicks > 0 ? SicknessMultiplier : 1)
        * (person.Health < 40 ? InjuryMultiplier : 1);
}
