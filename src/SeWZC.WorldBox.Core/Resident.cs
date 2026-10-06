using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

/// <summary>居民的身体状态、归属、随身资源、认知及已记录的经历。</summary>
public sealed class Resident
{
    /// <summary>居民的稳定 ID。</summary>
    public int Id { get; set; }

    /// <summary>居民的显示名称。</summary>
    public string Name { get; set; } = "";

    /// <summary>居民的种族。</summary>
    public RaceKind Race { get; set; }

    /// <summary>所在地点的横向地格坐标。</summary>
    public int X { get; set; }

    /// <summary>所在地点的纵向地格坐标。</summary>
    public int Y { get; set; }

    /// <summary>年龄，以模拟年为单位，允许小数。</summary>
    public double Age { get; set; }

    /// <summary>关联或归属国家的稳定 ID。</summary>
    public int NationId { get; set; }

    /// <summary>关联或归属聚落的稳定 ID。</summary>
    public int SettlementId { get; set; }

    /// <summary>当前职业分工。</summary>
    public Profession Profession { get; set; }

    /// <summary>当前活动或身体状态。</summary>
    public ResidentActivity Activity { get; set; }

    /// <summary>当前生命值，归零时居民死亡。</summary>
    public double Health { get; set; } = 100;

    /// <summary>饥饿程度，越高表示越缺粮。</summary>
    public double Hunger { get; set; }

    /// <summary>疫病剩余模拟日数，0 表示未患病。</summary>
    public int SicknessTicks { get; set; }

    /// <summary>所加入的军队 ID，0 表示未编入军队。</summary>
    public int ArmyId { get; set; }

    /// <summary>供档案显示的性格特征文字。</summary>
    public string Trait { get; set; } = "勤劳";

    /// <summary>疫病康复后的暂时免疫截止日序。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public long DiseaseImmuneUntilTick { get; set; }

    /// <summary>死亡直接原因，存活时为 <c>None</c>。</summary>
    [JsonRequired]
    public DeathCause DeathCause { get; set; }

    /// <summary>死亡时的模拟日序。</summary>
    [JsonRequired]
    public long DeathTick { get; set; }

    /// <summary>当前个人护甲强度。</summary>
    [JsonRequired]
    public double Armor { get; set; }

    /// <summary>当前可吸收伤害的个人符文护甲余量。</summary>
    [JsonRequired]
    public double PersonalWard { get; set; }

    /// <summary>冰霜减速效果的截止日序。</summary>
    [JsonRequired]
    public long FrozenUntilTick { get; set; }

    /// <summary>最近一次远程攻击的模拟日序。</summary>
    [JsonRequired]
    public long LastRangedAttackTick { get; set; } = -100;

    /// <summary>当前移动采用的交通方式。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public TravelMode TravelMode { get; set; }

    /// <summary>口渴程度，越高表示越缺水。</summary>
    [JsonRequired]
    public double Thirst { get; set; }

    /// <summary>文化归属的稳定 ID。</summary>
    public int CultureId { get; set; }

    /// <summary>居民的需求、性格、记忆和行动目标。</summary>
    public AgentState Agent { get; set; } = new();

    /// <summary>居民实际随身携带的资源和物品。</summary>
    public ResourceStock Inventory { get; set; } = new();

    /// <summary>当前可消耗的魔力。</summary>
    public double Mana { get; set; } = 20;

    /// <summary>魔法天赋，影响学习和施法能力。</summary>
    public double MagicTalent { get; set; } = 25;

    /// <summary>累计魔法训练程度。</summary>
    public double MagicTraining { get; set; }

    /// <summary>当前移动区段起点的横向地格坐标。</summary>
    public int FromX { get; set; }

    /// <summary>当前移动区段起点的纵向地格坐标。</summary>
    public int FromY { get; set; }

    /// <summary>当前移动区段开始的模拟日序。</summary>
    public long MoveStartedTick { get; set; }

    /// <summary>当前移动区段所需的模拟日数。</summary>
    public int MoveDurationTicks { get; set; } = 1;

    /// <summary>容量受限的个人经历记录。</summary>
    public List<ResidentHistoryEntry> History { get; set; } = [];
}
