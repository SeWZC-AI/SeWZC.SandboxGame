using System.Collections.Immutable;

namespace SeWZC.WorldBox.Core.Runtime;

/// <summary>CulturalContact 的引擎内定位引用；每次写入提交新的不可变状态。</summary>
internal sealed partial class CulturalContactCursor : StateCursor<global::SeWZC.WorldBox.Core.CulturalContact>
{
    public CulturalContactCursor() : this(new()) { }
    public CulturalContactCursor(global::SeWZC.WorldBox.Core.CulturalContact value) : base(value) { }
    public static implicit operator global::SeWZC.WorldBox.Core.CulturalContact(CulturalContactCursor cursor) => cursor.Value;
    public static implicit operator CulturalContactCursor(global::SeWZC.WorldBox.Core.CulturalContact value) => new(value);
    public int ResidentId { get => Value.ResidentId; set { if (!EqualityComparer<int>.Default.Equals(Value.ResidentId, value)) Replace(Value with { ResidentId = value }); } }
    public int CultureId { get => Value.CultureId; set { if (!EqualityComparer<int>.Default.Equals(Value.CultureId, value)) Replace(Value with { CultureId = value }); } }
    public double Exposure { get => Value.Exposure; set { if (!EqualityComparer<double>.Default.Equals(Value.Exposure, value)) Replace(Value with { Exposure = value }); } }
    public long LastContactTick { get => Value.LastContactTick; set { if (!EqualityComparer<long>.Default.Equals(Value.LastContactTick, value)) Replace(Value with { LastContactTick = value }); } }
}
