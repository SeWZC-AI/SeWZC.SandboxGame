using System.Collections.Immutable;

namespace SeWZC.WorldBox.Core.Runtime;

/// <summary>WorldState 的引擎内定位引用；每次写入提交新的不可变状态。</summary>
internal sealed partial class WorldStateCursor : StateCursor<global::SeWZC.WorldBox.Core.WorldState>
{
    public WorldStateCursor() : this(new() { Width = 0, Height = 0, Tiles = [] }) { }
    public WorldStateCursor(global::SeWZC.WorldBox.Core.WorldState value) : base(value) { }
    internal global::SeWZC.WorldBox.Core.WorldState Snapshot
    {
        get
        {
            _Tiles?.FlushUpdates();
            _Residents?.FlushUpdates();
            _Settlements?.FlushUpdates();
            FlushScalars();
            return Value;
        }
    }
    public static implicit operator global::SeWZC.WorldBox.Core.WorldState(WorldStateCursor cursor) => cursor.Snapshot;
    public static implicit operator WorldStateCursor(global::SeWZC.WorldBox.Core.WorldState value) => new(value);
    public int FormatVersion { get => Value.FormatVersion; set { if (!EqualityComparer<int>.Default.Equals(Value.FormatVersion, value)) ReplaceChanged(Value with { FormatVersion = value }); } }
    public int Seed { get => Value.Seed; set { if (!EqualityComparer<int>.Default.Equals(Value.Seed, value)) ReplaceChanged(Value with { Seed = value }); } }
    public int Width { get => Value.Width; set { if (!EqualityComparer<int>.Default.Equals(Value.Width, value)) ReplaceChanged(Value with { Width = value }); } }
    public int Height { get => Value.Height; set { if (!EqualityComparer<int>.Default.Equals(Value.Height, value)) ReplaceChanged(Value with { Height = value }); } }
    public long Tick { get => _scalarDepth > 0 ? _tick : Value.Tick; set { if (_scalarDepth > 0) { _tick = value; _scalarsChanged = true; } else if (Value.Tick != value) ReplaceChanged(Value with { Tick = value }); } }
    public uint RandomState { get => _scalarDepth > 0 ? _randomState : Value.RandomState; set { if (_scalarDepth > 0) { _randomState = value; _scalarsChanged = true; } else if (Value.RandomState != value) ReplaceChanged(Value with { RandomState = value }); } }
    public int NextId { get => _scalarDepth > 0 ? _nextId : Value.NextId; set { if (_scalarDepth > 0) { _nextId = value; _scalarsChanged = true; } else if (Value.NextId != value) ReplaceChanged(Value with { NextId = value }); } }
    private EntityListCursor<global::SeWZC.WorldBox.Core.Tile, TileCursor>? _Tiles;
    public EntityListCursor<global::SeWZC.WorldBox.Core.Tile, TileCursor> Tiles
    {
        get => _Tiles ??= new(Value.Tiles, value => { if (!ReferenceEquals(Value.Tiles, value)) ReplaceChanged(Value with { Tiles = value }); }, value => new TileCursor(value));
        set { _Tiles = null; Replace(Value with { Tiles = value.Snapshot }); }
    }
    private EntityListCursor<global::SeWZC.WorldBox.Core.Resident, ResidentCursor>? _Residents;
    public EntityListCursor<global::SeWZC.WorldBox.Core.Resident, ResidentCursor> Residents
    {
        get => _Residents ??= new(Value.Residents, value => { if (!ReferenceEquals(Value.Residents, value)) ReplaceChanged(Value with { Residents = value }); }, value => new ResidentCursor(value));
        set { _Residents = null; Replace(Value with { Residents = value.Snapshot }); }
    }
    private EntityListCursor<global::SeWZC.WorldBox.Core.Settlement, SettlementCursor>? _Settlements;
    public EntityListCursor<global::SeWZC.WorldBox.Core.Settlement, SettlementCursor> Settlements
    {
        get => _Settlements ??= new(Value.Settlements, value => { if (!ReferenceEquals(Value.Settlements, value)) ReplaceChanged(Value with { Settlements = value }); }, value => new SettlementCursor(value));
        set { _Settlements = null; Replace(Value with { Settlements = value.Snapshot }); }
    }
    private EntityListCursor<global::SeWZC.WorldBox.Core.Nation, NationCursor>? _Nations;
    public EntityListCursor<global::SeWZC.WorldBox.Core.Nation, NationCursor> Nations
    {
        get => _Nations ??= new(Value.Nations, value => { if (!ReferenceEquals(Value.Nations, value)) ReplaceChanged(Value with { Nations = value }); }, value => new NationCursor(value));
        set { _Nations = null; Replace(Value with { Nations = value.Snapshot }); }
    }
    private EntityListCursor<global::SeWZC.WorldBox.Core.Army, ArmyCursor>? _Armies;
    public EntityListCursor<global::SeWZC.WorldBox.Core.Army, ArmyCursor> Armies
    {
        get => _Armies ??= new(Value.Armies, value => { if (!ReferenceEquals(Value.Armies, value)) ReplaceChanged(Value with { Armies = value }); }, value => new ArmyCursor(value));
        set { _Armies = null; Replace(Value with { Armies = value.Snapshot }); }
    }
    private EntityListCursor<global::SeWZC.WorldBox.Core.DiplomaticRelation, DiplomaticRelationCursor>? _Diplomacies;
    public EntityListCursor<global::SeWZC.WorldBox.Core.DiplomaticRelation, DiplomaticRelationCursor> Diplomacies
    {
        get => _Diplomacies ??= new(Value.Diplomacies, value => { if (!ReferenceEquals(Value.Diplomacies, value)) ReplaceChanged(Value with { Diplomacies = value }); }, value => new DiplomaticRelationCursor(value));
        set { _Diplomacies = null; Replace(Value with { Diplomacies = value.Snapshot }); }
    }
    private EntityListCursor<global::SeWZC.WorldBox.Core.WorldEvent, WorldEventCursor>? _Events;
    public EntityListCursor<global::SeWZC.WorldBox.Core.WorldEvent, WorldEventCursor> Events
    {
        get => _Events ??= new(Value.Events, value => { if (!ReferenceEquals(Value.Events, value)) ReplaceChanged(Value with { Events = value }); }, value => new WorldEventCursor(value));
        set { _Events = null; Replace(Value with { Events = value.Snapshot }); }
    }
    public bool NaturalDisasters { get => Value.NaturalDisasters; set { if (!EqualityComparer<bool>.Default.Equals(Value.NaturalDisasters, value)) ReplaceChanged(Value with { NaturalDisasters = value }); } }
    public int Year => 1 + (int)(Tick / 120);
    public int Day => 1 + (int)(Tick % 120);
    public int Population => Value.Population;
    public WorldRules Rules { get => Value.Rules; set { if (!EqualityComparer<WorldRules>.Default.Equals(Value.Rules, value)) ReplaceChanged(Value with { Rules = value }); } }
    private EntityListCursor<global::SeWZC.WorldBox.Core.LocalConflict, LocalConflictCursor>? _Conflicts;
    public EntityListCursor<global::SeWZC.WorldBox.Core.LocalConflict, LocalConflictCursor> Conflicts
    {
        get => _Conflicts ??= new(Value.Conflicts, value => { if (!ReferenceEquals(Value.Conflicts, value)) ReplaceChanged(Value with { Conflicts = value }); }, value => new LocalConflictCursor(value));
        set { _Conflicts = null; Replace(Value with { Conflicts = value.Snapshot }); }
    }
    public int SimulationVersion { get => Value.SimulationVersion; set { if (!EqualityComparer<int>.Default.Equals(Value.SimulationVersion, value)) ReplaceChanged(Value with { SimulationVersion = value }); } }
    private SocietyStateCursor? _Society;
    public SocietyStateCursor Society
    {
        get
        {
            if (_Society is null)
            {
                _Society = new(Value.Society);
                _Society.Bind(value => { if (!ReferenceEquals(Value.Society, value)) ReplaceChanged(Value with { Society = value }); });
            }
            return _Society;
        }
        set { _Society = null; Replace(Value with { Society = value.Value }); }
    }
    private SnapshotListCursor<PendingMessage>? _PendingMessages;
    public SnapshotListCursor<PendingMessage> PendingMessages
    {
        get => _PendingMessages ??= new(Value.PendingMessages, value => { if (!ReferenceEquals(Value.PendingMessages, value)) ReplaceChanged(Value with { PendingMessages = value }); });
        set { _PendingMessages = null; Replace(Value with { PendingMessages = value.Snapshot }); }
    }
    private EntityListCursor<global::SeWZC.WorldBox.Core.Resident, ResidentCursor>? _ArchivedResidents;
    public EntityListCursor<global::SeWZC.WorldBox.Core.Resident, ResidentCursor> ArchivedResidents
    {
        get => _ArchivedResidents ??= new(Value.ArchivedResidents, value => { if (!ReferenceEquals(Value.ArchivedResidents, value)) ReplaceChanged(Value with { ArchivedResidents = value }); }, value => new ResidentCursor(value));
        set { _ArchivedResidents = null; Replace(Value with { ArchivedResidents = value.Snapshot }); }
    }
    protected override void OnReplace(in global::SeWZC.WorldBox.Core.WorldState before, in global::SeWZC.WorldBox.Core.WorldState after)
    {
        if (_Society is not null && !ReferenceEquals(_Society.Value, after.Society))
            _Society.Synchronize(after.Society);
    }
}
