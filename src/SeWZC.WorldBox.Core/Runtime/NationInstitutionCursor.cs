namespace SeWZC.WorldBox.Core.Runtime;

/// <summary>NationInstitution 的引擎内定位引用；每次写入提交新的不可变状态。</summary>
internal sealed class NationInstitutionCursor : StateCursor<NationInstitution>
{
    public NationInstitutionCursor(NationInstitution value) : base(value) { }

    public int NationId
    {
        get => Value.NationId;
    }

    public InstitutionKind Kind
    {
        get => Value.Kind;
        set
        {
            if (!EqualityComparer<InstitutionKind>.Default.Equals(Value.Kind, value))
                ReplaceChanged(Value with { Kind = value });
        }
    }

    public PolicyKind? PlayerPolicy
    {
        get => Value.PlayerPolicy;
        set
        {
            if (!EqualityComparer<PolicyKind?>.Default.Equals(Value.PlayerPolicy, value))
                ReplaceChanged(Value with { PlayerPolicy = value });
        }
    }

    public static implicit operator NationInstitution(NationInstitutionCursor cursor)
    {
        return cursor.Value;
    }

    public static implicit operator NationInstitutionCursor(NationInstitution value)
    {
        return new NationInstitutionCursor(value);
    }
}
