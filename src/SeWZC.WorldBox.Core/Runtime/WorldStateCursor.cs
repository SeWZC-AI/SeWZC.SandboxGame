namespace SeWZC.WorldBox.Core.Runtime;

/// <summary>WorldState 的引擎内定位引用；日内标量及集合更新在快照边界合并提交。</summary>
internal sealed partial class WorldStateCursor : StateCursor<WorldState>
{
    private EntityListCursor<Resident, ResidentCursor>? _archivedResidents;
    private EntityListCursor<Resident, ResidentCursor>? _residents;
    private EntityListCursor<Settlement, SettlementCursor>? _settlements;
    private SocietyStateCursor? _society;
    private EntityListCursor<Tile, TileCursor>? _tiles;
    public WorldStateCursor() : this(new WorldState { Width = 0, Height = 0, Tiles = [] }) { }
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

    public int FormatVersion
    {
        get => Value.FormatVersion;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(Value.FormatVersion, value))
                ReplaceChanged(Value with { FormatVersion = value });
        }
    }

    public int Seed
    {
        get => Value.Seed;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(Value.Seed, value))
                ReplaceChanged(Value with { Seed = value });
        }
    }

    public int Width
    {
        get => Value.Width;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(Value.Width, value))
                ReplaceChanged(Value with { Width = value });
        }
    }

    public int Height
    {
        get => Value.Height;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(Value.Height, value))
                ReplaceChanged(Value with { Height = value });
        }
    }

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

    public EntityListCursor<Tile, TileCursor> Tiles
    {
        get => _tiles ??= new EntityListCursor<Tile, TileCursor>(Value.Tiles, value =>
        {
            if (!ReferenceEquals(Value.Tiles, value))
                ReplaceChanged(Value with { Tiles = value });
        }, value => new TileCursor(value));
        set
        {
            _tiles = null;
            Replace(Value with { Tiles = value.Snapshot });
        }
    }

    public EntityListCursor<Resident, ResidentCursor> Residents
    {
        get => _residents ??= new EntityListCursor<Resident, ResidentCursor>(Value.Residents, value =>
        {
            if (!ReferenceEquals(Value.Residents, value))
                ReplaceChanged(Value with { Residents = value });
        }, value => new ResidentCursor(value));
        set
        {
            _residents = null;
            Replace(Value with { Residents = value.Snapshot });
        }
    }

    public EntityListCursor<Settlement, SettlementCursor> Settlements
    {
        get => _settlements ??= new EntityListCursor<Settlement, SettlementCursor>(Value.Settlements, value =>
        {
            if (!ReferenceEquals(Value.Settlements, value))
                ReplaceChanged(Value with { Settlements = value });
        }, value => new SettlementCursor(value));
        set
        {
            _settlements = null;
            Replace(Value with { Settlements = value.Snapshot });
        }
    }

    public EntityListCursor<Nation, NationCursor> Nations
    {
        get => field ??= new EntityListCursor<Nation, NationCursor>(Value.Nations, value =>
        {
            if (!ReferenceEquals(Value.Nations, value))
                ReplaceChanged(Value with { Nations = value });
        }, value => new NationCursor(value));
        set
        {
            field = null;
            Replace(Value with { Nations = value.Snapshot });
        }
    }

    public EntityListCursor<Army, ArmyCursor> Armies
    {
        get => field ??= new EntityListCursor<Army, ArmyCursor>(Value.Armies, value =>
        {
            if (!ReferenceEquals(Value.Armies, value))
                ReplaceChanged(Value with { Armies = value });
        }, value => new ArmyCursor(value));
        set
        {
            field = null;
            Replace(Value with { Armies = value.Snapshot });
        }
    }

    public EntityListCursor<DiplomaticRelation, DiplomaticRelationCursor> Diplomacies
    {
        get => field ??= new EntityListCursor<DiplomaticRelation, DiplomaticRelationCursor>(Value.Diplomacies,
            value =>
            {
                if (!ReferenceEquals(Value.Diplomacies, value))
                    ReplaceChanged(Value with { Diplomacies = value });
            }, value => new DiplomaticRelationCursor(value));
        set
        {
            field = null;
            Replace(Value with { Diplomacies = value.Snapshot });
        }
    }

    public EntityListCursor<WorldEvent, WorldEventCursor> Events
    {
        get => field ??= new EntityListCursor<WorldEvent, WorldEventCursor>(Value.Events, value =>
        {
            if (!ReferenceEquals(Value.Events, value))
                ReplaceChanged(Value with { Events = value });
        }, value => new WorldEventCursor(value));
        set
        {
            field = null;
            Replace(Value with { Events = value.Snapshot });
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

    public int Year => 1 + (int)(Tick / 120);
    public int Day => 1 + (int)(Tick % 120);
    public int Population => Residents.Count;

    public WorldRules Rules
    {
        get => Value.Rules;
        set
        {
            if (!EqualityComparer<WorldRules>.Default.Equals(Value.Rules, value))
                ReplaceChanged(Value with { Rules = value });
        }
    }

    public EntityListCursor<LocalConflict, LocalConflictCursor> Conflicts
    {
        get => field ??= new EntityListCursor<LocalConflict, LocalConflictCursor>(Value.Conflicts, value =>
        {
            if (!ReferenceEquals(Value.Conflicts, value))
                ReplaceChanged(Value with { Conflicts = value });
        }, value => new LocalConflictCursor(value));
        set
        {
            field = null;
            Replace(Value with { Conflicts = value.Snapshot });
        }
    }

    public int SimulationVersion
    {
        get => Value.SimulationVersion;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(Value.SimulationVersion, value))
                ReplaceChanged(Value with { SimulationVersion = value });
        }
    }

    public SocietyStateCursor Society
    {
        get
        {
            if (_society is null)
            {
                _society = new SocietyStateCursor(Value.Society);
                _society.Bind(value =>
                {
                    if (!ReferenceEquals(Value.Society, value))
                        ReplaceChanged(Value with { Society = value });
                });
            }

            return _society;
        }
        set
        {
            _society = null;
            Replace(Value with { Society = value.Value });
        }
    }

    public SnapshotListCursor<PendingMessage> PendingMessages
    {
        get => field ??= new SnapshotListCursor<PendingMessage>(Value.PendingMessages, value =>
        {
            if (!ReferenceEquals(Value.PendingMessages, value))
                ReplaceChanged(Value with { PendingMessages = value });
        });
        set
        {
            field = null;
            Replace(Value with { PendingMessages = value.Snapshot });
        }
    }

    public EntityListCursor<Resident, ResidentCursor> ArchivedResidents
    {
        get => _archivedResidents ??= new EntityListCursor<Resident, ResidentCursor>(Value.ArchivedResidents, value =>
        {
            if (!ReferenceEquals(Value.ArchivedResidents, value))
                ReplaceChanged(Value with { ArchivedResidents = value });
        }, value => new ResidentCursor(value));
        set
        {
            _archivedResidents = null;
            Replace(Value with { ArchivedResidents = value.Snapshot });
        }
    }

    public static implicit operator WorldState(WorldStateCursor cursor)
    {
        return cursor.Snapshot;
    }

    public static implicit operator WorldStateCursor(WorldState value)
    {
        return new WorldStateCursor(value);
    }

    protected override void OnReplace(in WorldState before, in WorldState after)
    {
        if (_society is not null && !ReferenceEquals(_society.Value, after.Society))
            _society.Synchronize(after.Society);
    }
}
