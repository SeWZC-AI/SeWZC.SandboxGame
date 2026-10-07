using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

/// <summary>由士兵组成、接受军令的作战单位。</summary>
public sealed partial record Army
{
    /// <summary>军队的稳定 ID。</summary>
    public int Id { get; init; }

    /// <summary>军队所属国家的 ID。</summary>
    public int NationId { get; init; }

    /// <summary>当前军事目标国家的 ID。</summary>
    public int TargetNationId { get; init; }

    /// <summary>所在地点的横向地格坐标。</summary>
    public int X { get; init; }

    /// <summary>所在地点的纵向地格坐标。</summary>
    public int Y { get; init; }

    /// <summary>当前士兵数量。</summary>
    public int Soldiers { get; init; }

    /// <summary>当前士气。</summary>
    public double Morale { get; init; } = 100;

    /// <summary>军队实际携带的粮食补给。</summary>
    public double Supplies { get; init; }

    /// <summary>供界面显示的当前军队行动说明。</summary>
    public string Status { get; init; } = "集结";

    /// <summary>军队实际携带的饮水补给。</summary>
    [JsonRequired]
    public double WaterSupplies { get; init; }
}
