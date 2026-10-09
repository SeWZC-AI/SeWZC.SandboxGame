namespace SeWZC.WorldBox.Core.Runtime;

/// <summary>DiplomaticRelation 的引擎内定位引用；每次写入提交新的不可变状态。</summary>
internal sealed class DiplomaticRelationCursor : StateCursor<DiplomaticRelation>
{
    public DiplomaticRelationCursor(DiplomaticRelation value) : base(value) { }

    public long LastChangedTick
    {
        get => Value.LastChangedTick;
    }

    public long LastContactTick
    {
        get => Value.LastContactTick;
        set
        {
            if (!EqualityComparer<long>.Default.Equals(Value.LastContactTick, value))
                ReplaceChanged(Value with { LastContactTick = value });
        }
    }

    public long FirstEscalationTick
    {
        get => Value.FirstEscalationTick;
        set
        {
            if (!EqualityComparer<long>.Default.Equals(Value.FirstEscalationTick, value))
                ReplaceChanged(Value with { FirstEscalationTick = value });
        }
    }

    public long SecondEscalationTick
    {
        get => Value.SecondEscalationTick;
        set
        {
            if (!EqualityComparer<long>.Default.Equals(Value.SecondEscalationTick, value))
                ReplaceChanged(Value with { SecondEscalationTick = value });
        }
    }

    public long LastEvaluatedTick
    {
        get => Value.LastEvaluatedTick;
        set
        {
            if (!EqualityComparer<long>.Default.Equals(Value.LastEvaluatedTick, value))
                ReplaceChanged(Value with { LastEvaluatedTick = value });
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

    public int AllianceOfferNationId
    {
        get => Value.AllianceOfferNationId;
    }

    public long AllianceOfferTick
    {
        get => Value.AllianceOfferTick;
    }

    public string Reason
    {
        get => Value.Reason;
        set
        {
            if (!EqualityComparer<string>.Default.Equals(Value.Reason, value))
                ReplaceChanged(Value with { Reason = value });
        }
    }

    public int FirstNationId
    {
        get => Value.FirstNationId;
    }

    public int SecondNationId
    {
        get => Value.SecondNationId;
    }

    public DiplomaticStatus Status
    {
        get => Value.Status;
        set
        {
            if (!EqualityComparer<DiplomaticStatus>.Default.Equals(Value.Status, value))
                ReplaceChanged(Value with { Status = value });
        }
    }

    public int FirstOpinion
    {
        get => Value.FirstOpinion;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(Value.FirstOpinion, value))
                ReplaceChanged(Value with { FirstOpinion = value });
        }
    }

    public int SecondOpinion
    {
        get => Value.SecondOpinion;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(Value.SecondOpinion, value))
                ReplaceChanged(Value with { SecondOpinion = value });
        }
    }

    public int Opinion
    {
        get => Value.Opinion;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(Value.Opinion, value))
                ReplaceChanged(Value with { Opinion = value });
        }
    }

    public static implicit operator DiplomaticRelation(DiplomaticRelationCursor cursor)
    {
        return cursor.Value;
    }

    public static implicit operator DiplomaticRelationCursor(DiplomaticRelation value)
    {
        return new DiplomaticRelationCursor(value);
    }
}
