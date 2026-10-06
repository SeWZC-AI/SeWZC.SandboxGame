using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

/// <summary>可序列化的世界事实、实体关系、时间和随机状态，用于保存与续演。</summary>
public sealed class WorldState
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

    /// <summary>当前世界采用的模拟规则。</summary>
    public WorldRules Rules { get; set; } = new();

    /// <summary>当前保留的局部资源冲突记录。</summary>
    [JsonRequired]
    public List<LocalConflict> Conflicts { get; set; } = [];

    /// <summary>模拟规则版本，用于校验存档的续演兼容性。</summary>
    [JsonRequired]
    public int SimulationVersion { get; set; } = 15;

    /// <summary>文化、设施、研究、制度及已收到报告的状态。</summary>
    public SocietyState Society { get; set; } = new();

    /// <summary>等待送达的消息集合。</summary>
    public List<PendingMessage> PendingMessages { get; set; } = [];

    /// <summary>已死亡居民的有限归档，供查看经历。</summary>
    public List<Resident> ArchivedResidents { get; set; } = [];
}
