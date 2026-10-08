namespace SeWZC.WorldBox.Core.Runtime;

/// <summary>Army 的引擎内定位引用；每次写入提交新的不可变状态。</summary>
internal sealed class ArmyCursor(Army value) : StateCursor<Army>(value)
{
    public ArmyCursor() : this(new Army()) { }

    public WarObjective Objective
    {
        get => Value.Objective;
        set
        {
            if (!EqualityComparer<WarObjective>.Default.Equals(Value.Objective, value))
                ReplaceChanged(Value with { Objective = value });
        }
    }

    public int CampaignEventId
    {
        get => Value.CampaignEventId;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(Value.CampaignEventId, value))
                ReplaceChanged(Value with { CampaignEventId = value });
        }
    }

    public int LastEventId
    {
        get => Value.LastEventId;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(Value.LastEventId, value))
                ReplaceChanged(Value with { LastEventId = value });
        }
    }

    public int InitialSoldiers
    {
        get => Value.InitialSoldiers;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(Value.InitialSoldiers, value))
                ReplaceChanged(Value with { InitialSoldiers = value });
        }
    }

    public long StartedTick
    {
        get => Value.StartedTick;
        set
        {
            if (!EqualityComparer<long>.Default.Equals(Value.StartedTick, value))
                ReplaceChanged(Value with { StartedTick = value });
        }
    }

    public int BlockedTicks
    {
        get => Value.BlockedTicks;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(Value.BlockedTicks, value))
                ReplaceChanged(Value with { BlockedTicks = value });
        }
    }

    public WarOutcome Outcome
    {
        get => Value.Outcome;
        set
        {
            if (!EqualityComparer<WarOutcome>.Default.Equals(Value.Outcome, value))
                ReplaceChanged(Value with { Outcome = value });
        }
    }

    public bool BattleRecorded
    {
        get => Value.BattleRecorded;
        set
        {
            if (!EqualityComparer<bool>.Default.Equals(Value.BattleRecorded, value))
                ReplaceChanged(Value with { BattleRecorded = value });
        }
    }

    public int CommanderId
    {
        get => Value.CommanderId;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(Value.CommanderId, value))
                ReplaceChanged(Value with { CommanderId = value });
        }
    }

    public DiplomaticStatus KnownDiplomacy
    {
        get => Value.KnownDiplomacy;
        set
        {
            if (!EqualityComparer<DiplomaticStatus>.Default.Equals(Value.KnownDiplomacy, value))
                ReplaceChanged(Value with { KnownDiplomacy = value });
        }
    }

    public long LastOrderTick
    {
        get => Value.LastOrderTick;
        set
        {
            if (!EqualityComparer<long>.Default.Equals(Value.LastOrderTick, value))
                ReplaceChanged(Value with { LastOrderTick = value });
        }
    }

    public int LastOrderFactId
    {
        get => Value.LastOrderFactId;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(Value.LastOrderFactId, value))
                ReplaceChanged(Value with { LastOrderFactId = value });
        }
    }

    public int FromX
    {
        get => Value.FromX;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(Value.FromX, value))
                ReplaceChanged(Value with { FromX = value });
        }
    }

    public int FromY
    {
        get => Value.FromY;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(Value.FromY, value))
                ReplaceChanged(Value with { FromY = value });
        }
    }

    public long MoveStartedTick
    {
        get => Value.MoveStartedTick;
        set
        {
            if (!EqualityComparer<long>.Default.Equals(Value.MoveStartedTick, value))
                ReplaceChanged(Value with { MoveStartedTick = value });
        }
    }

    public int MoveDurationTicks
    {
        get => Value.MoveDurationTicks;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(Value.MoveDurationTicks, value))
                ReplaceChanged(Value with { MoveDurationTicks = value });
        }
    }

    public bool Gathering
    {
        get => Value.Gathering;
        set
        {
            if (!EqualityComparer<bool>.Default.Equals(Value.Gathering, value))
                ReplaceChanged(Value with { Gathering = value });
        }
    }

    public bool Retreating
    {
        get => Value.Retreating;
        set
        {
            if (!EqualityComparer<bool>.Default.Equals(Value.Retreating, value))
                ReplaceChanged(Value with { Retreating = value });
        }
    }

    public int TargetX
    {
        get => Value.TargetX;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(Value.TargetX, value))
                ReplaceChanged(Value with { TargetX = value });
        }
    }

    public int TargetY
    {
        get => Value.TargetY;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(Value.TargetY, value))
                ReplaceChanged(Value with { TargetY = value });
        }
    }

    public int TargetSettlementId
    {
        get => Value.TargetSettlementId;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(Value.TargetSettlementId, value))
                ReplaceChanged(Value with { TargetSettlementId = value });
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

    public int NationId
    {
        get => Value.NationId;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(Value.NationId, value))
                ReplaceChanged(Value with { NationId = value });
        }
    }

    public int TargetNationId
    {
        get => Value.TargetNationId;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(Value.TargetNationId, value))
                ReplaceChanged(Value with { TargetNationId = value });
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

    public int Soldiers
    {
        get => Value.Soldiers;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(Value.Soldiers, value))
                ReplaceChanged(Value with { Soldiers = value });
        }
    }

    public double Morale
    {
        get => Value.Morale;
        set
        {
            if (!EqualityComparer<double>.Default.Equals(Value.Morale, value))
                ReplaceChanged(Value with { Morale = value });
        }
    }

    public double Supplies
    {
        get => Value.Supplies;
        set
        {
            if (!EqualityComparer<double>.Default.Equals(Value.Supplies, value))
                ReplaceChanged(Value with { Supplies = value });
        }
    }

    public string Status
    {
        get => Value.Status;
        set
        {
            if (!EqualityComparer<string>.Default.Equals(Value.Status, value))
                ReplaceChanged(Value with { Status = value });
        }
    }

    public double WaterSupplies
    {
        get => Value.WaterSupplies;
        set
        {
            if (!EqualityComparer<double>.Default.Equals(Value.WaterSupplies, value))
                ReplaceChanged(Value with { WaterSupplies = value });
        }
    }

    public static implicit operator Army(ArmyCursor cursor)
    {
        return cursor.Value;
    }

    public static implicit operator ArmyCursor(Army value)
    {
        return new ArmyCursor(value);
    }
}
