using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

public enum TerrainType { DeepWater = 0, Water = 1, Sand = 2, Grass = 3, Forest = 4, Mountain = 5, Snow = 6, Hills = 7, Wetland = 8, Desert = 9, River = 10, Tundra = 11, Lake = 12, DryFertile = 13,
    Stream, LargeRiver, Meadow, Woodland, Rainforest, Savanna, Scrub, Floodplain, AlpineMeadow }
public enum RaceKind { Human, Elf, Dwarf, Orc }
public enum Profession { Child, Farmer, Lumberjack, Miner, Soldier, Builder, Trader, Messenger, Representative, Scholar, Mage, Fisher,
    Engineer, Physician, Firefighter, Ranger, Archivist, Battlemage, Surveyor, Gardener }
public enum ResidentActivity { Wandering, Working, Hungry, Marching, Sick, Eating, Resting, Talking, Delivering, Studying, Casting, Fleeing }
public enum DisasterKind { Fire, Drought, Plague, Meteor }
public enum DiplomaticStatus { Neutral, Allied, War }
public enum WorldEventKind { Founding, Growth, Trade, Diplomacy, War, Disaster, Death, Editor, Personal, Communication, Culture, Policy, Research, Construction, Magic }

/// <summary>可序列化的世界事实、实体关系、时间和随机状态，用于保存与续演。</summary>
public sealed partial class WorldState
{
    [JsonRequired] public int FormatVersion { get; set; } = 16;
    public int Seed { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    /// <summary>已经推进的模拟日数；每个模拟年包含 120 日。</summary>
    public long Tick { get; set; }
    /// <summary>模拟随机数生成器的当前状态；载入后接续使用，不从种子重新开始。</summary>
    public uint RandomState { get; set; }
    public int NextId { get; set; } = 1;
    /// <summary>按行排列的地格数组，索引为 <c>y * Width + x</c>。</summary>
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

/// <summary>一个地格的地形、资源、归属、生态及局部灾害状态。</summary>
public sealed partial class Tile
{
    private TerrainType _terrain;
    public TerrainType Terrain
    {
        get => _terrain;
        set
        {
            if (_terrain == value) return;
            TerritoryCounts?.InvalidateTraversal();
            if (WorldEngine.IsWaterTerrain(_terrain) != WorldEngine.IsWaterTerrain(value)) TerritoryCounts?.InvalidateClaims();
            _terrain = value;
            if (value is not (TerrainType.Stream or TerrainType.River or TerrainType.LargeRiver)) RiverWidth = 0;
        }
    }
    /// <summary>用于地形和水系生成的海拔记录；运行时移动与劳动遵循地形规则。</summary>
    public byte Elevation { get; set; }
    public byte Fertility { get; set; }
    private int _nationId;
    [JsonIgnore] internal TerritoryCounts? TerritoryCounts { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int NationId
    {
        get => _nationId;
        set
        {
            if (_nationId == value) return;
            TerritoryCounts?.Change(_nationId, value);
            _nationId = value;
        }
    }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int SettlementId { get; set; }
    private int _fireTicks;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int FireTicks
    {
        get => _fireTicks;
        set
        {
            if ((_fireTicks > 0) != (value > 0)) TerritoryCounts?.InvalidateTraversal();
            _fireTicks = value;
        }
    }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int DroughtTicks { get; set; }
    [JsonIgnore] public bool IsWalkable => (Terrain == TerrainType.Mountain ? Improvement == LandImprovement.MountainPass
        : Terrain is TerrainType.Water or TerrainType.River or TerrainType.LargeRiver or TerrainType.Lake ? Improvement == LandImprovement.Bridge
        : Terrain != TerrainType.DeepWater);
}

/// <summary>居民的身体状态、归属、随身资源、认知及已记录的经历。</summary>
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

/// <summary>仓库库存、个人背包、成本及生产配方中的各类资源数量。</summary>
public sealed partial class ResourceStock
{
    // Format 16: missing amounts mean exactly zero, with no nonzero initializer.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public double Food { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public double Wood { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public double Stone { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public double Ore { get; set; }
}

/// <summary>具有实际中心、仓库、占领地块和本地已收到知识的聚落。</summary>
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

/// <summary>关联聚落、首都、文化、制度与外交关系的国家状态。</summary>
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
    /// <summary>用于展示的聚落库存合计；实际资源保存在各聚落仓库中。</summary>
    public ResourceStock Resources { get; set; } = new();
}

/// <summary>军队的位置、补给、战役目标和最近收到的军令。</summary>
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

/// <summary>两国共同的外交状态、各自对对方的态度及协商记录。</summary>
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
    /// <summary>双方态度的舍入均值，用于展示。</summary>
    public int Opinion { get; set; }
}

/// <summary>存档保留的粮食运输记录；当前居民贸易使用随身货物和行动目标。</summary>
public sealed class TradeRoute
{
    public int FromSettlementId { get; set; }
    public int ToSettlementId { get; set; }
    public int TravelTicks { get; set; }
    public int RemainingTicks { get; set; }
    public double FoodCargo { get; set; }
}

/// <summary>世界编年史中的事件记录，关联主体、位置和已记录的前因。</summary>
public sealed partial class WorldEvent
{
    public long Tick { get; set; }
    public WorldEventKind Kind { get; set; }
    public string Message { get; set; } = "";
    public int X { get; set; } = -1;
    public int Y { get; set; } = -1;
}
