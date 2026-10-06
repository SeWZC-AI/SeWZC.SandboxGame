using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

/// <summary>编年史事件的重要程度，用于展示和记录容量管理。</summary>
public enum EventImportance
{
    /// <summary>日常。</summary>
    Routine,
    /// <summary>值得关注。</summary>
    Notable,
    /// <summary>重大。</summary>
    Major,
    /// <summary>历史性。</summary>
    Historic,
}

/// <summary>居民当前选择的行动目标类别。</summary>
public enum AgentGoalKind
{
    /// <summary>空闲。</summary>
    Idle,
    /// <summary>进食。</summary>
    Eat,
    /// <summary>采集。</summary>
    Gather,
    /// <summary>劳动。</summary>
    Work,
    /// <summary>休息。</summary>
    Rest,
    /// <summary>逃离危险。</summary>
    Flee,
    /// <summary>社交。</summary>
    Socialize,
    /// <summary>递送消息。</summary>
    DeliverMessage,
    /// <summary>贸易。</summary>
    Trade,
    /// <summary>提交议题。</summary>
    Petition,
    /// <summary>学习研究。</summary>
    Study,
    /// <summary>训练魔法。</summary>
    TrainMagic,
    /// <summary>行军。</summary>
    March,
    /// <summary>返回家园。</summary>
    ReturnHome,
    /// <summary>迁徙。</summary>
    Migrate,
    /// <summary>探索。</summary>
    Explore,
    /// <summary>占领地块。</summary>
    ClaimLand,
    /// <summary>取水。</summary>
    FetchWater,
    /// <summary>狩猎。</summary>
    Hunt,
    /// <summary>捕鱼。</summary>
    Fish,
    /// <summary>扑灭火灾。</summary>
    ExtinguishFire,
}

/// <summary>居民观察、记忆或转述的信息议题类别。</summary>
public enum AgentFactKind
{
    /// <summary>食物供给。</summary>
    FoodSupply,
    /// <summary>危险。</summary>
    Danger,
    /// <summary>聚落位置。</summary>
    SettlementLocation,
    /// <summary>求援。</summary>
    ReliefRequest,
    /// <summary>政策。</summary>
    Policy,
    /// <summary>战争命令。</summary>
    WarOrder,
    /// <summary>停战命令。</summary>
    PeaceOrder,
    /// <summary>文化。</summary>
    Culture,
    /// <summary>研究知识。</summary>
    Research,
    /// <summary>个人消息。</summary>
    Personal,
    /// <summary>贸易交换。</summary>
    TradeExchange,
    /// <summary>外交通知。</summary>
    DiplomaticNotice,
    /// <summary>战报。</summary>
    WarReport,
    /// <summary>水源。</summary>
    WaterSource,
    /// <summary>建村地点。</summary>
    FoundingSite,
}

/// <summary>可影响未来性格的结构化个人经历类别。</summary>
public enum PersonalExperienceKind
{
    /// <summary>中性经历。</summary>
    Neutral,
    /// <summary>困苦。</summary>
    Hardship,
    /// <summary>成就。</summary>
    Achievement,
    /// <summary>善意。</summary>
    Kindness,
    /// <summary>背叛。</summary>
    Betrayal,
    /// <summary>学习。</summary>
    Learning,
}

public sealed partial class WorldState
{
    /// <summary>模拟规则版本，用于校验存档的续演兼容性。</summary>
    [JsonRequired]
    public int SimulationVersion { get; set; } = 15;

    /// <summary>文化、设施、研究、制度及已收到报告的状态。</summary>
    public SocietyState Society { get; set; } = new();
    /// <summary>等待送达的消息集合。</summary>
    public List<PendingMessage> PendingMessages { get; set; } = [];
    /// <summary>已死亡居民的有限归档，供查看经历。</summary>
    public List<Resident> ArchivedResidents { get; set; } = [];
}

public sealed partial class Tile
{
    /// <summary>道路等级，0 表示没有道路。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public byte RoadLevel { get; set; }

    /// <summary>可采集的共享自然资源存量。</summary>
    public double ResourceAmount { get; set; } = 100;
}

public sealed partial class Resident
{
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

public sealed partial class Settlement
{
    /// <summary>文化归属的稳定 ID。</summary>
    public int CultureId { get; set; }
    /// <summary>本聚落代表居民的 ID。</summary>
    public int RepresentativeId { get; set; }
    /// <summary>实际在本地观察或收到的公开信息，可能已经过时。</summary>
    public List<AgentFact> PublicKnowledge { get; set; } = [];
    /// <summary>存档保留的请愿记录；当前制度决策使用已收到的机构报告。</summary>
    public List<CivicOpinion> Petitions { get; set; } = [];
    /// <summary>丰饶祝福剩余模拟日数。</summary>
    public int FertilityBoostTicks { get; set; }
    /// <summary>聚落局部护盾剩余模拟日数。</summary>
    public int ShieldTicks { get; set; }
}

public sealed partial class Nation
{
    /// <summary>文化归属的稳定 ID。</summary>
    public int CultureId { get; set; }
    /// <summary>国家代表居民的 ID。</summary>
    public int RepresentativeId { get; set; }
}

public sealed partial class Army
{
    /// <summary>军队指挥居民的 ID。</summary>
    public int CommanderId { get; set; }
    /// <summary>军队根据已收到消息获知的外交状态。</summary>
    public DiplomaticStatus KnownDiplomacy { get; set; } = DiplomaticStatus.War;
    /// <summary>最近收到军令的观察日序。</summary>
    public long LastOrderTick { get; set; }

    /// <summary>最近收到军令的信息记录 ID。</summary>
    [JsonRequired]
    public int LastOrderFactId { get; set; }

    /// <summary>当前移动区段起点的横向地格坐标。</summary>
    public int FromX { get; set; }
    /// <summary>当前移动区段起点的纵向地格坐标。</summary>
    public int FromY { get; set; }
    /// <summary>当前移动区段开始的模拟日序。</summary>
    public long MoveStartedTick { get; set; }
    /// <summary>当前移动区段所需的模拟日数。</summary>
    public int MoveDurationTicks { get; set; } = 2;
    /// <summary>是否仍在集结士兵。</summary>
    public bool Gathering { get; set; } = true;
    /// <summary>是否正在撤回家园。</summary>
    public bool Retreating { get; set; }
    /// <summary>军队目标的横向地格坐标。</summary>
    public int TargetX { get; set; }
    /// <summary>军队目标的纵向地格坐标。</summary>
    public int TargetY { get; set; }
    /// <summary>当前军事目标聚落的 ID。</summary>
    public int TargetSettlementId { get; set; }
}

public sealed partial class WorldEvent
{
    /// <summary>世界事件的稳定 ID。</summary>
    public int Id { get; set; }
    /// <summary>事件的重要程度，用于展示及容量淘汰。</summary>
    public EventImportance Importance { get; set; } = EventImportance.Notable;
    /// <summary>关联居民的稳定 ID。</summary>
    public int ResidentId { get; set; }
    /// <summary>关联或归属国家的稳定 ID。</summary>
    public int NationId { get; set; }
    /// <summary>事件涉及的另一国家 ID。</summary>
    public int SecondNationId { get; set; }
    /// <summary>主要前因事件 ID，0 表示未关联前因。</summary>
    public int CauseEventId { get; set; }
}

/// <summary>用于目标评分及结构化经历影响的性格权重。</summary>
public sealed class PersonalityProfile
{
    /// <summary>勇气权重，范围为 0 至 1。</summary>
    public double Courage { get; set; } = 0.5;
    /// <summary>勤劳权重，范围为 0 至 1。</summary>
    public double Diligence { get; set; } = 0.5;
    /// <summary>社交权重，范围为 0 至 1。</summary>
    public double Sociability { get; set; } = 0.5;
    /// <summary>进取权重，范围为 0 至 1。</summary>
    public double Ambition { get; set; } = 0.5;
}

/// <summary>居民的需求、性格、记忆，以及当前行动或递送任务。</summary>
public sealed class AgentState
{
    /// <summary>是否已经初始化需求、性格和初始认知。</summary>
    public bool Initialized { get; set; }
    /// <summary>疲劳程度，越高表示越需要休息。</summary>
    public double Fatigue { get; set; }
    /// <summary>社交需求程度，越高表示越需要交流。</summary>
    public double SocialNeed { get; set; }
    /// <summary>当前用于行动评分的性格权重。</summary>
    public PersonalityProfile Personality { get; set; } = new();
    /// <summary>当前行动目标及其导航进度。</summary>
    public AgentGoal Goal { get; set; } = new();
    /// <summary>居民自己观察或收到的信息，可能已经过时。</summary>
    public List<AgentFact> Memory { get; set; } = [];
    /// <summary>近期目标选择的评分、理由和依据记录。</summary>
    public List<AgentDecision> Decisions { get; set; } = [];
    /// <summary>下一次自主评估目标的模拟日序。</summary>
    public long NextThinkTick { get; set; }
    /// <summary>最近一次与其他居民交谈的模拟日序。</summary>
    public long LastConversationTick { get; set; }
    /// <summary>当前贸易或递送任务的目的聚落 ID。</summary>
    public int DestinationSettlementId { get; set; }
    /// <summary>居民正在实际携带的信息副本。</summary>
    public List<AgentFact> CarriedMessages { get; set; } = [];
    /// <summary>当前贸易或递送任务的出发聚落 ID。</summary>
    public int MissionOriginSettlementId { get; set; }
    /// <summary>当前任务开始的模拟日序。</summary>
    public long MissionStartedTick { get; set; }
    /// <summary>当前任务受阻后允许再次尝试的模拟日序。</summary>
    public long MissionRetryTick { get; set; }

    /// <summary>当前探索朝向，按八个方向编号。</summary>
    [JsonRequired]
    public int ExplorationHeading { get; set; }

    /// <summary>当前优先补充的材料种类，空值表示无指定优先材料。</summary>
    public ResourceKind? MaterialPriority { get; set; }
    /// <summary>最近一次职业分工变化的模拟日序。</summary>
    public long JobChangedTick { get; set; } = -120;
}

/// <summary>当前任务、目标及依据，以及为避免反复受阻而保存的导航进度。</summary>
public sealed class AgentGoal
{
    /// <summary>当前导航目标地格的数组索引，-1 表示尚未设置。</summary>
    public int NavigationTarget { get; set; } = -1;
    /// <summary>当前导航已走过的地格索引，用于避免反复绕路。</summary>
    public List<int> NavigationVisited { get; set; } = [];
    /// <summary>当前导航曾达到的最短目标距离，以地格计。</summary>
    public int NavigationBestDistance { get; set; }
    /// <summary>连续未缩短目标距离的导航尝试次数。</summary>
    public int NavigationWithoutProgress { get; set; }
    /// <summary>受阻后允许重新尝试导航的模拟日序。</summary>
    public long NavigationRetryTick { get; set; }
    /// <summary>选择该目标使用的信息依据 ID。</summary>
    public int EvidenceFactId { get; set; }
    /// <summary>该行动关联的前因事件 ID。</summary>
    public int CauseEventId { get; set; }
    /// <summary>当前行动目标类别。</summary>
    public AgentGoalKind Kind { get; set; }
    /// <summary>目标位置的横向地格坐标。</summary>
    public int TargetX { get; set; }
    /// <summary>目标位置的纵向地格坐标。</summary>
    public int TargetY { get; set; }
    /// <summary>目标关联的聚落 ID。</summary>
    public int TargetSettlementId { get; set; }
    /// <summary>任务对象编号；设施任务使用建筑 ID，取水和狩猎捕鱼使用资源地格索引加 1。</summary>
    public int TargetEntityId { get; set; }
    /// <summary>该目标开始执行的模拟日序。</summary>
    public long StartedTick { get; set; }
    /// <summary>该目标已累计的劳动或驻留日数。</summary>
    public int WorkTicks { get; set; }
    /// <summary>下次重新评估该目标的模拟日序。</summary>
    public long ReviewTick { get; set; }
    /// <summary>该目标是否由玩家直接安排。</summary>
    public bool PlayerDirected { get; set; }
    /// <summary>选择该目标的理由。</summary>
    public string Reason { get; set; } = "";
}

/// <summary>亲眼观察或转述得到的信息，保留原始依据和本副本的获知记录，内容可能已经过时。</summary>
public sealed class AgentFact
{
    /// <summary>关联的世界事件 ID，0 表示未关联事件。</summary>
    public int EventId { get; set; }
    /// <summary>该信息关联的战役起始事件 ID。</summary>
    public int CampaignEventId { get; set; }
    /// <summary>战争信息中的军事目标。</summary>
    public WarObjective WarObjective { get; set; }
    /// <summary>战争信息中的目标国家 ID。</summary>
    public int TargetNationId { get; set; }
    /// <summary>信息记录的稳定 ID。</summary>
    public int Id { get; set; }
    /// <summary>信息议题类别。</summary>
    public AgentFactKind Kind { get; set; }
    /// <summary>信息关联主体的 ID，具体含义由议题类别决定。</summary>
    public int SubjectId { get; set; }
    /// <summary>所在地点的横向地格坐标。</summary>
    public int X { get; set; }
    /// <summary>所在地点的纵向地格坐标。</summary>
    public int Y { get; set; }
    /// <summary>观察得到的数值，具体含义由议题类别决定。</summary>
    public double Value { get; set; }

    /// <summary>最初观察发生的时间；转述时保留该时间。</summary>
    public long ObservedTick { get; set; }

    /// <summary>持有本副本的一方获知信息的时间。</summary>
    public long LearnedTick { get; set; }

    /// <summary>最初观察者的居民 ID。</summary>
    public int OriginResidentId { get; set; }
    /// <summary>最初观察者当时的职业。</summary>
    public Profession OriginProfession { get; set; }
    /// <summary>将本副本提供给持有者的居民 ID。</summary>
    public int SourceResidentId { get; set; }
    /// <summary>本副本的可信度，范围为 0 至 1。</summary>
    public double Confidence { get; set; } = 1;
    /// <summary>信息已经转述的次数。</summary>
    public int Hops { get; set; }
    /// <summary>观察或转述的内容说明。</summary>
    public string Text { get; set; } = "";
}

/// <summary>已记录的目标选择，包含当时的评分、理由和所用信息。</summary>
public sealed class AgentDecision
{
    /// <summary>作出目标选择的模拟日序。</summary>
    public long Tick { get; set; }
    /// <summary>当时选择的行动目标类别。</summary>
    public AgentGoalKind Goal { get; set; }
    /// <summary>当时选择该目标的评分。</summary>
    public double Score { get; set; }
    /// <summary>当时选择该目标的理由。</summary>
    public string Reason { get; set; } = "";
    /// <summary>目标评分采用的信息依据 ID。</summary>
    public int EvidenceFactId { get; set; }
    /// <summary>依据的信息最初被观察的模拟日序。</summary>
    public long KnowledgeObservedTick { get; set; }
    /// <summary>该信息副本的提供者居民 ID。</summary>
    public int SourceResidentId { get; set; }
}

/// <summary>等待按计划送达居民的信息，也可指定中继聚落作为接收地点。</summary>
public sealed class PendingMessage
{
    /// <summary>发送消息的居民 ID。</summary>
    public int SenderId { get; set; }
    /// <summary>接收消息的居民 ID。</summary>
    public int RecipientId { get; set; }
    /// <summary>作为中继接收地点的聚落 ID。</summary>
    public int TargetSettlementId { get; set; }
    /// <summary>计划送达的模拟日序。</summary>
    public long DeliverTick { get; set; }
    /// <summary>待递送的信息副本。</summary>
    public List<AgentFact> Facts { get; set; } = [];
}

/// <summary>存档保留的加权请愿记录；当前制度接收与决策使用 InstitutionReport。</summary>
public sealed class CivicOpinion
{
    /// <summary>请愿依据的信息记录 ID。</summary>
    public int FactId { get; set; }
    /// <summary>关联居民的稳定 ID。</summary>
    public int ResidentId { get; set; }
    /// <summary>请愿涉及的信息议题类别。</summary>
    public AgentFactKind Topic { get; set; }
    /// <summary>请愿记录的议题数值。</summary>
    public double Value { get; set; }
    /// <summary>请愿记录的议题权重。</summary>
    public double Weight { get; set; }
    /// <summary>议题最初被观察的模拟日序。</summary>
    public long ObservedTick { get; set; }
    /// <summary>请愿被接收的模拟日序。</summary>
    public long ReceivedTick { get; set; }
}

/// <summary>关联世界事件的个人经历；结构化编辑可影响未来性格。</summary>
public sealed class ResidentHistoryEntry
{
    /// <summary>关联的世界事件 ID，0 表示未关联事件。</summary>
    public int EventId { get; set; }
    /// <summary>关联或归属聚落的稳定 ID。</summary>
    public int SettlementId { get; set; }
    /// <summary>关联或归属国家的稳定 ID。</summary>
    public int NationId { get; set; }
    /// <summary>本次经历关联的信息依据 ID。</summary>
    public int EvidenceFactId { get; set; }
    /// <summary>经历发生的模拟日序。</summary>
    public long Tick { get; set; }
    /// <summary>经历的重要程度。</summary>
    public EventImportance Importance { get; set; } = EventImportance.Notable;
    /// <summary>档案中显示的经历描述。</summary>
    public string Text { get; set; } = "";
    /// <summary>该经历是否经玩家编辑。</summary>
    public bool PlayerEdited { get; set; }
    /// <summary>用于未来性格调整的结构化经历类别。</summary>
    public PersonalExperienceKind Experience { get; set; }
    /// <summary>结构化经历对未来性格的影响强度。</summary>
    public double Impact { get; set; }
}

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
