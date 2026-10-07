using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

public sealed partial record Settlement
{
    /// <summary>当前自主发展计划的目标说明。</summary>
    public string DevelopmentGoal { get; init; } = "稳定粮食，准备发展";

    /// <summary>当前发展计划遇到的限制说明。</summary>
    public string DevelopmentBlocker { get; init; } = "等待当地居民议事";

    /// <summary>最近一次评估自主发展计划的模拟日序。</summary>
    public long LastDevelopmentTick { get; init; }

    /// <summary>根据已收到的困苦报告形成的地方不满程度。</summary>
    public double Unrest { get; init; }

    /// <summary>最近一次政治归属变化的模拟日序。</summary>
    public long LastPoliticalChangeTick { get; init; }

    /// <summary>已经取得的村、镇或城等级。</summary>
    [JsonRequired]
    public SettlementTier Tier { get; init; }

    /// <summary>本轮村镇城晋升已累计的施工进度。</summary>
    public double ExpansionProgress { get; init; }

    /// <summary>本轮村镇城晋升所需的总施工量，0 表示没有晋升项目。</summary>
    public double ExpansionRequired { get; init; }

    /// <summary>是否有正在进行的村镇城晋升项目。</summary>
    [JsonIgnore]
    public bool IsExpanding => ExpansionRequired > 0;
}
