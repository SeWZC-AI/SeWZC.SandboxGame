using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

public sealed partial record Resident
{
    /// <summary>分配给居民的住宅编号；零表示尚无住所。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int HomeBuildingId { get; init; }

    /// <summary>是否已进入分配的住宅。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsInsideHome { get; init; }

    /// <summary>正在背负本人的居民编号；零表示未被背负。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int CarriedByResidentId { get; init; }

    /// <summary>是否经救助送入住宅卧床恢复，醒来后清除。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool BedRestAfterRescue { get; init; }
}
