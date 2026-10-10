using System.Collections.Immutable;
using SeWZC.WorldBox.Core.Runtime;

namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    private WorldState _state;
    private EntityStore<Tile>? _tileStates;
    private EntityStore<Resident>? _residentStates, _archivedResidentStates;
    private EntityStore<Settlement>? _settlementStates;
    private EntityStore<Nation>? _nationStates;
    private EntityStore<Army>? _armyStates;
    private EntityStore<Building>? _buildingStates;

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
        var buildings = _buildingStates?.CaptureSnapshot() ?? _state.Society.Buildings;
        var society = ReferenceEquals(_state.Society.Buildings, buildings)
            ? _state.Society : _state.Society with { Buildings = buildings };
        if (_state.Tick == SimulationTick && _state.RandomState == RandomState && _state.NextId == NextId
            && ReferenceEquals(_state.Tiles, tiles) && ReferenceEquals(_state.Residents, residents)
            && ReferenceEquals(_state.ArchivedResidents, archived) && ReferenceEquals(_state.Settlements, settlements)
            && ReferenceEquals(_state.Nations, nations) && ReferenceEquals(_state.Armies, armies)
            && ReferenceEquals(_state.Society, society))
            return _state;

        _state = _state with
        {
            Tick = SimulationTick, RandomState = RandomState, NextId = NextId,
            Tiles = tiles, Residents = residents, ArchivedResidents = archived,
            Settlements = settlements, Nations = nations, Armies = armies, Society = society,
        };
        return _state;
    }

    // 集合只维护定位索引和自身的持久化树，捕获时统一组合世界。
    internal EntityStore<Tile> Tiles => _tileStates ??= new(_state.Tiles);
    internal EntityStore<Resident> Residents => _residentStates ??= new(_state.Residents,
        static (before, after) => before.SettlementId != after.SettlementId);
    internal EntityStore<Resident> ArchivedResidents => _archivedResidentStates ??= new(_state.ArchivedResidents,
        static (before, after) => before.SettlementId != after.SettlementId);
    internal EntityStore<Settlement> Settlements => _settlementStates ??= new(_state.Settlements);
    internal EntityStore<Nation> Nations => _nationStates ??= new(_state.Nations);
    internal EntityStore<Army> Armies => _armyStates ??= new(_state.Armies);
    internal EntityStore<Building> Buildings => _buildingStates ??= new(Society.Buildings);

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
