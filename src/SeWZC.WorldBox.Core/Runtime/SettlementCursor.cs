using System.Collections.Immutable;

namespace SeWZC.WorldBox.Core.Runtime;

/// <summary>Settlement 的引擎内定位引用；每次写入提交新的不可变状态。</summary>
internal sealed partial class SettlementCursor : StateCursor<global::SeWZC.WorldBox.Core.Settlement>
{
    public SettlementCursor() : this(new()) { }
    public SettlementCursor(global::SeWZC.WorldBox.Core.Settlement value) : base(value) { }
    public static implicit operator global::SeWZC.WorldBox.Core.Settlement(SettlementCursor cursor)
    {
        cursor.FlushResources();
        return cursor.Value;
    }
    public static implicit operator SettlementCursor(global::SeWZC.WorldBox.Core.Settlement value) => new(value);
    public string DevelopmentGoal { get => Value.DevelopmentGoal; set { if (!EqualityComparer<string>.Default.Equals(Value.DevelopmentGoal, value)) ReplaceChanged(Value with { DevelopmentGoal = value }); } }
    public string DevelopmentBlocker { get => Value.DevelopmentBlocker; set { if (!EqualityComparer<string>.Default.Equals(Value.DevelopmentBlocker, value)) ReplaceChanged(Value with { DevelopmentBlocker = value }); } }
    public long LastDevelopmentTick { get => Value.LastDevelopmentTick; set { if (!EqualityComparer<long>.Default.Equals(Value.LastDevelopmentTick, value)) ReplaceChanged(Value with { LastDevelopmentTick = value }); } }
    public double Unrest { get => Value.Unrest; set { if (!EqualityComparer<double>.Default.Equals(Value.Unrest, value)) ReplaceChanged(Value with { Unrest = value }); } }
    public long LastPoliticalChangeTick { get => Value.LastPoliticalChangeTick; set { if (!EqualityComparer<long>.Default.Equals(Value.LastPoliticalChangeTick, value)) ReplaceChanged(Value with { LastPoliticalChangeTick = value }); } }
    public SettlementTier Tier { get => Value.Tier; set { if (!EqualityComparer<SettlementTier>.Default.Equals(Value.Tier, value)) ReplaceChanged(Value with { Tier = value }); } }
    public double ExpansionProgress { get => Value.ExpansionProgress; set { if (!EqualityComparer<double>.Default.Equals(Value.ExpansionProgress, value)) ReplaceChanged(Value with { ExpansionProgress = value }); } }
    public double ExpansionRequired { get => Value.ExpansionRequired; set { if (!EqualityComparer<double>.Default.Equals(Value.ExpansionRequired, value)) ReplaceChanged(Value with { ExpansionRequired = value }); } }
    public bool IsExpanding => Value.IsExpanding;
    public int Id { get => Value.Id; set { if (!EqualityComparer<int>.Default.Equals(Value.Id, value)) ReplaceChanged(Value with { Id = value }); } }
    public string Name { get => Value.Name; set { if (!EqualityComparer<string>.Default.Equals(Value.Name, value)) ReplaceChanged(Value with { Name = value }); } }
    public int X { get => Value.X; set { if (!EqualityComparer<int>.Default.Equals(Value.X, value)) ReplaceChanged(Value with { X = value }); } }
    public int Y { get => Value.Y; set { if (!EqualityComparer<int>.Default.Equals(Value.Y, value)) ReplaceChanged(Value with { Y = value }); } }
    public int NationId { get => Value.NationId; set { if (!EqualityComparer<int>.Default.Equals(Value.NationId, value)) ReplaceChanged(Value with { NationId = value }); } }
    public ResourceStock Resources
    {
        get => _resourceDepth > 0 ? _resourceDraft : Value.Resources;
        set
        {
            if (_resourceDepth > 0)
            {
                if (_resourceDraft != value) { _resourceDraft = value; _resourcesChanged = true; }
            }
            else if (Value.Resources != value) ReplaceChanged(Value with { Resources = value });
        }
    }
    public int Population { get => Value.Population; set { if (!EqualityComparer<int>.Default.Equals(Value.Population, value)) ReplaceChanged(Value with { Population = value }); } }
    public int Housing { get => Value.Housing; set { if (!EqualityComparer<int>.Default.Equals(Value.Housing, value)) ReplaceChanged(Value with { Housing = value }); } }
    public bool FoundationPending { get => Value.FoundationPending; set { if (!EqualityComparer<bool>.Default.Equals(Value.FoundationPending, value)) ReplaceChanged(Value with { FoundationPending = value }); } }
    public int MaxClaimRadius { get => Value.MaxClaimRadius; set { if (!EqualityComparer<int>.Default.Equals(Value.MaxClaimRadius, value)) ReplaceChanged(Value with { MaxClaimRadius = value }); } }
    public int CultureId { get => Value.CultureId; set { if (!EqualityComparer<int>.Default.Equals(Value.CultureId, value)) ReplaceChanged(Value with { CultureId = value }); } }
    public int RepresentativeId { get => Value.RepresentativeId; set { if (!EqualityComparer<int>.Default.Equals(Value.RepresentativeId, value)) ReplaceChanged(Value with { RepresentativeId = value }); } }
    private SnapshotListCursor<AgentFact>? _PublicKnowledge;
    public SnapshotListCursor<AgentFact> PublicKnowledge
    {
        get => _PublicKnowledge ??= new(Value.PublicKnowledge, value => { if (!ReferenceEquals(Value.PublicKnowledge, value)) ReplaceChanged(Value with { PublicKnowledge = value }); });
        set { _PublicKnowledge = null; Replace(Value with { PublicKnowledge = value.Snapshot }); }
    }
    public int FertilityBoostTicks { get => Value.FertilityBoostTicks; set { if (!EqualityComparer<int>.Default.Equals(Value.FertilityBoostTicks, value)) ReplaceChanged(Value with { FertilityBoostTicks = value }); } }
    public int ShieldTicks { get => Value.ShieldTicks; set { if (!EqualityComparer<int>.Default.Equals(Value.ShieldTicks, value)) ReplaceChanged(Value with { ShieldTicks = value }); } }
}
