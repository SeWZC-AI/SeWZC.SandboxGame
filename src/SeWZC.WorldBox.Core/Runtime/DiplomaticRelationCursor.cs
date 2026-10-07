using System.Collections.Immutable;

namespace SeWZC.WorldBox.Core.Runtime;

/// <summary>DiplomaticRelation 的引擎内定位引用；每次写入提交新的不可变状态。</summary>
internal sealed partial class DiplomaticRelationCursor : StateCursor<global::SeWZC.WorldBox.Core.DiplomaticRelation>
{
    public DiplomaticRelationCursor() : this(new()) { }
    public DiplomaticRelationCursor(global::SeWZC.WorldBox.Core.DiplomaticRelation value) : base(value) { }
    public static implicit operator global::SeWZC.WorldBox.Core.DiplomaticRelation(DiplomaticRelationCursor cursor) => cursor.Value;
    public static implicit operator DiplomaticRelationCursor(global::SeWZC.WorldBox.Core.DiplomaticRelation value) => new(value);
    public long LastChangedTick { get => Value.LastChangedTick; set { if (!EqualityComparer<long>.Default.Equals(Value.LastChangedTick, value)) ReplaceChanged(Value with { LastChangedTick = value }); } }
    public long LastContactTick { get => Value.LastContactTick; set { if (!EqualityComparer<long>.Default.Equals(Value.LastContactTick, value)) ReplaceChanged(Value with { LastContactTick = value }); } }
    public long FirstEscalationTick { get => Value.FirstEscalationTick; set { if (!EqualityComparer<long>.Default.Equals(Value.FirstEscalationTick, value)) ReplaceChanged(Value with { FirstEscalationTick = value }); } }
    public long SecondEscalationTick { get => Value.SecondEscalationTick; set { if (!EqualityComparer<long>.Default.Equals(Value.SecondEscalationTick, value)) ReplaceChanged(Value with { SecondEscalationTick = value }); } }
    public long LastEvaluatedTick { get => Value.LastEvaluatedTick; set { if (!EqualityComparer<long>.Default.Equals(Value.LastEvaluatedTick, value)) ReplaceChanged(Value with { LastEvaluatedTick = value }); } }
    public int LastEventId { get => Value.LastEventId; set { if (!EqualityComparer<int>.Default.Equals(Value.LastEventId, value)) ReplaceChanged(Value with { LastEventId = value }); } }
    public int AllianceOfferNationId { get => Value.AllianceOfferNationId; set { if (!EqualityComparer<int>.Default.Equals(Value.AllianceOfferNationId, value)) ReplaceChanged(Value with { AllianceOfferNationId = value }); } }
    public long AllianceOfferTick { get => Value.AllianceOfferTick; set { if (!EqualityComparer<long>.Default.Equals(Value.AllianceOfferTick, value)) ReplaceChanged(Value with { AllianceOfferTick = value }); } }
    public string Reason { get => Value.Reason; set { if (!EqualityComparer<string>.Default.Equals(Value.Reason, value)) ReplaceChanged(Value with { Reason = value }); } }
    public int FirstNationId { get => Value.FirstNationId; set { if (!EqualityComparer<int>.Default.Equals(Value.FirstNationId, value)) ReplaceChanged(Value with { FirstNationId = value }); } }
    public int SecondNationId { get => Value.SecondNationId; set { if (!EqualityComparer<int>.Default.Equals(Value.SecondNationId, value)) ReplaceChanged(Value with { SecondNationId = value }); } }
    public DiplomaticStatus Status { get => Value.Status; set { if (!EqualityComparer<DiplomaticStatus>.Default.Equals(Value.Status, value)) ReplaceChanged(Value with { Status = value }); } }
    public int FirstOpinion { get => Value.FirstOpinion; set { if (!EqualityComparer<int>.Default.Equals(Value.FirstOpinion, value)) ReplaceChanged(Value with { FirstOpinion = value }); } }
    public int SecondOpinion { get => Value.SecondOpinion; set { if (!EqualityComparer<int>.Default.Equals(Value.SecondOpinion, value)) ReplaceChanged(Value with { SecondOpinion = value }); } }
    public int Opinion { get => Value.Opinion; set { if (!EqualityComparer<int>.Default.Equals(Value.Opinion, value)) ReplaceChanged(Value with { Opinion = value }); } }
}
