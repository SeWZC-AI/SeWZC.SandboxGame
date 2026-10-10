using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

/// <summary>居民依据当前需求与熟悉任务预先安排的当日作息。</summary>
public sealed record ResidentDailyPlan
{
    /// <summary>制定安排时的模拟刻序。</summary>
    public long PlannedTick { get; init; }
    /// <summary>预计开始返回住宅的模拟刻序。</summary>
    public long ReturnHomeTick { get; init; }
    /// <summary>预计粮食储备需要补充的模拟刻序。</summary>
    public long FoodReviewTick { get; init; }
    /// <summary>预计饮水储备需要补充的模拟刻序。</summary>
    public long WaterReviewTick { get; init; }
    /// <summary>预计需要恢复体力或补觉的模拟刻序。</summary>
    public long RestReviewTick { get; init; }
    /// <summary>最近一次适合重复的日常劳动目标。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AgentGoal? WorkGoal { get; init; }
}
