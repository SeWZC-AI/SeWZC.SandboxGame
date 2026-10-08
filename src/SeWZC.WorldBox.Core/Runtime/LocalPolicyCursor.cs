namespace SeWZC.WorldBox.Core.Runtime;

/// <summary>LocalPolicy 的引擎内定位引用；每次写入提交新的不可变状态。</summary>
internal sealed class LocalPolicyCursor : StateCursor<LocalPolicy>
{
    public LocalPolicyCursor() : this(new LocalPolicy()) { }
    public LocalPolicyCursor(LocalPolicy value) : base(value) { }

    public int SettlementId
    {
        get => Value.SettlementId;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(Value.SettlementId, value))
                ReplaceChanged(Value with { SettlementId = value });
        }
    }

    public PolicyKind Kind
    {
        get => Value.Kind;
        set
        {
            if (!EqualityComparer<PolicyKind>.Default.Equals(Value.Kind, value))
                ReplaceChanged(Value with { Kind = value });
        }
    }

    public bool PlayerOverride
    {
        get => Value.PlayerOverride;
        set
        {
            if (!EqualityComparer<bool>.Default.Equals(Value.PlayerOverride, value))
                ReplaceChanged(Value with { PlayerOverride = value });
        }
    }

    public long DecidedTick
    {
        get => Value.DecidedTick;
        set
        {
            if (!EqualityComparer<long>.Default.Equals(Value.DecidedTick, value))
                ReplaceChanged(Value with { DecidedTick = value });
        }
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

    public int EvidenceFactId
    {
        get => Value.EvidenceFactId;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(Value.EvidenceFactId, value))
                ReplaceChanged(Value with { EvidenceFactId = value });
        }
    }

    public long EvidenceObservedTick
    {
        get => Value.EvidenceObservedTick;
        set
        {
            if (!EqualityComparer<long>.Default.Equals(Value.EvidenceObservedTick, value))
                ReplaceChanged(Value with { EvidenceObservedTick = value });
        }
    }

    public static implicit operator LocalPolicy(LocalPolicyCursor cursor)
    {
        return cursor.Value;
    }

    public static implicit operator LocalPolicyCursor(LocalPolicy value)
    {
        return new LocalPolicyCursor(value);
    }
}
