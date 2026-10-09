using System.Collections.Immutable;

namespace SeWZC.WorldBox.Core.Runtime;

/// <summary>WorldState 的引擎内定位引用；日内标量及集合更新在快照边界合并提交。</summary>
internal sealed partial class WorldStateCursor : StateCursor<WorldState>
{
    private EntityListCursor<Resident, ResidentCursor>? _archivedResidents;
    private EntityListCursor<Resident, ResidentCursor>? _residents;
    private EntityListCursor<Settlement, SettlementCursor>? _settlements;
    private EntityListCursor<Tile, TileCursor>? _tiles;
    public WorldStateCursor(WorldState value) : base(value) { }

    internal WorldState Snapshot
    {
        get
        {
            _tiles?.FlushUpdates();
            _residents?.FlushUpdates();
            _archivedResidents?.FlushUpdates();
            _settlements?.FlushUpdates();
            FlushScalars();
            return Value;
        }
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

    public EntityListCursor<Tile, TileCursor> Tiles =>
        _tiles ??= new EntityListCursor<Tile, TileCursor>(Value.Tiles, value =>
        {
            if (!ReferenceEquals(Value.Tiles, value))
                ReplaceChanged(Value with { Tiles = value });
        }, value => new TileCursor(value));

    public EntityListCursor<Resident, ResidentCursor> Residents =>
        _residents ??= new EntityListCursor<Resident, ResidentCursor>(Value.Residents, value =>
        {
            if (!ReferenceEquals(Value.Residents, value))
                ReplaceChanged(Value with { Residents = value });
        }, value => new ResidentCursor(value));

    public EntityListCursor<Settlement, SettlementCursor> Settlements =>
        _settlements ??= new EntityListCursor<Settlement, SettlementCursor>(Value.Settlements, value =>
        {
            if (!ReferenceEquals(Value.Settlements, value))
                ReplaceChanged(Value with { Settlements = value });
        }, value => new SettlementCursor(value));

    public EntityListCursor<Nation, NationCursor> Nations =>
        field ??= new EntityListCursor<Nation, NationCursor>(Value.Nations, value =>
        {
            if (!ReferenceEquals(Value.Nations, value))
                ReplaceChanged(Value with { Nations = value });
        }, value => new NationCursor(value));

    public EntityListCursor<Army, ArmyCursor> Armies =>
        field ??= new EntityListCursor<Army, ArmyCursor>(Value.Armies, value =>
        {
            if (!ReferenceEquals(Value.Armies, value))
                ReplaceChanged(Value with { Armies = value });
        }, value => new ArmyCursor(value));

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

    public EntityListCursor<Building, BuildingCursor> Buildings =>
        field ??= new EntityListCursor<Building, BuildingCursor>(Society.Buildings,
            value => Society = Society with { Buildings = value }, value => new BuildingCursor(value));

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
        }, value => new ResidentCursor(value));

    public static implicit operator WorldState(WorldStateCursor cursor)
    {
        return cursor.Snapshot;
    }

    public static implicit operator WorldStateCursor(WorldState value)
    {
        return new WorldStateCursor(value);
    }
}
