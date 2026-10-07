using System.Collections.Immutable;

namespace SeWZC.WorldBox.Core.Runtime;

/// <summary>SocietyState 的引擎内定位引用；每次写入提交新的不可变状态。</summary>
internal sealed partial class SocietyStateCursor : StateCursor<global::SeWZC.WorldBox.Core.SocietyState>
{
    public SocietyStateCursor() : this(new()) { }
    public SocietyStateCursor(global::SeWZC.WorldBox.Core.SocietyState value) : base(value) { }
    public static implicit operator global::SeWZC.WorldBox.Core.SocietyState(SocietyStateCursor cursor) => cursor.Value;
    public static implicit operator SocietyStateCursor(global::SeWZC.WorldBox.Core.SocietyState value) => new(value);
    public bool MagicEnabled { get => Value.MagicEnabled; set { if (!EqualityComparer<bool>.Default.Equals(Value.MagicEnabled, value)) Replace(Value with { MagicEnabled = value }); } }
    private SnapshotListCursor<CultureDefinition>? _Cultures;
    public SnapshotListCursor<CultureDefinition> Cultures
    {
        get => _Cultures ??= new(Value.Cultures, value => Replace(Value with { Cultures = value }));
        set { _Cultures = null; Replace(Value with { Cultures = value.Snapshot }); }
    }
    private EntityListCursor<global::SeWZC.WorldBox.Core.Building, BuildingCursor>? _Buildings;
    public EntityListCursor<global::SeWZC.WorldBox.Core.Building, BuildingCursor> Buildings
    {
        get => _Buildings ??= new(Value.Buildings, value => Replace(Value with { Buildings = value }), value => new BuildingCursor(value));
        set { _Buildings = null; Replace(Value with { Buildings = value.Snapshot }); }
    }
    private EntityListCursor<global::SeWZC.WorldBox.Core.SettlementResearch, SettlementResearchCursor>? _Research;
    public EntityListCursor<global::SeWZC.WorldBox.Core.SettlementResearch, SettlementResearchCursor> Research
    {
        get => _Research ??= new(Value.Research, value => Replace(Value with { Research = value }), value => new SettlementResearchCursor(value));
        set { _Research = null; Replace(Value with { Research = value.Snapshot }); }
    }
    private EntityListCursor<global::SeWZC.WorldBox.Core.LocalPolicy, LocalPolicyCursor>? _Policies;
    public EntityListCursor<global::SeWZC.WorldBox.Core.LocalPolicy, LocalPolicyCursor> Policies
    {
        get => _Policies ??= new(Value.Policies, value => Replace(Value with { Policies = value }), value => new LocalPolicyCursor(value));
        set { _Policies = null; Replace(Value with { Policies = value.Snapshot }); }
    }
    private EntityListCursor<global::SeWZC.WorldBox.Core.NationInstitution, NationInstitutionCursor>? _Institutions;
    public EntityListCursor<global::SeWZC.WorldBox.Core.NationInstitution, NationInstitutionCursor> Institutions
    {
        get => _Institutions ??= new(Value.Institutions, value => Replace(Value with { Institutions = value }), value => new NationInstitutionCursor(value));
        set { _Institutions = null; Replace(Value with { Institutions = value.Snapshot }); }
    }
    private SnapshotListCursor<InstitutionReport>? _Reports;
    public SnapshotListCursor<InstitutionReport> Reports
    {
        get => _Reports ??= new(Value.Reports, value => Replace(Value with { Reports = value }));
        set { _Reports = null; Replace(Value with { Reports = value.Snapshot }); }
    }
    private EntityListCursor<global::SeWZC.WorldBox.Core.CulturalContact, CulturalContactCursor>? _CulturalContacts;
    public EntityListCursor<global::SeWZC.WorldBox.Core.CulturalContact, CulturalContactCursor> CulturalContacts
    {
        get => _CulturalContacts ??= new(Value.CulturalContacts, value => Replace(Value with { CulturalContacts = value }), value => new CulturalContactCursor(value));
        set { _CulturalContacts = null; Replace(Value with { CulturalContacts = value.Snapshot }); }
    }
}
