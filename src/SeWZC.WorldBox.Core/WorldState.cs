using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

/// <summary>地格的地形类别，决定通行、资源和生态规则。</summary>
public enum TerrainType
{
    /// <summary>深海。</summary>
    DeepWater = 0,
    /// <summary>浅海。</summary>
    Water = 1,
    /// <summary>沙地。</summary>
    Sand = 2,
    /// <summary>草地。</summary>
    Grass = 3,
    /// <summary>森林。</summary>
    Forest = 4,
    /// <summary>山地。</summary>
    Mountain = 5,
    /// <summary>雪地。</summary>
    Snow = 6,
    /// <summary>丘陵。</summary>
    Hills = 7,
    /// <summary>湿地。</summary>
    Wetland = 8,
    /// <summary>沙漠。</summary>
    Desert = 9,
    /// <summary>河流。</summary>
    River = 10,
    /// <summary>苔原。</summary>
    Tundra = 11,
    /// <summary>湖泊。</summary>
    Lake = 12,
    /// <summary>旱沃地。</summary>
    DryFertile = 13,
    /// <summary>小溪。</summary>
    Stream,
    /// <summary>大江。</summary>
    LargeRiver,
    /// <summary>草甸。</summary>
    Meadow,
    /// <summary>疏林。</summary>
    Woodland,
    /// <summary>雨林。</summary>
    Rainforest,
    /// <summary>稀树草原。</summary>
    Savanna,
    /// <summary>灌丛。</summary>
    Scrub,
    /// <summary>冲积平原。</summary>
    Floodplain,
    /// <summary>高山草甸。</summary>
    AlpineMeadow,
}

/// <summary>居民所属的种族。</summary>
public enum RaceKind
{
    /// <summary>人类。</summary>
    Human,
    /// <summary>精灵。</summary>
    Elf,
    /// <summary>矮人。</summary>
    Dwarf,
    /// <summary>兽人。</summary>
    Orc,
}

/// <summary>居民的职业分工，包括尚未成年的孩童。</summary>
public enum Profession
{
    /// <summary>孩童。</summary>
    Child,
    /// <summary>农民。</summary>
    Farmer,
    /// <summary>伐木工。</summary>
    Lumberjack,
    /// <summary>矿工。</summary>
    Miner,
    /// <summary>战士。</summary>
    Soldier,
    /// <summary>建造者。</summary>
    Builder,
    /// <summary>商人。</summary>
    Trader,
    /// <summary>信使。</summary>
    Messenger,
    /// <summary>代表。</summary>
    Representative,
    /// <summary>学者。</summary>
    Scholar,
    /// <summary>法师。</summary>
    Mage,
    /// <summary>渔民。</summary>
    Fisher,
    /// <summary>工程师。</summary>
    Engineer,
    /// <summary>医师。</summary>
    Physician,
    /// <summary>消防员。</summary>
    Firefighter,
    /// <summary>游击射手。</summary>
    Ranger,
    /// <summary>文献师。</summary>
    Archivist,
    /// <summary>战斗法师。</summary>
    Battlemage,
    /// <summary>测绘员。</summary>
    Surveyor,
    /// <summary>园艺师。</summary>
    Gardener,
}

/// <summary>居民当前身体状态或正在进行的活动。</summary>
public enum ResidentActivity
{
    /// <summary>漫游。</summary>
    Wandering,
    /// <summary>劳动。</summary>
    Working,
    /// <summary>饥饿。</summary>
    Hungry,
    /// <summary>行军。</summary>
    Marching,
    /// <summary>患病。</summary>
    Sick,
    /// <summary>进食。</summary>
    Eating,
    /// <summary>休息。</summary>
    Resting,
    /// <summary>交谈。</summary>
    Talking,
    /// <summary>递送。</summary>
    Delivering,
    /// <summary>学习。</summary>
    Studying,
    /// <summary>施法。</summary>
    Casting,
    /// <summary>逃离危险。</summary>
    Fleeing,
}

/// <summary>玩家可在局部施加的灾害类别。</summary>
public enum DisasterKind
{
    /// <summary>火灾。</summary>
    Fire,
    /// <summary>干旱。</summary>
    Drought,
    /// <summary>疫病。</summary>
    Plague,
    /// <summary>陨石。</summary>
    Meteor,
}

/// <summary>两国当前共同的中立、结盟或战争状态。</summary>
public enum DiplomaticStatus
{
    /// <summary>中立。</summary>
    Neutral,
    /// <summary>结盟。</summary>
    Allied,
    /// <summary>战争。</summary>
    War,
}

/// <summary>编年史记录所属的事件类别。</summary>
public enum WorldEventKind
{
    /// <summary>建国或定居。</summary>
    Founding,
    /// <summary>聚落成长。</summary>
    Growth,
    /// <summary>贸易。</summary>
    Trade,
    /// <summary>外交。</summary>
    Diplomacy,
    /// <summary>战争。</summary>
    War,
    /// <summary>灾害。</summary>
    Disaster,
    /// <summary>死亡。</summary>
    Death,
    /// <summary>玩家编辑。</summary>
    Editor,
    /// <summary>个人经历。</summary>
    Personal,
    /// <summary>信息通信。</summary>
    Communication,
    /// <summary>文化。</summary>
    Culture,
    /// <summary>政策。</summary>
    Policy,
    /// <summary>研究。</summary>
    Research,
    /// <summary>建设。</summary>
    Construction,
    /// <summary>魔法。</summary>
    Magic,
}

/// <summary>可序列化的世界事实、实体关系、时间和随机状态，用于保存与续演。</summary>
public sealed partial class WorldState
{
    /// <summary>存档数据结构版本，用于拒绝不兼容的格式。</summary>
    [JsonRequired]
    public int FormatVersion { get; set; } = 16;

    /// <summary>生成世界时使用的整数种子。</summary>
    public int Seed { get; set; }
    /// <summary>地图宽度，以地格为单位。</summary>
    public int Width { get; set; }
    /// <summary>地图高度，以地格为单位。</summary>
    public int Height { get; set; }

    /// <summary>已经推进的模拟日数；每个模拟年包含 120 日。</summary>
    public long Tick { get; set; }

    /// <summary>模拟随机数生成器的当前状态；载入后接续使用，不从种子重新开始。</summary>
    public uint RandomState { get; set; }

    /// <summary>下一次分配给实体、事件或信息记录的稳定 ID。</summary>
    public int NextId { get; set; } = 1;

    /// <summary>按行排列的地格数组，索引为 <c>y * Width + x</c>。</summary>
    public Tile[] Tiles { get; set; } = [];

    /// <summary>当前存活居民的状态集合。</summary>
    public List<Resident> Residents { get; set; } = [];
    /// <summary>世界中的聚落及其仓库状态。</summary>
    public List<Settlement> Settlements { get; set; } = [];
    /// <summary>世界中的国家状态集合。</summary>
    public List<Nation> Nations { get; set; } = [];
    /// <summary>仍在行动的军队状态集合。</summary>
    public List<Army> Armies { get; set; } = [];
    /// <summary>各国之间的外交关系记录。</summary>
    public List<DiplomaticRelation> Diplomacies { get; set; } = [];
    /// <summary>容量受限的世界编年史记录。</summary>
    public List<WorldEvent> Events { get; set; } = [];
    /// <summary>存档中的粮食运输记录集合。</summary>
    public List<TradeRoute> TradeRoutes { get; set; } = [];
    /// <summary>是否允许模拟自主产生自然灾害。</summary>
    public bool NaturalDisasters { get; set; } = true;

    /// <summary>当前模拟年，从 1 开始，每年 120 日。</summary>
    [JsonIgnore]
    public int Year => 1 + (int)(Tick / 120);

    /// <summary>当前模拟年内的日序，从 1 到 120。</summary>
    [JsonIgnore]
    public int Day => 1 + (int)(Tick % 120);

    /// <summary>当前存活居民的总数。</summary>
    [JsonIgnore]
    public int Population => Residents.Count;
}

/// <summary>一个地格的地形、资源、归属、生态及局部灾害状态。</summary>
public sealed partial class Tile
{
    private int _fireTicks;
    private int _nationId;
    private TerrainType _terrain;

    /// <summary>当前地形；改变时使通行及受影响的占领统计失效。</summary>
    public TerrainType Terrain
    {
        get => _terrain;
        set
        {
            if (_terrain == value) return;
            TerritoryCounts?.InvalidateTraversal();
            if (WorldEngine.IsWaterTerrain(_terrain) != WorldEngine.IsWaterTerrain(value))
                TerritoryCounts?.InvalidateClaims();
            _terrain = value;
            if (value is not (TerrainType.Stream or TerrainType.River or TerrainType.LargeRiver)) RiverWidth = 0;
        }
    }

    /// <summary>用于地形和水系生成的海拔记录；运行时移动与劳动遵循地形规则。</summary>
    public byte Elevation { get; set; }

    /// <summary>当前肥力，取值为 0 至 100。</summary>
    public byte Fertility { get; set; }

    [JsonIgnore]
    internal TerritoryCounts? TerritoryCounts { get; set; }

    /// <summary>拥有此地的国家 ID，0 表示无国家归属。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
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

    /// <summary>以此格为中心的聚落 ID，0 表示没有聚落中心。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int SettlementId { get; set; }

    /// <summary>火灾剩余模拟日数，0 表示未燃烧。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int FireTicks
    {
        get => _fireTicks;
        set
        {
            if (_fireTicks > 0 != value > 0) TerritoryCounts?.InvalidateTraversal();
            _fireTicks = value;
        }
    }

    /// <summary>干旱剩余模拟日数，0 表示未受干旱影响。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int DroughtTicks { get; set; }

    /// <summary>按基础步行规则判断地形和改良是否可通行，不含种族特例。</summary>
    [JsonIgnore]
    public bool IsWalkable => Terrain == TerrainType.Mountain
        ? Improvement == LandImprovement.MountainPass
        : Terrain is TerrainType.Water or TerrainType.River or TerrainType.LargeRiver or TerrainType.Lake
            ? Improvement == LandImprovement.Bridge
            : Terrain != TerrainType.DeepWater;
}

/// <summary>居民的身体状态、归属、随身资源、认知及已记录的经历。</summary>
public sealed partial class Resident
{
    /// <summary>居民的稳定 ID。</summary>
    public int Id { get; set; }
    /// <summary>居民的显示名称。</summary>
    public string Name { get; set; } = "";
    /// <summary>居民的种族。</summary>
    public RaceKind Race { get; set; }
    /// <summary>所在地点的横向地格坐标。</summary>
    public int X { get; set; }
    /// <summary>所在地点的纵向地格坐标。</summary>
    public int Y { get; set; }
    /// <summary>年龄，以模拟年为单位，允许小数。</summary>
    public double Age { get; set; }
    /// <summary>关联或归属国家的稳定 ID。</summary>
    public int NationId { get; set; }
    /// <summary>关联或归属聚落的稳定 ID。</summary>
    public int SettlementId { get; set; }
    /// <summary>当前职业分工。</summary>
    public Profession Profession { get; set; }
    /// <summary>当前活动或身体状态。</summary>
    public ResidentActivity Activity { get; set; }
    /// <summary>当前生命值，归零时居民死亡。</summary>
    public double Health { get; set; } = 100;
    /// <summary>饥饿程度，越高表示越缺粮。</summary>
    public double Hunger { get; set; }
    /// <summary>疫病剩余模拟日数，0 表示未患病。</summary>
    public int SicknessTicks { get; set; }
    /// <summary>所加入的军队 ID，0 表示未编入军队。</summary>
    public int ArmyId { get; set; }
    /// <summary>供档案显示的性格特征文字。</summary>
    public string Trait { get; set; } = "勤劳";
}

/// <summary>仓库库存、个人背包、成本及生产配方中的各类资源数量。</summary>
public sealed partial class ResourceStock
{
    // 省略零数量后，缺失字段必须还原为零，因此不能使用非零属性初值。
    /// <summary>粮食的资源数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Food { get; set; }

    /// <summary>木材的资源数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Wood { get; set; }

    /// <summary>石材的资源数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Stone { get; set; }

    /// <summary>矿石的资源数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Ore { get; set; }
}

/// <summary>具有实际中心、仓库、占领地块和本地已收到知识的聚落。</summary>
public sealed partial class Settlement
{
    /// <summary>聚落的稳定 ID。</summary>
    public int Id { get; set; }
    /// <summary>聚落的显示名称。</summary>
    public string Name { get; set; } = "";
    /// <summary>所在地点的横向地格坐标。</summary>
    public int X { get; set; }
    /// <summary>所在地点的纵向地格坐标。</summary>
    public int Y { get; set; }
    /// <summary>关联或归属国家的稳定 ID。</summary>
    public int NationId { get; set; }
    /// <summary>本聚落仓库中的实际资源库存。</summary>
    public ResourceStock Resources { get; set; } = new();
    /// <summary>归属本聚落的存活居民数量。</summary>
    public int Population { get; set; }
    /// <summary>本聚落的基础住房容量，不含已运营住宅的额外容量。</summary>
    public int Housing { get; set; } = 40;
    /// <summary>存档保留的基础等级字段，当前村镇城晋升使用 <c>Tier</c>。</summary>
    public int Level { get; set; } = 1;
}

/// <summary>关联聚落、首都、文化、制度与外交关系的国家状态。</summary>
public sealed partial class Nation
{
    /// <summary>国家的稳定 ID。</summary>
    public int Id { get; set; }
    /// <summary>国家的显示名称。</summary>
    public string Name { get; set; } = "";
    /// <summary>旗帜和地图归属颜色，使用 ARGB 编码。</summary>
    public uint ColorArgb { get; set; }
    /// <summary>建国时的创始种族，不限制后续居民种族。</summary>
    public RaceKind FoundingRace { get; set; }
    /// <summary>首都聚落的 ID。</summary>
    public int CapitalId { get; set; }
    /// <summary>所属聚落的存活居民总数。</summary>
    public int Population { get; set; }
    /// <summary>登记属于本国的地格总数。</summary>
    public int Territory { get; set; }
    /// <summary>古代工具发展等级，范围为 1 至 5，独立于聚落研究。</summary>
    public int Technology { get; set; } = 1;
    /// <summary>最近一次国家决策的说明文字。</summary>
    public string Decision { get; set; } = "积累粮食，建立家园";

    /// <summary>用于展示的聚落库存合计；实际资源保存在各聚落仓库中。</summary>
    public ResourceStock Resources { get; set; } = new();
}

/// <summary>军队的位置、补给、战役目标和最近收到的军令。</summary>
public sealed partial class Army
{
    /// <summary>军队的稳定 ID。</summary>
    public int Id { get; set; }
    /// <summary>关联或归属国家的稳定 ID。</summary>
    public int NationId { get; set; }
    /// <summary>当前军事目标国家的 ID。</summary>
    public int TargetNationId { get; set; }
    /// <summary>所在地点的横向地格坐标。</summary>
    public int X { get; set; }
    /// <summary>所在地点的纵向地格坐标。</summary>
    public int Y { get; set; }
    /// <summary>当前士兵数量。</summary>
    public int Soldiers { get; set; }
    /// <summary>当前士气。</summary>
    public double Morale { get; set; } = 100;
    /// <summary>军队实际携带的粮食补给。</summary>
    public double Supplies { get; set; }
    /// <summary>供界面显示的当前军队行动说明。</summary>
    public string Status { get; set; } = "集结";
}

/// <summary>两国共同的外交状态、各自对对方的态度及协商记录。</summary>
public sealed class DiplomaticRelation
{
    /// <summary>最近一次外交状态变化的模拟日序。</summary>
    public long LastChangedTick { get; set; }
    /// <summary>最近一次实际接触的模拟日序。</summary>
    public long LastContactTick { get; set; }

    /// <summary>第一国首次进入持续敌意的日序，0 表示尚未开始。</summary>
    [JsonRequired]
    public long FirstEscalationTick { get; set; }

    /// <summary>第二国首次进入持续敌意的日序，0 表示尚未开始。</summary>
    [JsonRequired]
    public long SecondEscalationTick { get; set; }

    /// <summary>最近一次评估外交关系的模拟日序。</summary>
    public long LastEvaluatedTick { get; set; }
    /// <summary>最近一次外交变化关联的事件 ID。</summary>
    public int LastEventId { get; set; }
    /// <summary>当前发起结盟提议的国家 ID。</summary>
    public int AllianceOfferNationId { get; set; }
    /// <summary>当前结盟提议发起的模拟日序。</summary>
    public long AllianceOfferTick { get; set; }
    /// <summary>当前外交关系或协商决定的理由。</summary>
    public string Reason { get; set; } = "等待实际接触与递送的消息";
    /// <summary>关系中的第一国 ID。</summary>
    public int FirstNationId { get; set; }
    /// <summary>关系中的第二国 ID。</summary>
    public int SecondNationId { get; set; }
    /// <summary>两国共同的外交状态。</summary>
    public DiplomaticStatus Status { get; set; }

    /// <summary>第一国对第二国的态度分值。</summary>
    [JsonRequired]
    public int FirstOpinion { get; set; }

    /// <summary>第二国对第一国的态度分值。</summary>
    [JsonRequired]
    public int SecondOpinion { get; set; }

    /// <summary>双方态度的舍入均值，用于展示。</summary>
    public int Opinion { get; set; }
}

/// <summary>存档保留的粮食运输记录；当前居民贸易使用随身货物和行动目标。</summary>
public sealed class TradeRoute
{
    /// <summary>粮食运输记录的出发聚落 ID。</summary>
    public int FromSettlementId { get; set; }
    /// <summary>粮食运输记录的目的聚落 ID。</summary>
    public int ToSettlementId { get; set; }
    /// <summary>记录中的全程运输模拟日数。</summary>
    public int TravelTicks { get; set; }
    /// <summary>记录中的剩余运输模拟日数。</summary>
    public int RemainingTicks { get; set; }
    /// <summary>记录中的粮食货物数量。</summary>
    public double FoodCargo { get; set; }
}

/// <summary>世界编年史中的事件记录，关联主体、位置和已记录的前因。</summary>
public sealed partial class WorldEvent
{
    /// <summary>事件发生的模拟日序。</summary>
    public long Tick { get; set; }
    /// <summary>事件所属类别。</summary>
    public WorldEventKind Kind { get; set; }
    /// <summary>编年史中显示的事件描述。</summary>
    public string Message { get; set; } = "";
    /// <summary>事件地点的横向地格坐标，-1 表示没有具体地点。</summary>
    public int X { get; set; } = -1;
    /// <summary>事件地点的纵向地格坐标，-1 表示没有具体地点。</summary>
    public int Y { get; set; } = -1;
}
