using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

public sealed partial class Building
{
    /// <summary>规划此设施用途的理由。</summary>
    public string PlanningReason { get; set; } = "";
    /// <summary>选择此地建造的理由。</summary>
    public string SiteReason { get; set; } = "";

    /// <summary>设施等级，独立于聚落的村镇城等级。</summary>
    [JsonRequired]
    public int Level { get; set; } = 1;

    /// <summary>本轮升级或改向已累计的施工量。</summary>
    public double UpgradeProgress { get; set; }
    /// <summary>本轮升级或改向所需的总施工量，0 表示没有项目。</summary>
    public double UpgradeRequired { get; set; }
    /// <summary>桥梁当前允许通行的轴向。</summary>
    public BridgeDirection Direction { get; set; }
    /// <summary>改造完成后采用的桥梁轴向，空值表示未安排改向。</summary>
    public BridgeDirection? PendingDirection { get; set; }

    /// <summary>是否有正在进行的升级或改向项目。</summary>
    [JsonIgnore]
    public bool IsUpgrading => UpgradeRequired > 0;

    /// <summary>由设施等级计算的劳动效率倍率。</summary>
    [JsonIgnore]
    public double Efficiency => 1 + (Level - 1) * .25;
}
