using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

public enum TerrainType { DeepWater = 0, Water = 1, Sand = 2, Grass = 3, Forest = 4, Mountain = 5, Snow = 6, Hills = 7, Wetland = 8, Desert = 9, River = 10, Tundra = 11 }
public enum RaceKind { Human, Elf, Dwarf, Orc }
public enum Profession { Child, Farmer, Lumberjack, Miner, Soldier, Builder, Trader, Messenger, Representative, Scholar, Mage }
public enum ResidentActivity { Wandering, Working, Hungry, Marching, Sick, Eating, Resting, Talking, Delivering, Studying, Casting, Fleeing }
public enum DisasterKind { Fire, Drought, Plague, Meteor }
public enum DiplomaticStatus { Neutral, Allied, War }
public enum WorldEventKind { Founding, Growth, Trade, Diplomacy, War, Disaster, Death, Editor, Personal, Communication, Culture, Policy, Research, Construction, Magic }

public sealed partial class WorldState
{
    [JsonRequired] public int FormatVersion { get; set; } = 8;
    public int Seed { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public long Tick { get; set; }
    public uint RandomState { get; set; }
    public int NextId { get; set; } = 1;
    public Tile[] Tiles { get; set; } = [];
    public List<Resident> Residents { get; set; } = [];
    public List<Settlement> Settlements { get; set; } = [];
    public List<Nation> Nations { get; set; } = [];
    public List<Army> Armies { get; set; } = [];
    public List<DiplomaticRelation> Diplomacies { get; set; } = [];
    public List<WorldEvent> Events { get; set; } = [];
    public List<TradeRoute> TradeRoutes { get; set; } = [];
    public bool NaturalDisasters { get; set; } = true;
    [JsonIgnore] public int Year => 1 + (int)(Tick / 120);
    [JsonIgnore] public int Day => 1 + (int)(Tick % 120);
    [JsonIgnore] public int Population => Residents.Count;
}

public sealed partial class Tile
{
    public TerrainType Terrain { get; set; }
    public byte Elevation { get; set; }
    public byte Fertility { get; set; }
    private int _nationId;
    [JsonIgnore] internal TerritoryCounts? TerritoryCounts { get; set; }
    public int NationId
    {
        get => _nationId;
        set
        {
            if (_nationId == value) return;
            TerritoryCounts?.Change(_nationId, value);
            _nationId = value;
        }
    }
    public int SettlementId { get; set; }
    public int FireTicks { get; set; }
    public int DroughtTicks { get; set; }
    [JsonIgnore] public bool IsWalkable => (Terrain == TerrainType.Mountain ? Improvement == LandImprovement.MountainPass
        : Terrain is TerrainType.Water or TerrainType.River ? Improvement == LandImprovement.Bridge
        : Terrain != TerrainType.DeepWater);
}

public sealed partial class Resident
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public RaceKind Race { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public double Age { get; set; }
    public int NationId { get; set; }
    public int SettlementId { get; set; }
    public Profession Profession { get; set; }
    public ResidentActivity Activity { get; set; }
    public double Health { get; set; } = 100;
    public double Hunger { get; set; }
    public int SicknessTicks { get; set; }
    public int ArmyId { get; set; }
    public string Trait { get; set; } = "勤劳";
}

public sealed partial class ResourceStock
{
    public double Food { get; set; }
    public double Wood { get; set; }
    public double Stone { get; set; }
    public double Ore { get; set; }
}

public sealed partial class Settlement
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
    public int NationId { get; set; }
    public ResourceStock Resources { get; set; } = new();
    public int Population { get; set; }
    public int Housing { get; set; } = 40;
    public int Level { get; set; } = 1;
}

public sealed partial class Nation
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public uint ColorArgb { get; set; }
    public RaceKind FoundingRace { get; set; }
    public int CapitalId { get; set; }
    public int Population { get; set; }
    public int Territory { get; set; }
    public int Technology { get; set; } = 1;
    public string Decision { get; set; } = "积累粮食，建立家园";
    // A derived display total; settlement stocks are authoritative.
    public ResourceStock Resources { get; set; } = new();
}

public sealed partial class Army
{
    public int Id { get; set; }
    public int NationId { get; set; }
    public int TargetNationId { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Soldiers { get; set; }
    public double Morale { get; set; } = 100;
    public double Supplies { get; set; }
    public string Status { get; set; } = "集结";
}

public sealed class DiplomaticRelation
{
    public long LastChangedTick { get; set; }
    public long LastContactTick { get; set; }
    [JsonRequired] public long FirstEscalationTick { get; set; }
    [JsonRequired] public long SecondEscalationTick { get; set; }
    public long LastEvaluatedTick { get; set; }
    public int LastEventId { get; set; }
    public int AllianceOfferNationId { get; set; }
    public long AllianceOfferTick { get; set; }
    public string Reason { get; set; } = "等待实际接触与递送的消息";
    public int FirstNationId { get; set; }
    public int SecondNationId { get; set; }
    public DiplomaticStatus Status { get; set; }
    [JsonRequired] public int FirstOpinion { get; set; }
    [JsonRequired] public int SecondOpinion { get; set; }
    public int Opinion { get; set; }
}

public sealed class TradeRoute
{
    public int FromSettlementId { get; set; }
    public int ToSettlementId { get; set; }
    public int TravelTicks { get; set; }
    public int RemainingTicks { get; set; }
    public double FoodCargo { get; set; }
}

public sealed partial class WorldEvent
{
    public long Tick { get; set; }
    public WorldEventKind Kind { get; set; }
    public string Message { get; set; } = "";
    public int X { get; set; } = -1;
    public int Y { get; set; } = -1;
}
