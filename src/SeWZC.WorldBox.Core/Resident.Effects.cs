namespace SeWZC.WorldBox.Core;

public sealed partial record Resident
{
    // 免疫、战斗和死亡信息只在发生相应事件时复制，日常需求及移动共享这些不可变值。
    private readonly Effects _effects = Effects.Default;

    private sealed record Effects
    {
        internal static readonly Effects Default = new();

        public int ArmyId { get; init; }
        public long DiseaseImmuneUntilTick { get; init; }
        public DeathCause DeathCause { get; init; }
        public long DeathTick { get; init; }
        public double Armor { get; init; }
        public double PersonalWard { get; init; }
        public long FrozenUntilTick { get; init; }
        public long LastRangedAttackTick { get; init; } = -100;
    }
}
