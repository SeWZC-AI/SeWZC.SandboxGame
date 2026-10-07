using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

/// <summary>按资源类别记录的不可变库存。</summary>
public readonly partial record struct ResourceStock
{
    /// <summary>饮水数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Water { get; init; }

    /// <summary>合金数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Alloy { get; init; }

    /// <summary>动力单元数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double EnergyCells { get; init; }

    /// <summary>魔晶数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Crystals { get; init; }

    /// <summary>煤数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Coal { get; init; }

    /// <summary>石油数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Oil { get; init; }

    /// <summary>稀土数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double RareEarth { get; init; }

    /// <summary>舟船数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Boats { get; init; }

    /// <summary>运输机数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Aircraft { get; init; }

    /// <summary>工具数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Tools { get; init; }

    /// <summary>药品数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Medicine { get; init; }

    /// <summary>弹药数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Ammunition { get; init; }

    // 省略零数量后，缺失字段必须还原为零，因此不能使用非零属性初值。
    /// <summary>粮食数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Food { get; init; }

    /// <summary>木材数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Wood { get; init; }

    /// <summary>石材数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Stone { get; init; }

    /// <summary>矿石数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Ore { get; init; }
}
