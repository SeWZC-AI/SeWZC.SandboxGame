namespace SeWZC.WorldBox.Core.Runtime;

/// <summary>Army 的引擎内定位引用；每次写入提交新的不可变状态。</summary>
internal sealed class ArmyCursor(Army value) : StateCursor<Army>(value)
{
    public WarObjective Objective => Value.Objective;

    public int CampaignEventId => Value.CampaignEventId;

    public int LastEventId
    {
        get => Value.LastEventId;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(Value.LastEventId, value))
                ReplaceChanged(Value with { LastEventId = value });
        }
    }

    public int InitialSoldiers => Value.InitialSoldiers;

    public long StartedTick => Value.StartedTick;

    public int BlockedTicks => Value.BlockedTicks;

    public WarOutcome Outcome => Value.Outcome;

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

    public DiplomaticStatus KnownDiplomacy => Value.KnownDiplomacy;

    public long LastOrderTick => Value.LastOrderTick;

    public int LastOrderFactId => Value.LastOrderFactId;

    public bool Gathering
    {
        get => Value.Gathering;
        set
        {
            if (!EqualityComparer<bool>.Default.Equals(Value.Gathering, value))
                ReplaceChanged(Value with { Gathering = value });
        }
    }

    public bool Retreating => Value.Retreating;

    public int TargetX => Value.TargetX;

    public int TargetY => Value.TargetY;

    public int TargetSettlementId => Value.TargetSettlementId;

    public int Id => Value.Id;

    public int NationId => Value.NationId;

    public int TargetNationId => Value.TargetNationId;

    public int X => Value.X;

    public int Y => Value.Y;

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
