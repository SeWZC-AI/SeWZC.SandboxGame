using System.Collections.Immutable;

namespace SeWZC.WorldBox.Core;

public sealed partial record Resident
{
    // 日常身体结算和移动共享身份、职业与经历；只有相应事件发生时才复制这一组状态。
    private readonly Identity _identity = Identity.Default;

    private sealed record Identity
    {
        internal static readonly Identity Default = new();

        public int Id { get; init; }
        public string Name { get; init; } = "";
        public RaceKind Race { get; init; }
        public int NationId { get; init; }
        public int SettlementId { get; init; }
        public Profession Profession { get; init; }
        public string Trait { get; init; } = "勤劳";
        public int CultureId { get; init; }
        public double MagicTalent { get; init; } = 25;
        public double MagicTraining { get; init; }
        public ImmutableList<ResidentHistoryEntry> History { get; init; } = [];
    }
}
