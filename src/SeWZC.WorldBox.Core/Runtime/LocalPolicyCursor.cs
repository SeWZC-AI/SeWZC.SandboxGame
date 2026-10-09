namespace SeWZC.WorldBox.Core.Runtime;

/// <summary>LocalPolicy 的引擎内定位引用；每次写入提交新的不可变状态。</summary>
internal sealed class LocalPolicyCursor : StateCursor<LocalPolicy>
{
    public LocalPolicyCursor(LocalPolicy value) : base(value) { }

    public int SettlementId
    {
        get => Value.SettlementId;
    }

    public PolicyKind Kind
    {
        get => Value.Kind;
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

    public string Reason
    {
        get => Value.Reason;
        set
        {
            if (!EqualityComparer<string>.Default.Equals(Value.Reason, value))
                ReplaceChanged(Value with { Reason = value });
        }
    }

    public long EvidenceObservedTick
    {
        get => Value.EvidenceObservedTick;
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
