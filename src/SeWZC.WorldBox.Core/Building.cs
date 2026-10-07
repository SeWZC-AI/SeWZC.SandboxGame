using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

/// <summary>世界中可建造并运营的设施。</summary>
public sealed partial record Building
{
    /// <summary>牧场或养殖厂实际饲养的动物物种。</summary>
    [JsonRequired]
    public WildlifeKind LivestockKind { get; init; }

    /// <summary>设施内实际饲养的动物数量。</summary>
    [JsonRequired]
    public double LivestockPopulation { get; init; }

    /// <summary>设施已经完成的生产批次数。</summary>
    [JsonRequired]
    public int ProductionBatches { get; init; }

    /// <summary>设施已经完成的服务次数。</summary>
    [JsonRequired]
    public int ServiceActions { get; init; }

    /// <summary>最近一次提供服务的模拟日序。</summary>
    [JsonRequired]
    public long LastServiceTick { get; init; } = -100;

    /// <summary>是否允许设施运营，关闭后保留建筑本身。</summary>
    [JsonRequired]
    public bool Enabled { get; init; } = true;

    /// <summary>本轮施工的进展观测记录。</summary>
    public ProjectObservation Observation { get; init; } = new();

    /// <summary>建筑的稳定 ID。</summary>
    public int Id { get; init; }

    /// <summary>建筑所属聚落的 ID。</summary>
    public int SettlementId { get; init; }

    /// <summary>设施类别。</summary>
    public BuildingKind Kind { get; init; }

    /// <summary>所在地点的横向地格坐标。</summary>
    public int X { get; init; }

    /// <summary>所在地点的纵向地格坐标。</summary>
    public int Y { get; init; }

    /// <summary>已经累计的建造施工量。</summary>
    public double ConstructionProgress { get; init; }

    /// <summary>完成建造所需的总施工量。</summary>
    public double ConstructionRequired { get; init; } = 30;

    /// <summary>当前建筑生命值，影响存续和运营。</summary>
    public double Health { get; init; } = 100;

    /// <summary>同时容纳的劳动岗位数。</summary>
    public int WorkSlots { get; init; } = 3;

    /// <summary>当前登记的工人居民 ID。</summary>
    public ImmutableList<int> Workers { get; init; } = [];

    /// <summary>最近一次居民在此劳动的模拟日序。</summary>
    public long LastWorkedTick { get; init; } = -100;

    /// <summary>建造进度是否达标且建筑仍有生命值。</summary>
    [JsonIgnore]
    public bool IsCompleted => ConstructionProgress >= ConstructionRequired && Health > 0;
}
