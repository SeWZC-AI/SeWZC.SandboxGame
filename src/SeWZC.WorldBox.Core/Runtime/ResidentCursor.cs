using System.Collections.Immutable;

namespace SeWZC.WorldBox.Core.Runtime;

/// <summary>Resident 的引擎内定位引用；每次写入提交新的不可变状态。</summary>
internal sealed partial class ResidentCursor : StateCursor<global::SeWZC.WorldBox.Core.Resident>
{
    public ResidentCursor() : this(new()) { }
    public ResidentCursor(global::SeWZC.WorldBox.Core.Resident value) : base(value) { }
    public static implicit operator global::SeWZC.WorldBox.Core.Resident(ResidentCursor cursor) => cursor.Value;
    public static implicit operator ResidentCursor(global::SeWZC.WorldBox.Core.Resident value) => new(value);
    public int Id { get => Value.Id; set { if (!EqualityComparer<int>.Default.Equals(Value.Id, value)) ReplaceChanged(Value with { Id = value }); } }
    public string Name { get => Value.Name; set { if (!EqualityComparer<string>.Default.Equals(Value.Name, value)) ReplaceChanged(Value with { Name = value }); } }
    public RaceKind Race { get => Value.Race; set { if (!EqualityComparer<RaceKind>.Default.Equals(Value.Race, value)) ReplaceChanged(Value with { Race = value }); } }
    public int X { get => Value.X; set { if (!EqualityComparer<int>.Default.Equals(Value.X, value)) ReplaceChanged(Value with { X = value }); } }
    public int Y { get => Value.Y; set { if (!EqualityComparer<int>.Default.Equals(Value.Y, value)) ReplaceChanged(Value with { Y = value }); } }
    public double Age { get => Value.Age; set { if (!EqualityComparer<double>.Default.Equals(Value.Age, value)) ReplaceChanged(Value with { Age = value }); } }
    public int NationId { get => Value.NationId; set { if (!EqualityComparer<int>.Default.Equals(Value.NationId, value)) ReplaceChanged(Value with { NationId = value }); } }
    public int SettlementId { get => Value.SettlementId; set { if (!EqualityComparer<int>.Default.Equals(Value.SettlementId, value)) ReplaceChanged(Value with { SettlementId = value }); } }
    public Profession Profession { get => Value.Profession; set { if (!EqualityComparer<Profession>.Default.Equals(Value.Profession, value)) ReplaceChanged(Value with { Profession = value }); } }
    public ResidentActivity Activity { get => Value.Activity; set { if (!EqualityComparer<ResidentActivity>.Default.Equals(Value.Activity, value)) ReplaceChanged(Value with { Activity = value }); } }
    public double Health { get => Value.Health; set { if (!EqualityComparer<double>.Default.Equals(Value.Health, value)) ReplaceChanged(Value with { Health = value }); } }
    public double Hunger { get => Value.Hunger; set { if (!EqualityComparer<double>.Default.Equals(Value.Hunger, value)) ReplaceChanged(Value with { Hunger = value }); } }
    public int SicknessTicks { get => Value.SicknessTicks; set { if (!EqualityComparer<int>.Default.Equals(Value.SicknessTicks, value)) ReplaceChanged(Value with { SicknessTicks = value }); } }
    public int ArmyId { get => Value.ArmyId; set { if (!EqualityComparer<int>.Default.Equals(Value.ArmyId, value)) ReplaceChanged(Value with { ArmyId = value }); } }
    public string Trait { get => Value.Trait; set { if (!EqualityComparer<string>.Default.Equals(Value.Trait, value)) ReplaceChanged(Value with { Trait = value }); } }
    public long DiseaseImmuneUntilTick { get => Value.DiseaseImmuneUntilTick; set { if (!EqualityComparer<long>.Default.Equals(Value.DiseaseImmuneUntilTick, value)) ReplaceChanged(Value with { DiseaseImmuneUntilTick = value }); } }
    public DeathCause DeathCause { get => Value.DeathCause; set { if (!EqualityComparer<DeathCause>.Default.Equals(Value.DeathCause, value)) ReplaceChanged(Value with { DeathCause = value }); } }
    public long DeathTick { get => Value.DeathTick; set { if (!EqualityComparer<long>.Default.Equals(Value.DeathTick, value)) ReplaceChanged(Value with { DeathTick = value }); } }
    public double Armor { get => Value.Armor; set { if (!EqualityComparer<double>.Default.Equals(Value.Armor, value)) ReplaceChanged(Value with { Armor = value }); } }
    public double PersonalWard { get => Value.PersonalWard; set { if (!EqualityComparer<double>.Default.Equals(Value.PersonalWard, value)) ReplaceChanged(Value with { PersonalWard = value }); } }
    public long FrozenUntilTick { get => Value.FrozenUntilTick; set { if (!EqualityComparer<long>.Default.Equals(Value.FrozenUntilTick, value)) ReplaceChanged(Value with { FrozenUntilTick = value }); } }
    public long LastRangedAttackTick { get => Value.LastRangedAttackTick; set { if (!EqualityComparer<long>.Default.Equals(Value.LastRangedAttackTick, value)) ReplaceChanged(Value with { LastRangedAttackTick = value }); } }
    public TravelMode TravelMode { get => Value.TravelMode; set { if (!EqualityComparer<TravelMode>.Default.Equals(Value.TravelMode, value)) ReplaceChanged(Value with { TravelMode = value }); } }
    public double Thirst { get => Value.Thirst; set { if (!EqualityComparer<double>.Default.Equals(Value.Thirst, value)) ReplaceChanged(Value with { Thirst = value }); } }
    public int CultureId { get => Value.CultureId; set { if (!EqualityComparer<int>.Default.Equals(Value.CultureId, value)) ReplaceChanged(Value with { CultureId = value }); } }
    private AgentStateCursor? _Agent;
    public AgentStateCursor Agent
    {
        get
        {
            if (_Agent is null)
            {
                _Agent = new(Value.Agent);
                _Agent.Bind(value => { if (!ReferenceEquals(Value.Agent, value)) ReplaceChanged(Value with { Agent = value }); });
            }
            return _Agent;
        }
        set { _Agent = null; Replace(Value with { Agent = value.Value }); }
    }
    public ResourceStock Inventory { get => Value.Inventory; set { if (!EqualityComparer<ResourceStock>.Default.Equals(Value.Inventory, value)) ReplaceChanged(Value with { Inventory = value }); } }
    public double Mana { get => Value.Mana; set { if (!EqualityComparer<double>.Default.Equals(Value.Mana, value)) ReplaceChanged(Value with { Mana = value }); } }
    public double MagicTalent { get => Value.MagicTalent; set { if (!EqualityComparer<double>.Default.Equals(Value.MagicTalent, value)) ReplaceChanged(Value with { MagicTalent = value }); } }
    public double MagicTraining { get => Value.MagicTraining; set { if (!EqualityComparer<double>.Default.Equals(Value.MagicTraining, value)) ReplaceChanged(Value with { MagicTraining = value }); } }
    public int FromX { get => Value.FromX; set { if (!EqualityComparer<int>.Default.Equals(Value.FromX, value)) ReplaceChanged(Value with { FromX = value }); } }
    public int FromY { get => Value.FromY; set { if (!EqualityComparer<int>.Default.Equals(Value.FromY, value)) ReplaceChanged(Value with { FromY = value }); } }
    public long MoveStartedTick { get => Value.MoveStartedTick; set { if (!EqualityComparer<long>.Default.Equals(Value.MoveStartedTick, value)) ReplaceChanged(Value with { MoveStartedTick = value }); } }
    public int MoveDurationTicks { get => Value.MoveDurationTicks; set { if (!EqualityComparer<int>.Default.Equals(Value.MoveDurationTicks, value)) ReplaceChanged(Value with { MoveDurationTicks = value }); } }
    private SnapshotListCursor<ResidentHistoryEntry>? _History;
    public SnapshotListCursor<ResidentHistoryEntry> History
    {
        get => _History ??= new(Value.History, value => { if (!ReferenceEquals(Value.History, value)) ReplaceChanged(Value with { History = value }); });
        set { _History = null; Replace(Value with { History = value.Snapshot }); }
    }
    protected override void OnReplace(global::SeWZC.WorldBox.Core.Resident before, global::SeWZC.WorldBox.Core.Resident after)
    {
        if (_Agent is not null && !ReferenceEquals(_Agent.Value, after.Agent))
            _Agent.Synchronize(after.Agent);
    }
}
