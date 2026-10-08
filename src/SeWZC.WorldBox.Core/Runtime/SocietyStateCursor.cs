namespace SeWZC.WorldBox.Core.Runtime;

/// <summary>SocietyState 的引擎内定位引用；每次写入提交新的不可变状态。</summary>
internal sealed class SocietyStateCursor : StateCursor<SocietyState>
{
    public SocietyStateCursor() : this(new SocietyState()) { }
    public SocietyStateCursor(SocietyState value) : base(value) { }

    public bool MagicEnabled
    {
        get => Value.MagicEnabled;
        set
        {
            if (!EqualityComparer<bool>.Default.Equals(Value.MagicEnabled, value))
                ReplaceChanged(Value with { MagicEnabled = value });
        }
    }

    public SnapshotListCursor<CultureDefinition> Cultures
    {
        get => field ??= new SnapshotListCursor<CultureDefinition>(Value.Cultures, value =>
        {
            if (!ReferenceEquals(Value.Cultures, value))
                ReplaceChanged(Value with { Cultures = value });
        });
        set
        {
            field = null;
            Replace(Value with { Cultures = value.Snapshot });
        }
    }

    public EntityListCursor<Building, BuildingCursor> Buildings
    {
        get => field ??= new EntityListCursor<Building, BuildingCursor>(Value.Buildings, value =>
        {
            if (!ReferenceEquals(Value.Buildings, value))
                ReplaceChanged(Value with { Buildings = value });
        }, value => new BuildingCursor(value));
        set
        {
            field = null;
            Replace(Value with { Buildings = value.Snapshot });
        }
    }

    public EntityListCursor<SettlementResearch, SettlementResearchCursor> Research
    {
        get => field ??= new EntityListCursor<SettlementResearch, SettlementResearchCursor>(Value.Research, value =>
        {
            if (!ReferenceEquals(Value.Research, value))
                ReplaceChanged(Value with { Research = value });
        }, value => new SettlementResearchCursor(value));
        set
        {
            field = null;
            Replace(Value with { Research = value.Snapshot });
        }
    }

    public EntityListCursor<LocalPolicy, LocalPolicyCursor> Policies
    {
        get => field ??= new EntityListCursor<LocalPolicy, LocalPolicyCursor>(Value.Policies, value =>
        {
            if (!ReferenceEquals(Value.Policies, value))
                ReplaceChanged(Value with { Policies = value });
        }, value => new LocalPolicyCursor(value));
        set
        {
            field = null;
            Replace(Value with { Policies = value.Snapshot });
        }
    }

    public EntityListCursor<NationInstitution, NationInstitutionCursor> Institutions
    {
        get => field ??= new EntityListCursor<NationInstitution, NationInstitutionCursor>(Value.Institutions,
            value =>
            {
                if (!ReferenceEquals(Value.Institutions, value))
                    ReplaceChanged(Value with { Institutions = value });
            }, value => new NationInstitutionCursor(value));
        set
        {
            field = null;
            Replace(Value with { Institutions = value.Snapshot });
        }
    }

    public SnapshotListCursor<InstitutionReport> Reports
    {
        get => field ??= new SnapshotListCursor<InstitutionReport>(Value.Reports, value =>
        {
            if (!ReferenceEquals(Value.Reports, value))
                ReplaceChanged(Value with { Reports = value });
        });
        set
        {
            field = null;
            Replace(Value with { Reports = value.Snapshot });
        }
    }

    public EntityListCursor<CulturalContact, CulturalContactCursor> CulturalContacts
    {
        get => field ??= new EntityListCursor<CulturalContact, CulturalContactCursor>(
            Value.CulturalContacts, value =>
            {
                if (!ReferenceEquals(Value.CulturalContacts, value))
                    ReplaceChanged(Value with { CulturalContacts = value });
            }, value => new CulturalContactCursor(value));
        set
        {
            field = null;
            Replace(Value with { CulturalContacts = value.Snapshot });
        }
    }

    public static implicit operator SocietyState(SocietyStateCursor cursor)
    {
        return cursor.Value;
    }

    public static implicit operator SocietyStateCursor(SocietyState value)
    {
        return new SocietyStateCursor(value);
    }
}
