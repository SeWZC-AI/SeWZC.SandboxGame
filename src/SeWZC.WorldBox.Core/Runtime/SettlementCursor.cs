using System.Collections.Immutable;

namespace SeWZC.WorldBox.Core.Runtime;

/// <summary>Settlement 的引擎内定位引用；每次写入提交新的不可变状态。</summary>
internal sealed partial class SettlementCursor : StateCursor<global::SeWZC.WorldBox.Core.Settlement>
{
    public SettlementCursor() : this(new()) { }
    public SettlementCursor(global::SeWZC.WorldBox.Core.Settlement value) : base(value) { }
    public static implicit operator global::SeWZC.WorldBox.Core.Settlement(SettlementCursor cursor) => cursor.Value;
    public static implicit operator SettlementCursor(global::SeWZC.WorldBox.Core.Settlement value) => new(value);
    public string DevelopmentGoal { get => Value.DevelopmentGoal; set { if (!EqualityComparer<string>.Default.Equals(Value.DevelopmentGoal, value)) Replace(Value with { DevelopmentGoal = value }); } }
    public string DevelopmentBlocker { get => Value.DevelopmentBlocker; set { if (!EqualityComparer<string>.Default.Equals(Value.DevelopmentBlocker, value)) Replace(Value with { DevelopmentBlocker = value }); } }
    public long LastDevelopmentTick { get => Value.LastDevelopmentTick; set { if (!EqualityComparer<long>.Default.Equals(Value.LastDevelopmentTick, value)) Replace(Value with { LastDevelopmentTick = value }); } }
    public double Unrest { get => Value.Unrest; set { if (!EqualityComparer<double>.Default.Equals(Value.Unrest, value)) Replace(Value with { Unrest = value }); } }
    public long LastPoliticalChangeTick { get => Value.LastPoliticalChangeTick; set { if (!EqualityComparer<long>.Default.Equals(Value.LastPoliticalChangeTick, value)) Replace(Value with { LastPoliticalChangeTick = value }); } }
    public SettlementTier Tier { get => Value.Tier; set { if (!EqualityComparer<SettlementTier>.Default.Equals(Value.Tier, value)) Replace(Value with { Tier = value }); } }
    public double ExpansionProgress { get => Value.ExpansionProgress; set { if (!EqualityComparer<double>.Default.Equals(Value.ExpansionProgress, value)) Replace(Value with { ExpansionProgress = value }); } }
    public double ExpansionRequired { get => Value.ExpansionRequired; set { if (!EqualityComparer<double>.Default.Equals(Value.ExpansionRequired, value)) Replace(Value with { ExpansionRequired = value }); } }
    public bool IsExpanding => Value.IsExpanding;
    public int Id { get => Value.Id; set { if (!EqualityComparer<int>.Default.Equals(Value.Id, value)) Replace(Value with { Id = value }); } }
    public string Name { get => Value.Name; set { if (!EqualityComparer<string>.Default.Equals(Value.Name, value)) Replace(Value with { Name = value }); } }
    public int X { get => Value.X; set { if (!EqualityComparer<int>.Default.Equals(Value.X, value)) Replace(Value with { X = value }); } }
    public int Y { get => Value.Y; set { if (!EqualityComparer<int>.Default.Equals(Value.Y, value)) Replace(Value with { Y = value }); } }
    public int NationId { get => Value.NationId; set { if (!EqualityComparer<int>.Default.Equals(Value.NationId, value)) Replace(Value with { NationId = value }); } }
    public ResourceStock Resources { get => Value.Resources; set { if (!EqualityComparer<ResourceStock>.Default.Equals(Value.Resources, value)) Replace(Value with { Resources = value }); } }
    public int Population { get => Value.Population; set { if (!EqualityComparer<int>.Default.Equals(Value.Population, value)) Replace(Value with { Population = value }); } }
    public int Housing { get => Value.Housing; set { if (!EqualityComparer<int>.Default.Equals(Value.Housing, value)) Replace(Value with { Housing = value }); } }
    public bool FoundationPending { get => Value.FoundationPending; set { if (!EqualityComparer<bool>.Default.Equals(Value.FoundationPending, value)) Replace(Value with { FoundationPending = value }); } }
    public int MaxClaimRadius { get => Value.MaxClaimRadius; set { if (!EqualityComparer<int>.Default.Equals(Value.MaxClaimRadius, value)) Replace(Value with { MaxClaimRadius = value }); } }
    public int CultureId { get => Value.CultureId; set { if (!EqualityComparer<int>.Default.Equals(Value.CultureId, value)) Replace(Value with { CultureId = value }); } }
    public int RepresentativeId { get => Value.RepresentativeId; set { if (!EqualityComparer<int>.Default.Equals(Value.RepresentativeId, value)) Replace(Value with { RepresentativeId = value }); } }
    private SnapshotListCursor<AgentFact>? _PublicKnowledge;
    public SnapshotListCursor<AgentFact> PublicKnowledge
    {
        get => _PublicKnowledge ??= new(Value.PublicKnowledge, value => Replace(Value with { PublicKnowledge = value }));
        set { _PublicKnowledge = null; Replace(Value with { PublicKnowledge = value.Snapshot }); }
    }
    public int FertilityBoostTicks { get => Value.FertilityBoostTicks; set { if (!EqualityComparer<int>.Default.Equals(Value.FertilityBoostTicks, value)) Replace(Value with { FertilityBoostTicks = value }); } }
    public int ShieldTicks { get => Value.ShieldTicks; set { if (!EqualityComparer<int>.Default.Equals(Value.ShieldTicks, value)) Replace(Value with { ShieldTicks = value }); } }
}
