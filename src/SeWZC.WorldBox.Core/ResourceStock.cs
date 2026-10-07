using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

/// <summary>可原地修改的一组资源数量。</summary>
public sealed partial class ResourceStock
{
    // 省略零数量后，缺失字段必须还原为零，因此不能使用非零属性初值。
    /// <summary>粮食数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Food { get; set; }

    /// <summary>木材数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Wood { get; set; }

    /// <summary>石材数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Stone { get; set; }

    /// <summary>矿石数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Ore { get; set; }
}
