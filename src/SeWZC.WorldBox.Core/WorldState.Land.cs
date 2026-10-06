using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

/// <summary>地格上的农田、山路或桥梁改良。</summary>
public enum LandImprovement
{
    /// <summary>无地块改良。</summary>
    None,
    /// <summary>农田。</summary>
    Farmland,
    /// <summary>山路。</summary>
    MountainPass,
    /// <summary>桥梁。</summary>
    Bridge,
}

/// <summary>居民步行、乘船或航空移动的方式。</summary>
public enum TravelMode
{
    /// <summary>步行。</summary>
    Foot,
    /// <summary>舟船。</summary>
    Boat,
    /// <summary>运输机。</summary>
    Aircraft,
}

public sealed partial class Tile
{
    private LandImprovement _improvement;

    /// <summary>当前地块改良，影响通行和资源生产。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public LandImprovement Improvement
    {
        get => _improvement;
        set
        {
            if (_improvement == value) return;
            _improvement = value;
            TerritoryCounts?.InvalidateTraversal();
        }
    }

    /// <summary>地下矿藏种类，空值表示没有矿藏。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ResourceKind? Deposit { get; set; }

    /// <summary>尚未开采的矿藏资源数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double DepositAmount { get; set; }

    /// <summary>观察者地图上的矿藏发现标记；居民仍须依据本地知识勘探。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool DepositDiscovered { get; set; }

    /// <summary>最近记录采收量的模拟日序。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public long LastHarvestTick { get; set; }

    /// <summary>最近记录的模拟日内，累计采收的资源数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Harvested { get; set; }
}

public sealed partial class Resident
{
    /// <summary>当前移动采用的交通方式。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public TravelMode TravelMode { get; set; }
}
