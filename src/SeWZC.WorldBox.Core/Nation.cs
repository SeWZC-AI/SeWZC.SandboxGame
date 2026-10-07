namespace SeWZC.WorldBox.Core;

/// <summary>以聚落为成员的政治实体。</summary>
public sealed class Nation
{
    /// <summary>国家的稳定 ID。</summary>
    public int Id { get; set; }

    /// <summary>国家的显示名称。</summary>
    public string Name { get; set; } = "";

    /// <summary>旗帜和地图归属颜色，使用 ARGB 编码。</summary>
    public uint ColorArgb { get; set; }

    /// <summary>建国时的创始种族，不限制后续居民种族。</summary>
    public RaceKind FoundingRace { get; set; }

    /// <summary>首都聚落的 ID。</summary>
    public int CapitalId { get; set; }

    /// <summary>所属聚落的存活居民总数。</summary>
    public int Population { get; set; }

    /// <summary>登记属于本国的地格总数。</summary>
    public int Territory { get; set; }

    /// <summary>古代工具发展等级，范围为 1 至 5，独立于聚落研究。</summary>
    public int Technology { get; set; } = 1;

    /// <summary>最近一次国家决策的说明文字。</summary>
    public string Decision { get; set; } = "积累粮食，建立家园";

    /// <summary>用于展示的聚落库存合计；实际资源保存在各聚落仓库中。</summary>
    public ResourceStock Resources { get; set; } = new();

    // 未指定发展方向时按文化确定默认选择，避免依赖额外随机数。
    /// <summary>国家对未来科技与魔法规划的偏好。</summary>
    public DevelopmentFocus DevelopmentFocus { get; set; }

    /// <summary>机构发布的军令及实际收到的战报。</summary>
    public MilitaryRecord Military { get; set; } = new();

    /// <summary>文化归属的稳定 ID。</summary>
    public int CultureId { get; set; }

    /// <summary>国家代表居民的 ID。</summary>
    public int RepresentativeId { get; set; }
}
