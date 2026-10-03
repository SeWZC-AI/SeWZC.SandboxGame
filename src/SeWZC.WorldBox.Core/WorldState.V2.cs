using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

public enum EventImportance { Routine, Notable, Major, Historic }
public enum AgentGoalKind { Idle, Eat, Gather, Work, Rest, Flee, Socialize, DeliverMessage, Trade, Petition, Study, TrainMagic, March, ReturnHome, Migrate }
public enum AgentFactKind { FoodSupply, Danger, SettlementLocation, ReliefRequest, Policy, WarOrder, PeaceOrder, Culture, Research, Personal, TradeExchange, DiplomaticNotice, WarReport }
public enum PersonalExperienceKind { Neutral, Hardship, Achievement, Kindness, Betrayal, Learning }

public sealed partial class WorldState
{
    [JsonRequired]
    public int SimulationVersion { get; set; } = 7;
    public SocietyState Society { get; set; } = new();
    public List<PendingMessage> PendingMessages { get; set; } = [];
    public List<Resident> ArchivedResidents { get; set; } = [];
}

public sealed partial class Tile
{
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

public sealed class PersonalityProfile
{
    public double Courage { get; set; } = 0.5;
    public double Diligence { get; set; } = 0.5;
    public double Sociability { get; set; } = 0.5;
    public double Ambition { get; set; } = 0.5;
}

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
    public long JobChangedTick { get; set; } = -120;
}

public sealed class AgentGoal
{
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
    public long ObservedTick { get; set; }
    public long LearnedTick { get; set; }
    public int OriginResidentId { get; set; }
    public Profession OriginProfession { get; set; }
    public int SourceResidentId { get; set; }
    public double Confidence { get; set; } = 1;
    public int Hops { get; set; }
    public string Text { get; set; } = "";
}

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

public sealed class PendingMessage
{
    public int SenderId { get; set; }
    public int RecipientId { get; set; }
    public int TargetSettlementId { get; set; }
    public long DeliverTick { get; set; }
    public List<AgentFact> Facts { get; set; } = [];
}

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
    public string? Trait { get; set; }
    public double? Mana { get; set; }
    public double? MagicTalent { get; set; }
    public double? MagicTraining { get; set; }
    public AgentState? Agent { get; set; }
    public List<ResidentHistoryEntry>? History { get; set; }
}
