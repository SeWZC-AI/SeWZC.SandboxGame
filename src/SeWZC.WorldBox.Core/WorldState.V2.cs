using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

public enum EventImportance
{
    Routine,
    Notable,
    Major,
    Historic,
}

public enum AgentGoalKind
{
    Idle,
    Eat,
    Gather,
    Work,
    Rest,
    Flee,
    Socialize,
    DeliverMessage,
    Trade,
    Petition,
    Study,
    TrainMagic,
    March,
    ReturnHome,
    Migrate,
    Explore,
    ClaimLand,
    FetchWater,
    Hunt,
    Fish,
    ExtinguishFire,
}

public enum AgentFactKind
{
    FoodSupply,
    Danger,
    SettlementLocation,
    ReliefRequest,
    Policy,
    WarOrder,
    PeaceOrder,
    Culture,
    Research,
    Personal,
    TradeExchange,
    DiplomaticNotice,
    WarReport,
    WaterSource,
    FoundingSite,
}

public enum PersonalExperienceKind
{
    Neutral,
    Hardship,
    Achievement,
    Kindness,
    Betrayal,
    Learning,
}

public sealed partial class WorldState
{
    [JsonRequired]
    public int SimulationVersion { get; set; } = 15;

    public SocietyState Society { get; set; } = new();
    public List<PendingMessage> PendingMessages { get; set; } = [];
    public List<Resident> ArchivedResidents { get; set; } = [];
}

public sealed partial class Tile
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public byte RoadLevel { get; set; }

    public double ResourceAmount { get; set; } = 100;
}

public sealed partial class Resident
{
    public int CultureId { get; set; }
    public AgentState Agent { get; set; } = new();
    public ResourceStock Inventory { get; set; } = new();
    public double Mana { get; set; } = 20;
    public double MagicTalent { get; set; } = 25;
    public double MagicTraining { get; set; }
    public int FromX { get; set; }
    public int FromY { get; set; }
    public long MoveStartedTick { get; set; }
    public int MoveDurationTicks { get; set; } = 1;
    public List<ResidentHistoryEntry> History { get; set; } = [];
}

public sealed partial class Settlement
{
    public int CultureId { get; set; }
    public int RepresentativeId { get; set; }
    public List<AgentFact> PublicKnowledge { get; set; } = [];
    public List<CivicOpinion> Petitions { get; set; } = [];
    public int FertilityBoostTicks { get; set; }
    public int ShieldTicks { get; set; }
}

public sealed partial class Nation
{
    public int CultureId { get; set; }
    public int RepresentativeId { get; set; }
}

public sealed partial class Army
{
    public int CommanderId { get; set; }
    public DiplomaticStatus KnownDiplomacy { get; set; } = DiplomaticStatus.War;
    public long LastOrderTick { get; set; }

    [JsonRequired]
    public int LastOrderFactId { get; set; }

    public int FromX { get; set; }
    public int FromY { get; set; }
    public long MoveStartedTick { get; set; }
    public int MoveDurationTicks { get; set; } = 2;
    public bool Gathering { get; set; } = true;
    public bool Retreating { get; set; }
    public int TargetX { get; set; }
    public int TargetY { get; set; }
    public int TargetSettlementId { get; set; }
}

public sealed partial class WorldEvent
{
    public int Id { get; set; }
    public EventImportance Importance { get; set; } = EventImportance.Notable;
    public int ResidentId { get; set; }
    public int NationId { get; set; }
    public int SecondNationId { get; set; }
    public int CauseEventId { get; set; }
}

/// <summary>用于目标评分及结构化经历影响的性格权重。</summary>
public sealed class PersonalityProfile
{
    public double Courage { get; set; } = 0.5;
    public double Diligence { get; set; } = 0.5;
    public double Sociability { get; set; } = 0.5;
    public double Ambition { get; set; } = 0.5;
}

/// <summary>居民的需求、性格、记忆，以及当前行动或递送任务。</summary>
public sealed class AgentState
{
    public bool Initialized { get; set; }
    public double Fatigue { get; set; }
    public double SocialNeed { get; set; }
    public PersonalityProfile Personality { get; set; } = new();
    public AgentGoal Goal { get; set; } = new();
    public List<AgentFact> Memory { get; set; } = [];
    public List<AgentDecision> Decisions { get; set; } = [];
    public long NextThinkTick { get; set; }
    public long LastConversationTick { get; set; }
    public int DestinationSettlementId { get; set; }
    public List<AgentFact> CarriedMessages { get; set; } = [];
    public int MissionOriginSettlementId { get; set; }
    public long MissionStartedTick { get; set; }
    public long MissionRetryTick { get; set; }

    [JsonRequired]
    public int ExplorationHeading { get; set; }

    public ResourceKind? MaterialPriority { get; set; }
    public long JobChangedTick { get; set; } = -120;
}

/// <summary>当前任务、目标及依据，以及为避免反复受阻而保存的导航进度。</summary>
public sealed class AgentGoal
{
    public int NavigationTarget { get; set; } = -1;
    public List<int> NavigationVisited { get; set; } = [];
    public int NavigationBestDistance { get; set; }
    public int NavigationWithoutProgress { get; set; }
    public long NavigationRetryTick { get; set; }
    public int EvidenceFactId { get; set; }
    public int CauseEventId { get; set; }
    public AgentGoalKind Kind { get; set; }
    public int TargetX { get; set; }
    public int TargetY { get; set; }
    public int TargetSettlementId { get; set; }
    public int TargetEntityId { get; set; }
    public long StartedTick { get; set; }
    public int WorkTicks { get; set; }
    public long ReviewTick { get; set; }
    public bool PlayerDirected { get; set; }
    public string Reason { get; set; } = "";
}

/// <summary>亲眼观察或转述得到的信息，保留原始依据和本副本的获知记录，内容可能已经过时。</summary>
public sealed class AgentFact
{
    public int EventId { get; set; }
    public int CampaignEventId { get; set; }
    public WarObjective WarObjective { get; set; }
    public int TargetNationId { get; set; }
    public int Id { get; set; }
    public AgentFactKind Kind { get; set; }
    public int SubjectId { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public double Value { get; set; }

    /// <summary>最初观察发生的时间；转述时保留该时间。</summary>
    public long ObservedTick { get; set; }

    /// <summary>持有本副本的一方获知信息的时间。</summary>
    public long LearnedTick { get; set; }

    public int OriginResidentId { get; set; }
    public Profession OriginProfession { get; set; }
    public int SourceResidentId { get; set; }
    public double Confidence { get; set; } = 1;
    public int Hops { get; set; }
    public string Text { get; set; } = "";
}

/// <summary>已记录的目标选择，包含当时的评分、理由和所用信息。</summary>
public sealed class AgentDecision
{
    public long Tick { get; set; }
    public AgentGoalKind Goal { get; set; }
    public double Score { get; set; }
    public string Reason { get; set; } = "";
    public int EvidenceFactId { get; set; }
    public long KnowledgeObservedTick { get; set; }
    public int SourceResidentId { get; set; }
}

/// <summary>等待按计划送达居民的信息，也可指定中继聚落作为接收地点。</summary>
public sealed class PendingMessage
{
    public int SenderId { get; set; }
    public int RecipientId { get; set; }
    public int TargetSettlementId { get; set; }
    public long DeliverTick { get; set; }
    public List<AgentFact> Facts { get; set; } = [];
}

/// <summary>存档保留的加权请愿记录；当前制度接收与决策使用 InstitutionReport。</summary>
public sealed class CivicOpinion
{
    public int FactId { get; set; }
    public int ResidentId { get; set; }
    public AgentFactKind Topic { get; set; }
    public double Value { get; set; }
    public double Weight { get; set; }
    public long ObservedTick { get; set; }
    public long ReceivedTick { get; set; }
}

/// <summary>关联世界事件的个人经历；结构化编辑可影响未来性格。</summary>
public sealed class ResidentHistoryEntry
{
    public int EventId { get; set; }
    public int SettlementId { get; set; }
    public int NationId { get; set; }
    public int EvidenceFactId { get; set; }
    public long Tick { get; set; }
    public EventImportance Importance { get; set; } = EventImportance.Notable;
    public string Text { get; set; } = "";
    public bool PlayerEdited { get; set; }
    public PersonalExperienceKind Experience { get; set; }
    public double Impact { get; set; }
}

/// <summary>Null fields retain their current value. Cognition/history edits change future behavior only.</summary>
public sealed class ResidentEdit
{
    public string? Name { get; set; }
    public RaceKind? Race { get; set; }
    public int? CultureId { get; set; }
    public int? SettlementId { get; set; }
    public int? X { get; set; }
    public int? Y { get; set; }
    public int? ArmyId { get; set; }
    public int? SicknessTicks { get; set; }
    public ResourceStock? Inventory { get; set; }
    public Profession? Profession { get; set; }
    public double? Age { get; set; }
    public double? Health { get; set; }
    public double? Hunger { get; set; }
    public double? Thirst { get; set; }
    public string? Trait { get; set; }
    public double? Mana { get; set; }
    public double? MagicTalent { get; set; }
    public double? MagicTraining { get; set; }
    public AgentState? Agent { get; set; }
    public List<ResidentHistoryEntry>? History { get; set; }
}
