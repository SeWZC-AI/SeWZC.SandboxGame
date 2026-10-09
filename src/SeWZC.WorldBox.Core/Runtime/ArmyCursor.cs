namespace SeWZC.WorldBox.Core.Runtime;

/// <summary>Army 的引擎内定位引用；每次写入提交新的不可变状态。</summary>
internal sealed class ArmyCursor(Army value) : StateCursor<Army>(value)
{

    public WarObjective Objective
    {
        get => Value.Objective;
    }

    public int CampaignEventId
    {
        get => Value.CampaignEventId;
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
    }

    public long StartedTick
    {
        get => Value.StartedTick;
    }

    public int BlockedTicks
    {
        get => Value.BlockedTicks;
    }

    public WarOutcome Outcome
    {
        get => Value.Outcome;
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
    }

    public long LastOrderTick
    {
        get => Value.LastOrderTick;
    }

    public int LastOrderFactId
    {
        get => Value.LastOrderFactId;
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
    }

    public int TargetX
    {
        get => Value.TargetX;
    }

    public int TargetY
    {
        get => Value.TargetY;
    }

    public int TargetSettlementId
    {
        get => Value.TargetSettlementId;
    }

    public int Id
    {
        get => Value.Id;
    }

    public int NationId
    {
        get => Value.NationId;
    }

    public int TargetNationId
    {
        get => Value.TargetNationId;
    }

    public int X
    {
        get => Value.X;
    }

    public int Y
    {
        get => Value.Y;
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
