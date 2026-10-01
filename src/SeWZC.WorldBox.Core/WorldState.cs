using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

public enum TerrainType { DeepWater, Water, Sand, Grass, Forest, Mountain, Snow }
public enum RaceKind { Human, Elf, Dwarf, Orc }
public enum Profession { Child, Farmer, Lumberjack, Miner, Soldier, Builder }
public enum ResidentActivity { Wandering, Working, Hungry, Marching, Sick }
public enum DisasterKind { Fire, Drought, Plague }
public enum DiplomaticStatus { Neutral, Allied, War }
public enum WorldEventKind { Founding, Growth, Trade, Diplomacy, War, Disaster, Death, Editor }

public sealed class WorldState
{
    public int FormatVersion { get; set; } = 1;
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

public sealed class Tile
{
    public TerrainType Terrain { get; set; }
    public byte Elevation { get; set; }
    public byte Fertility { get; set; }
    public int NationId { get; set; }
    public int SettlementId { get; set; }
    public int FireTicks { get; set; }
    public int DroughtTicks { get; set; }
    [JsonIgnore] public bool IsWalkable => Terrain is not TerrainType.DeepWater and not TerrainType.Water and not TerrainType.Mountain;
}

public sealed class Resident
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

public sealed class ResourceStock
{
    public double Food { get; set; }
    public double Wood { get; set; }
    public double Stone { get; set; }
    public double Ore { get; set; }
}

public sealed class Settlement
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

public sealed class Nation
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

public sealed class Army
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
    public int FirstNationId { get; set; }
    public int SecondNationId { get; set; }
    public DiplomaticStatus Status { get; set; }
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

public sealed class WorldEvent
{
    public long Tick { get; set; }
    public WorldEventKind Kind { get; set; }
    public string Message { get; set; } = "";
    public int X { get; set; } = -1;
    public int Y { get; set; } = -1;
}
