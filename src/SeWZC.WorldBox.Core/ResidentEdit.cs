namespace SeWZC.WorldBox.Core;

/// <summary>居民修改补丁；空值保留原字段，认知和经历编辑只影响未来行为。</summary>
public sealed class ResidentEdit
{
    /// <summary>要设置的姓名。</summary>
    public string? Name { get; set; }
    /// <summary>要设置的种族。</summary>
    public RaceKind? Race { get; set; }
    /// <summary>要设置的文化 ID。</summary>
    public int? CultureId { get; set; }
    /// <summary>要设置的归属聚落 ID。</summary>
    public int? SettlementId { get; set; }
    /// <summary>要设置的横向地格坐标。</summary>
    public int? X { get; set; }
    /// <summary>要设置的纵向地格坐标。</summary>
    public int? Y { get; set; }
    /// <summary>要设置的军队 ID。</summary>
    public int? ArmyId { get; set; }
    /// <summary>要设置的疫病剩余日数。</summary>
    public int? SicknessTicks { get; set; }
    /// <summary>要设置的随身资源。</summary>
    public ResourceStock? Inventory { get; set; }
    /// <summary>要设置的职业。</summary>
    public Profession? Profession { get; set; }
    /// <summary>要设置的年龄，以模拟年计。</summary>
    public double? Age { get; set; }
    /// <summary>要设置的生命值。</summary>
    public double? Health { get; set; }
    /// <summary>要设置的饥饿程度。</summary>
    public double? Hunger { get; set; }
    /// <summary>要设置的口渴程度。</summary>
    public double? Thirst { get; set; }
    /// <summary>要设置的性格描述。</summary>
    public string? Trait { get; set; }
    /// <summary>要设置的当前魔力。</summary>
    public double? Mana { get; set; }
    /// <summary>要设置的魔法天赋。</summary>
    public double? MagicTalent { get; set; }
    /// <summary>要设置的魔法训练程度。</summary>
    public double? MagicTraining { get; set; }
    /// <summary>要设置的认知和行动状态。</summary>
    public AgentState? Agent { get; set; }
    /// <summary>要设置的个人经历记录。</summary>
    public List<ResidentHistoryEntry>? History { get; set; }
}
