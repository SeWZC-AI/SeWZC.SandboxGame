using System.Collections.Immutable;

namespace SeWZC.WorldBox.Core.Runtime;

/// <summary>WorldState 的引擎内定位引用；日内标量及集合更新在快照边界合并提交。</summary>
internal sealed partial class WorldStateCursor : StateReference<WorldState>
{
    private EntityListCursor<Resident, ResidentCursor>? _archivedResidents;
    private EntityListCursor<Resident, ResidentCursor>? _residents;
    private EntityListCursor<Settlement, StateReference<Settlement>>? _settlements;
    private EntityListCursor<Tile, StateReference<Tile>>? _tiles;
    private EntityListCursor<Nation, StateReference<Nation>>? _nations;
    private EntityListCursor<Army, StateReference<Army>>? _armies;
    private EntityListCursor<Building, StateReference<Building>>? _buildings;
    public WorldStateCursor(WorldState value) : base(value) { }

    internal WorldState CaptureSnapshot()
    {
        _tiles?.FlushUpdates();
        _residents?.FlushUpdates();
        _archivedResidents?.FlushUpdates();
        _settlements?.FlushUpdates();
        _nations?.FlushUpdates();
        _armies?.FlushUpdates();
        _buildings?.FlushUpdates();
        FlushScalars();
        return Value;
    }

    public int Seed => Value.Seed;

    public int Width => Value.Width;

    public int Height => Value.Height;

    public long Tick
    {
        get => _scalarDepth > 0 ? _tick : Value.Tick;
        set
        {
            if (_scalarDepth > 0)
            {
                _tick = value;
                _scalarsChanged = true;
            }
            else if (Value.Tick != value)
                ReplaceChanged(Value with { Tick = value });
        }
    }

    public uint RandomState
    {
        get => _scalarDepth > 0 ? _randomState : Value.RandomState;
        set
        {
            if (_scalarDepth > 0)
            {
                _randomState = value;
                _scalarsChanged = true;
            }
            else if (Value.RandomState != value)
                ReplaceChanged(Value with { RandomState = value });
        }
    }

    public int NextId
    {
        get => _scalarDepth > 0 ? _nextId : Value.NextId;
        set
        {
            if (_scalarDepth > 0)
            {
                _nextId = value;
                _scalarsChanged = true;
            }
            else if (Value.NextId != value)
                ReplaceChanged(Value with { NextId = value });
        }
    }

    public EntityListCursor<Tile, StateReference<Tile>> Tiles =>
        _tiles ??= new EntityListCursor<Tile, StateReference<Tile>>(Value.Tiles, value =>
        {
            if (!ReferenceEquals(Value.Tiles, value))
                ReplaceChanged(Value with { Tiles = value });
        }, value => new StateReference<Tile>(value));

    public EntityListCursor<Resident, ResidentCursor> Residents =>
        _residents ??= new EntityListCursor<Resident, ResidentCursor>(Value.Residents, value =>
        {
            if (!ReferenceEquals(Value.Residents, value))
                ReplaceChanged(Value with { Residents = value });
        }, value => new ResidentCursor(value), static (before, after) => before.SettlementId != after.SettlementId);

    public EntityListCursor<Settlement, StateReference<Settlement>> Settlements =>
        _settlements ??= new EntityListCursor<Settlement, StateReference<Settlement>>(Value.Settlements, value =>
        {
            if (!ReferenceEquals(Value.Settlements, value))
                ReplaceChanged(Value with { Settlements = value });
        }, value => new StateReference<Settlement>(value));

    public EntityListCursor<Nation, StateReference<Nation>> Nations =>
        _nations ??= new EntityListCursor<Nation, StateReference<Nation>>(Value.Nations, value =>
        {
            if (!ReferenceEquals(Value.Nations, value))
                ReplaceChanged(Value with { Nations = value });
        }, value => new StateReference<Nation>(value));

    public EntityListCursor<Army, StateReference<Army>> Armies =>
        _armies ??= new EntityListCursor<Army, StateReference<Army>>(Value.Armies, value =>
        {
            if (!ReferenceEquals(Value.Armies, value))
                ReplaceChanged(Value with { Armies = value });
        }, value => new StateReference<Army>(value));

    public ImmutableVector<DiplomaticRelation> Diplomacies
    {
        get => Value.Diplomacies;
        set
        {
            if (!ReferenceEquals(Value.Diplomacies, value))
                ReplaceChanged(Value with { Diplomacies = value });
        }
    }

    public ImmutableVector<WorldEvent> Events
    {
        get => Value.Events;
        set
        {
            if (!ReferenceEquals(Value.Events, value))
                ReplaceChanged(Value with { Events = value });
        }
    }

    public bool NaturalDisasters
    {
        get => Value.NaturalDisasters;
        set
        {
            if (!EqualityComparer<bool>.Default.Equals(Value.NaturalDisasters, value))
                ReplaceChanged(Value with { NaturalDisasters = value });
        }
    }

    public int Year => 1 + (int)(Tick / SimulationTime.TicksPerYear);
    public int Day => 1 + (int)(Tick / SimulationTime.TicksPerDay % SimulationTime.DaysPerMonth);
    public int Population => Residents.Count;

    public WorldRules Rules => Value.Rules;

    public ImmutableVector<LocalConflict> Conflicts
    {
        get => Value.Conflicts;
        set
        {
            if (!ReferenceEquals(Value.Conflicts, value))
                ReplaceChanged(Value with { Conflicts = value });
        }
    }

    public SocietyState Society
    {
        get => Value.Society;
        set
        {
            if (!ReferenceEquals(Value.Society, value))
                ReplaceChanged(Value with { Society = value });
        }
    }

    public EntityListCursor<Building, StateReference<Building>> Buildings =>
        _buildings ??= new EntityListCursor<Building, StateReference<Building>>(Society.Buildings,
            value => Society = Society with { Buildings = value }, value => new StateReference<Building>(value));

    public ImmutableList<PendingMessage> PendingMessages
    {
        get => Value.PendingMessages;
        set
        {
            if (!ReferenceEquals(Value.PendingMessages, value))
                ReplaceChanged(Value with { PendingMessages = value });
        }
    }

    public EntityListCursor<Resident, ResidentCursor> ArchivedResidents =>
        _archivedResidents ??= new EntityListCursor<Resident, ResidentCursor>(Value.ArchivedResidents, value =>
        {
            if (!ReferenceEquals(Value.ArchivedResidents, value))
                ReplaceChanged(Value with { ArchivedResidents = value });
        }, value => new ResidentCursor(value), static (before, after) => before.SettlementId != after.SettlementId);
}
