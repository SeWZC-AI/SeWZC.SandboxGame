using System.Collections.Immutable;

namespace SeWZC.WorldBox.Core.Runtime;

/// <summary>Building 的引擎内定位引用；每次写入提交新的不可变状态。</summary>
internal sealed partial class BuildingCursor : StateCursor<global::SeWZC.WorldBox.Core.Building>
{
    public BuildingCursor() : this(new()) { }
    public BuildingCursor(global::SeWZC.WorldBox.Core.Building value) : base(value) { }
    public static implicit operator global::SeWZC.WorldBox.Core.Building(BuildingCursor cursor) => cursor.Value;
    public static implicit operator BuildingCursor(global::SeWZC.WorldBox.Core.Building value) => new(value);
    public string PlanningReason { get => Value.PlanningReason; set { if (!EqualityComparer<string>.Default.Equals(Value.PlanningReason, value)) Replace(Value with { PlanningReason = value }); } }
    public string SiteReason { get => Value.SiteReason; set { if (!EqualityComparer<string>.Default.Equals(Value.SiteReason, value)) Replace(Value with { SiteReason = value }); } }
    public int Level { get => Value.Level; set { if (!EqualityComparer<int>.Default.Equals(Value.Level, value)) Replace(Value with { Level = value }); } }
    public double UpgradeProgress { get => Value.UpgradeProgress; set { if (!EqualityComparer<double>.Default.Equals(Value.UpgradeProgress, value)) Replace(Value with { UpgradeProgress = value }); } }
    public double UpgradeRequired { get => Value.UpgradeRequired; set { if (!EqualityComparer<double>.Default.Equals(Value.UpgradeRequired, value)) Replace(Value with { UpgradeRequired = value }); } }
    public BridgeDirection Direction { get => Value.Direction; set { if (!EqualityComparer<BridgeDirection>.Default.Equals(Value.Direction, value)) Replace(Value with { Direction = value }); } }
    public BridgeDirection? PendingDirection { get => Value.PendingDirection; set { if (!EqualityComparer<BridgeDirection?>.Default.Equals(Value.PendingDirection, value)) Replace(Value with { PendingDirection = value }); } }
    public bool IsUpgrading => Value.IsUpgrading;
    public double Efficiency => Value.Efficiency;
    public WildlifeKind LivestockKind { get => Value.LivestockKind; set { if (!EqualityComparer<WildlifeKind>.Default.Equals(Value.LivestockKind, value)) Replace(Value with { LivestockKind = value }); } }
    public double LivestockPopulation { get => Value.LivestockPopulation; set { if (!EqualityComparer<double>.Default.Equals(Value.LivestockPopulation, value)) Replace(Value with { LivestockPopulation = value }); } }
    public int ProductionBatches { get => Value.ProductionBatches; set { if (!EqualityComparer<int>.Default.Equals(Value.ProductionBatches, value)) Replace(Value with { ProductionBatches = value }); } }
    public int ServiceActions { get => Value.ServiceActions; set { if (!EqualityComparer<int>.Default.Equals(Value.ServiceActions, value)) Replace(Value with { ServiceActions = value }); } }
    public long LastServiceTick { get => Value.LastServiceTick; set { if (!EqualityComparer<long>.Default.Equals(Value.LastServiceTick, value)) Replace(Value with { LastServiceTick = value }); } }
    public bool Enabled { get => Value.Enabled; set { if (!EqualityComparer<bool>.Default.Equals(Value.Enabled, value)) Replace(Value with { Enabled = value }); } }
    public ProjectObservation Observation { get => Value.Observation; set { if (!EqualityComparer<ProjectObservation>.Default.Equals(Value.Observation, value)) Replace(Value with { Observation = value }); } }
    public int Id { get => Value.Id; set { if (!EqualityComparer<int>.Default.Equals(Value.Id, value)) Replace(Value with { Id = value }); } }
    public int SettlementId { get => Value.SettlementId; set { if (!EqualityComparer<int>.Default.Equals(Value.SettlementId, value)) Replace(Value with { SettlementId = value }); } }
    public BuildingKind Kind { get => Value.Kind; set { if (!EqualityComparer<BuildingKind>.Default.Equals(Value.Kind, value)) Replace(Value with { Kind = value }); } }
    public int X { get => Value.X; set { if (!EqualityComparer<int>.Default.Equals(Value.X, value)) Replace(Value with { X = value }); } }
    public int Y { get => Value.Y; set { if (!EqualityComparer<int>.Default.Equals(Value.Y, value)) Replace(Value with { Y = value }); } }
    public double ConstructionProgress { get => Value.ConstructionProgress; set { if (!EqualityComparer<double>.Default.Equals(Value.ConstructionProgress, value)) Replace(Value with { ConstructionProgress = value }); } }
    public double ConstructionRequired { get => Value.ConstructionRequired; set { if (!EqualityComparer<double>.Default.Equals(Value.ConstructionRequired, value)) Replace(Value with { ConstructionRequired = value }); } }
    public double Health { get => Value.Health; set { if (!EqualityComparer<double>.Default.Equals(Value.Health, value)) Replace(Value with { Health = value }); } }
    public int WorkSlots { get => Value.WorkSlots; set { if (!EqualityComparer<int>.Default.Equals(Value.WorkSlots, value)) Replace(Value with { WorkSlots = value }); } }
    private SnapshotListCursor<int>? _Workers;
    public SnapshotListCursor<int> Workers
    {
        get => _Workers ??= new(Value.Workers, value => Replace(Value with { Workers = value }));
        set { _Workers = null; Replace(Value with { Workers = value.Snapshot }); }
    }
    public long LastWorkedTick { get => Value.LastWorkedTick; set { if (!EqualityComparer<long>.Default.Equals(Value.LastWorkedTick, value)) Replace(Value with { LastWorkedTick = value }); } }
    public bool IsCompleted => Value.IsCompleted;
}
