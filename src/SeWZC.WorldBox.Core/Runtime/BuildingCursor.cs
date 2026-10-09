using System.Collections.Immutable;

namespace SeWZC.WorldBox.Core.Runtime;

/// <summary>Building 的引擎内定位引用；每次写入提交新的不可变状态。</summary>
internal sealed class BuildingCursor(Building value) : StateCursor<Building>(value)
{
    public BuildingCursor() : this(new Building()) { }

    public int Level
    {
        get => Value.Level;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(Value.Level, value))
                ReplaceChanged(Value with { Level = value });
        }
    }

    public double UpgradeProgress
    {
        get => Value.UpgradeProgress;
        set
        {
            if (!EqualityComparer<double>.Default.Equals(Value.UpgradeProgress, value))
                ReplaceChanged(Value with { UpgradeProgress = value });
        }
    }

    public double UpgradeRequired
    {
        get => Value.UpgradeRequired;
        set
        {
            if (!EqualityComparer<double>.Default.Equals(Value.UpgradeRequired, value))
                ReplaceChanged(Value with { UpgradeRequired = value });
        }
    }

    public BridgeDirection Direction
    {
        get => Value.Direction;
        set
        {
            if (!EqualityComparer<BridgeDirection>.Default.Equals(Value.Direction, value))
                ReplaceChanged(Value with { Direction = value });
        }
    }

    public BridgeDirection? PendingDirection
    {
        get => Value.PendingDirection;
        set
        {
            if (!EqualityComparer<BridgeDirection?>.Default.Equals(Value.PendingDirection, value))
                ReplaceChanged(Value with { PendingDirection = value });
        }
    }

    public bool IsUpgrading => Value.IsUpgrading;
    public double Efficiency => Value.Efficiency;

    public WildlifeKind LivestockKind => Value.LivestockKind;

    public double LivestockPopulation
    {
        get => Value.LivestockPopulation;
        set
        {
            if (!EqualityComparer<double>.Default.Equals(Value.LivestockPopulation, value))
                ReplaceChanged(Value with { LivestockPopulation = value });
        }
    }

    public int ProductionBatches
    {
        get => Value.ProductionBatches;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(Value.ProductionBatches, value))
                ReplaceChanged(Value with { ProductionBatches = value });
        }
    }

    public int ServiceActions
    {
        get => Value.ServiceActions;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(Value.ServiceActions, value))
                ReplaceChanged(Value with { ServiceActions = value });
        }
    }

    public long LastServiceTick
    {
        get => Value.LastServiceTick;
        set
        {
            if (!EqualityComparer<long>.Default.Equals(Value.LastServiceTick, value))
                ReplaceChanged(Value with { LastServiceTick = value });
        }
    }

    public bool Enabled
    {
        get => Value.Enabled;
        set
        {
            if (!EqualityComparer<bool>.Default.Equals(Value.Enabled, value))
                ReplaceChanged(Value with { Enabled = value });
        }
    }

    public ProjectObservation Observation
    {
        get => Value.Observation;
        set
        {
            if (!EqualityComparer<ProjectObservation>.Default.Equals(Value.Observation, value))
                ReplaceChanged(Value with { Observation = value });
        }
    }

    public int Id
    {
        get => Value.Id;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(Value.Id, value))
                ReplaceChanged(Value with { Id = value });
        }
    }

    public int SettlementId
    {
        get => Value.SettlementId;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(Value.SettlementId, value))
                ReplaceChanged(Value with { SettlementId = value });
        }
    }

    public BuildingKind Kind
    {
        get => Value.Kind;
        set
        {
            if (!EqualityComparer<BuildingKind>.Default.Equals(Value.Kind, value))
                ReplaceChanged(Value with { Kind = value });
        }
    }

    public int X
    {
        get => Value.X;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(Value.X, value))
                ReplaceChanged(Value with { X = value });
        }
    }

    public int Y
    {
        get => Value.Y;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(Value.Y, value))
                ReplaceChanged(Value with { Y = value });
        }
    }

    public double ConstructionProgress
    {
        get => Value.ConstructionProgress;
        set
        {
            if (!EqualityComparer<double>.Default.Equals(Value.ConstructionProgress, value))
                ReplaceChanged(Value with { ConstructionProgress = value });
        }
    }

    public double ConstructionRequired => Value.ConstructionRequired;

    public double Health
    {
        get => Value.Health;
        set
        {
            if (!EqualityComparer<double>.Default.Equals(Value.Health, value))
                ReplaceChanged(Value with { Health = value });
        }
    }

    public int WorkSlots
    {
        get => Value.WorkSlots;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(Value.WorkSlots, value))
                ReplaceChanged(Value with { WorkSlots = value });
        }
    }

    public ImmutableList<int> Workers
    {
        get => Value.Workers;
        set
        {
            if (!ReferenceEquals(Value.Workers, value))
                ReplaceChanged(Value with { Workers = value });
        }
    }

    public long LastWorkedTick
    {
        get => Value.LastWorkedTick;
        set
        {
            if (!EqualityComparer<long>.Default.Equals(Value.LastWorkedTick, value))
                ReplaceChanged(Value with { LastWorkedTick = value });
        }
    }

    public bool IsCompleted => Value.IsCompleted;

    public static implicit operator Building(BuildingCursor cursor)
    {
        return cursor.Value;
    }

    public static implicit operator BuildingCursor(Building value)
    {
        return new BuildingCursor(value);
    }
}
