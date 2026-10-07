using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

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
            if (_improvement == value)
                return;
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
