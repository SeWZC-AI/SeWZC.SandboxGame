using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

/// <summary>可保存并恢复续演的不可变世界状态；旧状态可作为独立快照保留。</summary>
public readonly record struct WorldState
{
    /// <summary>建立采用当前存档格式和缺省规则的世界状态。</summary>
    public WorldState() { }

    /// <summary>存档数据结构版本，用于拒绝不兼容的格式。</summary>
    [JsonRequired]
    public int FormatVersion { get; init; } = 19;

    /// <summary>生成世界时使用的整数种子。</summary>
    public int Seed { get; init; }

    /// <summary>地图宽度，以地格为单位。</summary>
    public required int Width { get; init; }

    /// <summary>地图高度，以地格为单位。</summary>
    public required int Height { get; init; }

    /// <summary>已经推进的模拟日数；每个模拟年包含 120 日。</summary>
    public long Tick { get; init; }

    /// <summary>模拟随机数生成器的当前状态；载入后接续使用，不从种子重新开始。</summary>
    public uint RandomState { get; init; }

    /// <summary>下一次分配给实体、事件或信息记录的稳定 ID。</summary>
    public int NextId { get; init; } = 1;

    /// <summary>按行排列的地格集合，索引为 <c>y * Width + x</c>。</summary>
    [JsonConverter(typeof(ImmutableVectorJsonConverter<Tile>))]
    public required ImmutableVector<Tile> Tiles { get; init; }

    /// <summary>当前存活居民的状态集合。</summary>
    [JsonConverter(typeof(ImmutableVectorJsonConverter<Resident>))]
    public ImmutableVector<Resident> Residents { get; init; } = [];

    /// <summary>世界中的聚落及其仓库状态。</summary>
    [JsonConverter(typeof(ImmutableVectorJsonConverter<Settlement>))]
    public ImmutableVector<Settlement> Settlements { get; init; } = [];

    /// <summary>世界中的国家状态集合。</summary>
    [JsonConverter(typeof(ImmutableVectorJsonConverter<Nation>))]
    public ImmutableVector<Nation> Nations { get; init; } = [];

    /// <summary>仍在行动的军队状态集合。</summary>
    [JsonConverter(typeof(ImmutableVectorJsonConverter<Army>))]
    public ImmutableVector<Army> Armies { get; init; } = [];

    /// <summary>各国之间的外交关系记录。</summary>
    [JsonConverter(typeof(ImmutableVectorJsonConverter<DiplomaticRelation>))]
    public ImmutableVector<DiplomaticRelation> Diplomacies { get; init; } = [];

    /// <summary>容量受限的世界编年史记录。</summary>
    [JsonConverter(typeof(ImmutableVectorJsonConverter<WorldEvent>))]
    public ImmutableVector<WorldEvent> Events { get; init; } = [];

    /// <summary>是否允许模拟自主产生自然灾害。</summary>
    public bool NaturalDisasters { get; init; } = true;

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
    public WorldRules Rules { get; init; } = new();

    /// <summary>当前保留的局部资源冲突记录。</summary>
    [JsonRequired]
    [JsonConverter(typeof(ImmutableVectorJsonConverter<LocalConflict>))]
    public ImmutableVector<LocalConflict> Conflicts { get; init; } = [];

    /// <summary>模拟规则版本，用于校验存档的续演兼容性。</summary>
    [JsonRequired]
    public int SimulationVersion { get; init; } = 18;

    /// <summary>世界的社会发展状态。</summary>
    public SocietyState Society { get; init; } = new();

    /// <summary>等待送达的消息集合。</summary>
    public ImmutableList<PendingMessage> PendingMessages { get; init; } = [];

    /// <summary>已死亡居民的有限归档，供查看经历。</summary>
    [JsonConverter(typeof(ImmutableVectorJsonConverter<Resident>))]
    public ImmutableVector<Resident> ArchivedResidents { get; init; } = [];
}
