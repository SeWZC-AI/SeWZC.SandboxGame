using System.Collections.Immutable;

namespace SeWZC.WorldBox.Core.Runtime;

/// <summary>Army 的引擎内定位引用；每次写入提交新的不可变状态。</summary>
internal sealed partial class ArmyCursor : StateCursor<global::SeWZC.WorldBox.Core.Army>
{
    public ArmyCursor() : this(new()) { }
    public ArmyCursor(global::SeWZC.WorldBox.Core.Army value) : base(value) { }
    public static implicit operator global::SeWZC.WorldBox.Core.Army(ArmyCursor cursor) => cursor.Value;
    public static implicit operator ArmyCursor(global::SeWZC.WorldBox.Core.Army value) => new(value);
    public WarObjective Objective { get => Value.Objective; set { if (!EqualityComparer<WarObjective>.Default.Equals(Value.Objective, value)) Replace(Value with { Objective = value }); } }
    public int CampaignEventId { get => Value.CampaignEventId; set { if (!EqualityComparer<int>.Default.Equals(Value.CampaignEventId, value)) Replace(Value with { CampaignEventId = value }); } }
    public int LastEventId { get => Value.LastEventId; set { if (!EqualityComparer<int>.Default.Equals(Value.LastEventId, value)) Replace(Value with { LastEventId = value }); } }
    public int InitialSoldiers { get => Value.InitialSoldiers; set { if (!EqualityComparer<int>.Default.Equals(Value.InitialSoldiers, value)) Replace(Value with { InitialSoldiers = value }); } }
    public long StartedTick { get => Value.StartedTick; set { if (!EqualityComparer<long>.Default.Equals(Value.StartedTick, value)) Replace(Value with { StartedTick = value }); } }
    public int BlockedTicks { get => Value.BlockedTicks; set { if (!EqualityComparer<int>.Default.Equals(Value.BlockedTicks, value)) Replace(Value with { BlockedTicks = value }); } }
    public WarOutcome Outcome { get => Value.Outcome; set { if (!EqualityComparer<WarOutcome>.Default.Equals(Value.Outcome, value)) Replace(Value with { Outcome = value }); } }
    public bool BattleRecorded { get => Value.BattleRecorded; set { if (!EqualityComparer<bool>.Default.Equals(Value.BattleRecorded, value)) Replace(Value with { BattleRecorded = value }); } }
    public int CommanderId { get => Value.CommanderId; set { if (!EqualityComparer<int>.Default.Equals(Value.CommanderId, value)) Replace(Value with { CommanderId = value }); } }
    public DiplomaticStatus KnownDiplomacy { get => Value.KnownDiplomacy; set { if (!EqualityComparer<DiplomaticStatus>.Default.Equals(Value.KnownDiplomacy, value)) Replace(Value with { KnownDiplomacy = value }); } }
    public long LastOrderTick { get => Value.LastOrderTick; set { if (!EqualityComparer<long>.Default.Equals(Value.LastOrderTick, value)) Replace(Value with { LastOrderTick = value }); } }
    public int LastOrderFactId { get => Value.LastOrderFactId; set { if (!EqualityComparer<int>.Default.Equals(Value.LastOrderFactId, value)) Replace(Value with { LastOrderFactId = value }); } }
    public int FromX { get => Value.FromX; set { if (!EqualityComparer<int>.Default.Equals(Value.FromX, value)) Replace(Value with { FromX = value }); } }
    public int FromY { get => Value.FromY; set { if (!EqualityComparer<int>.Default.Equals(Value.FromY, value)) Replace(Value with { FromY = value }); } }
    public long MoveStartedTick { get => Value.MoveStartedTick; set { if (!EqualityComparer<long>.Default.Equals(Value.MoveStartedTick, value)) Replace(Value with { MoveStartedTick = value }); } }
    public int MoveDurationTicks { get => Value.MoveDurationTicks; set { if (!EqualityComparer<int>.Default.Equals(Value.MoveDurationTicks, value)) Replace(Value with { MoveDurationTicks = value }); } }
    public bool Gathering { get => Value.Gathering; set { if (!EqualityComparer<bool>.Default.Equals(Value.Gathering, value)) Replace(Value with { Gathering = value }); } }
    public bool Retreating { get => Value.Retreating; set { if (!EqualityComparer<bool>.Default.Equals(Value.Retreating, value)) Replace(Value with { Retreating = value }); } }
    public int TargetX { get => Value.TargetX; set { if (!EqualityComparer<int>.Default.Equals(Value.TargetX, value)) Replace(Value with { TargetX = value }); } }
    public int TargetY { get => Value.TargetY; set { if (!EqualityComparer<int>.Default.Equals(Value.TargetY, value)) Replace(Value with { TargetY = value }); } }
    public int TargetSettlementId { get => Value.TargetSettlementId; set { if (!EqualityComparer<int>.Default.Equals(Value.TargetSettlementId, value)) Replace(Value with { TargetSettlementId = value }); } }
    public int Id { get => Value.Id; set { if (!EqualityComparer<int>.Default.Equals(Value.Id, value)) Replace(Value with { Id = value }); } }
    public int NationId { get => Value.NationId; set { if (!EqualityComparer<int>.Default.Equals(Value.NationId, value)) Replace(Value with { NationId = value }); } }
    public int TargetNationId { get => Value.TargetNationId; set { if (!EqualityComparer<int>.Default.Equals(Value.TargetNationId, value)) Replace(Value with { TargetNationId = value }); } }
    public int X { get => Value.X; set { if (!EqualityComparer<int>.Default.Equals(Value.X, value)) Replace(Value with { X = value }); } }
    public int Y { get => Value.Y; set { if (!EqualityComparer<int>.Default.Equals(Value.Y, value)) Replace(Value with { Y = value }); } }
    public int Soldiers { get => Value.Soldiers; set { if (!EqualityComparer<int>.Default.Equals(Value.Soldiers, value)) Replace(Value with { Soldiers = value }); } }
    public double Morale { get => Value.Morale; set { if (!EqualityComparer<double>.Default.Equals(Value.Morale, value)) Replace(Value with { Morale = value }); } }
    public double Supplies { get => Value.Supplies; set { if (!EqualityComparer<double>.Default.Equals(Value.Supplies, value)) Replace(Value with { Supplies = value }); } }
    public string Status { get => Value.Status; set { if (!EqualityComparer<string>.Default.Equals(Value.Status, value)) Replace(Value with { Status = value }); } }
    public double WaterSupplies { get => Value.WaterSupplies; set { if (!EqualityComparer<double>.Default.Equals(Value.WaterSupplies, value)) Replace(Value with { WaterSupplies = value }); } }
}
