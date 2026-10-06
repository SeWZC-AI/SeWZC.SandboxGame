using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

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
