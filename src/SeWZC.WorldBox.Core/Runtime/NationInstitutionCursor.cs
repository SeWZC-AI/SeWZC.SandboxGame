using System.Collections.Immutable;

namespace SeWZC.WorldBox.Core.Runtime;

/// <summary>NationInstitution 的引擎内定位引用；每次写入提交新的不可变状态。</summary>
internal sealed partial class NationInstitutionCursor : StateCursor<global::SeWZC.WorldBox.Core.NationInstitution>
{
    public NationInstitutionCursor() : this(new()) { }
    public NationInstitutionCursor(global::SeWZC.WorldBox.Core.NationInstitution value) : base(value) { }
    public static implicit operator global::SeWZC.WorldBox.Core.NationInstitution(NationInstitutionCursor cursor) => cursor.Value;
    public static implicit operator NationInstitutionCursor(global::SeWZC.WorldBox.Core.NationInstitution value) => new(value);
    public int NationId { get => Value.NationId; set { if (!EqualityComparer<int>.Default.Equals(Value.NationId, value)) ReplaceChanged(Value with { NationId = value }); } }
    public InstitutionKind Kind { get => Value.Kind; set { if (!EqualityComparer<InstitutionKind>.Default.Equals(Value.Kind, value)) ReplaceChanged(Value with { Kind = value }); } }
    public PolicyKind? PlayerPolicy { get => Value.PlayerPolicy; set { if (!EqualityComparer<PolicyKind?>.Default.Equals(Value.PlayerPolicy, value)) ReplaceChanged(Value with { PlayerPolicy = value }); } }
    public string LastDecision { get => Value.LastDecision; set { if (!EqualityComparer<string>.Default.Equals(Value.LastDecision, value)) ReplaceChanged(Value with { LastDecision = value }); } }
    public long LastDecisionTick { get => Value.LastDecisionTick; set { if (!EqualityComparer<long>.Default.Equals(Value.LastDecisionTick, value)) ReplaceChanged(Value with { LastDecisionTick = value }); } }
}
