using System.Collections.Immutable;
using SeWZC.WorldBox.Core.Runtime;

namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    private WorldState _state;
    private EntityListCursor<Tile, StateReference<Tile>>? _tileStates;
    private EntityListCursor<Resident, ResidentCursor>? _residentStates, _archivedResidentStates;
    private EntityListCursor<Settlement, StateReference<Settlement>>? _settlementStates;
    private EntityListCursor<Nation, StateReference<Nation>>? _nationStates;
    private EntityListCursor<Army, StateReference<Army>>? _armyStates;
    private EntityListCursor<Building, StateReference<Building>>? _buildingStates;

    // 时间、随机序列和编号只有引擎中的当前值；不可变世界在捕获时一次组合。
    internal long SimulationTick { get; set; }
    internal uint RandomState { get; set; }
    internal int NextId { get; set; }
    internal int Seed => _state.Seed;
    internal int Width => _state.Width;
    internal int Height => _state.Height;
    internal int Population => Residents.Count;
    internal WorldRules Rules => _state.Rules;

    private WorldState CaptureSnapshot()
    {
        var tiles = _tileStates?.CaptureSnapshot() ?? _state.Tiles;
        var residents = _residentStates?.CaptureSnapshot() ?? _state.Residents;
        var archived = _archivedResidentStates?.CaptureSnapshot() ?? _state.ArchivedResidents;
        var settlements = _settlementStates?.CaptureSnapshot() ?? _state.Settlements;
        var nations = _nationStates?.CaptureSnapshot() ?? _state.Nations;
        var armies = _armyStates?.CaptureSnapshot() ?? _state.Armies;
        _buildingStates?.FlushUpdates();
        if (_state.Tick == SimulationTick && _state.RandomState == RandomState && _state.NextId == NextId
            && ReferenceEquals(_state.Tiles, tiles) && ReferenceEquals(_state.Residents, residents)
            && ReferenceEquals(_state.ArchivedResidents, archived) && ReferenceEquals(_state.Settlements, settlements)
            && ReferenceEquals(_state.Nations, nations) && ReferenceEquals(_state.Armies, armies))
            return _state;

        _state = _state with
        {
            Tick = SimulationTick, RandomState = RandomState, NextId = NextId,
            Tiles = tiles, Residents = residents, ArchivedResidents = archived,
            Settlements = settlements, Nations = nations, Armies = armies,
        };
        return _state;
    }

    // 集合仅维护自身的持久化树，不在每个批次结束时复制世界根。
    internal EntityListCursor<Tile, StateReference<Tile>> Tiles => _tileStates ??= new(_state.Tiles,
        null, static value => new StateReference<Tile>(value));

    internal EntityListCursor<Resident, ResidentCursor> Residents => _residentStates ??= new(_state.Residents,
        null, static value => new ResidentCursor(value),
        static (before, after) => before.SettlementId != after.SettlementId);

    internal EntityListCursor<Resident, ResidentCursor> ArchivedResidents =>
        _archivedResidentStates ??= new(_state.ArchivedResidents,
            null, static value => new ResidentCursor(value),
            static (before, after) => before.SettlementId != after.SettlementId);

    internal EntityListCursor<Settlement, StateReference<Settlement>> Settlements =>
        _settlementStates ??= new(_state.Settlements, null,
            static value => new StateReference<Settlement>(value));

    internal EntityListCursor<Nation, StateReference<Nation>> Nations => _nationStates ??= new(_state.Nations,
        null, static value => new StateReference<Nation>(value));

    internal EntityListCursor<Army, StateReference<Army>> Armies => _armyStates ??= new(_state.Armies,
        null, static value => new StateReference<Army>(value));

    internal EntityListCursor<Building, StateReference<Building>> Buildings =>
        _buildingStates ??= new(Society.Buildings, value => Society = Society with { Buildings = value },
            static value => new StateReference<Building>(value));

    internal ImmutableVector<DiplomaticRelation> Diplomacies
    {
        get => _state.Diplomacies;
        set
        {
            if (!ReferenceEquals(_state.Diplomacies, value))
                _state = _state with { Diplomacies = value };
        }
    }

    internal ImmutableVector<WorldEvent> Events
    {
        get => _state.Events;
        set
        {
            if (!ReferenceEquals(_state.Events, value))
                _state = _state with { Events = value };
        }
    }

    internal bool NaturalDisasters
    {
        get => _state.NaturalDisasters;
        set
        {
            if (_state.NaturalDisasters != value)
                _state = _state with { NaturalDisasters = value };
        }
    }

    internal ImmutableVector<LocalConflict> Conflicts
    {
        get => _state.Conflicts;
        set
        {
            if (!ReferenceEquals(_state.Conflicts, value))
                _state = _state with { Conflicts = value };
        }
    }

    internal SocietyState Society
    {
        get => _state.Society;
        set
        {
            if (!ReferenceEquals(_state.Society, value))
                _state = _state with { Society = value };
        }
    }

    internal ImmutableList<PendingMessage> PendingMessages
    {
        get => _state.PendingMessages;
        set
        {
            if (!ReferenceEquals(_state.PendingMessages, value))
                _state = _state with { PendingMessages = value };
        }
    }
}
