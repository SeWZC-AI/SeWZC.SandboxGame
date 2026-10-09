namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    private readonly ParallelOptions _bodyParallelism =
        new() { MaxDegreeOfParallelism = Math.Min(2, Environment.ProcessorCount) };

    private DailyResidentInput[] _dailyResidentInputs = [];
    private Resident.DailyState[] _dailyResidentOutputs = [];

    // 公共库存及随机性在主线程结算，个人身体转换只有下列已确定的输入。
    private readonly struct DailyResidentInput
    {
        internal Resident Person { get; init; }
        internal AgentState Agent { get; init; }
        internal ResourceStock Inventory { get; init; }
        internal Tile Tile { get; init; }
        internal Profession Profession { get; init; }
        internal int InfectionDuration { get; init; }
        internal double ManaRecovery { get; init; }
        internal bool ConsumeNeeds { get; init; }
        internal double SocialGrowth { get; init; }
        internal double DeliveredWater { get; init; }
        internal int ArrivedTile { get; init; }

        internal Resident.DailyState Advance(WorldRules rules, long tick)
        {
            return Person.CalculateDay(rules, Tile, tick,
                Profession, InfectionDuration, ManaRecovery, ConsumeNeeds, SocialGrowth, DeliveredWater, ArrivedTile,
                Inventory, Agent);
        }
    }
}
