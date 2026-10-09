using System.Collections.Immutable;

namespace SeWZC.WorldBox.Core.Runtime;

/// <summary>Settlement 的引擎内定位引用；连续仓库补给在阶段结束或读取快照时合并提交。</summary>
internal sealed partial class SettlementCursor : StateReference<Settlement>
{
    public SettlementCursor(Settlement value) : base(value) { }

    public string DevelopmentGoal => Value.DevelopmentGoal;

    public string DevelopmentBlocker => Value.DevelopmentBlocker;

    public long LastDevelopmentTick => Value.LastDevelopmentTick;

    public double Unrest => Value.Unrest;

    public long LastPoliticalChangeTick => Value.LastPoliticalChangeTick;

    public SettlementTier Tier => Value.Tier;

    public double ExpansionProgress => Value.ExpansionProgress;

    public double ExpansionRequired => Value.ExpansionRequired;

    public bool IsExpanding => Value.IsExpanding;

    public int Id => Value.Id;

    public string Name => Value.Name;

    public int X => Value.X;

    public int Y => Value.Y;

    public int NationId => Value.NationId;

    public ResourceStock Resources => _resourceDepth > 0 ? _resourceDraft : Value.Resources;

    internal void UpdateResources(in ResourceStock value)
    {
        if (_resourceDepth > 0)
        {
            if (_resourceDraft != value)
            {
                _resourceDraft = value;
                _resourcesChanged = true;
            }
        }
        else if (Value.Resources != value)
            ReplaceChanged(Value with { Resources = value });
    }

    public int Population => Value.Population;

    public int Housing => Value.Housing;

    public bool FoundationPending => Value.FoundationPending;

    public int MaxClaimRadius => Value.MaxClaimRadius;

    public int CultureId => Value.CultureId;

    public int RepresentativeId => Value.RepresentativeId;

    public ImmutableList<AgentFact> PublicKnowledge => Value.PublicKnowledge;

    public int FertilityBoostTicks => Value.FertilityBoostTicks;

    public int ShieldTicks => Value.ShieldTicks;
}
