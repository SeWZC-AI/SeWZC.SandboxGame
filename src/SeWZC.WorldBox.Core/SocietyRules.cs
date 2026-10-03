using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

public enum BuildingKind { Farm, Workshop, Academy, Waystation, SignalTower, ArcaneSanctum, Infirmary,
    Foundry, PowerPlant, AutomatedFarm, Fabricator, Crystallizer, RunicGarden, AetherForge, MountainPass, Bridge, Dock, Airfield, TownCenter }
public enum ResearchKind { Agriculture, Logistics, SignalNetwork, ArcaneArts,
    Industry, Electrification, Automation, AdvancedComputing, Crystalcraft, RunicEngineering, AetherMastery, Aviation }
public enum InstitutionKind { Council, Monarchy, GuildCouncil }
public enum PolicyKind { Balanced, FoodSecurity, Defense, Scholarship, PublicHealth }
public enum SpellKind { Heal, HarvestBlessing, Shield, Ember }

public sealed class SocietyState
{
    public bool MagicEnabled { get; set; } = true;
    public List<CultureDefinition> Cultures { get; set; } = [];
    public List<Building> Buildings { get; set; } = [];
    public List<SettlementResearch> Research { get; set; } = [];
    public List<LocalPolicy> Policies { get; set; } = [];
    public List<NationInstitution> Institutions { get; set; } = [];
    public List<InstitutionReport> Reports { get; set; } = [];
    public List<CulturalContact> CulturalContacts { get; set; } = [];
}

public sealed class CultureDefinition
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public double Cooperation { get; set; } = 0.5;
    public double Innovation { get; set; } = 0.5;
    public double NatureAffinity { get; set; } = 0.5;
}

public sealed class Building
{
    [JsonRequired] public int ProductionBatches { get; set; }
    [JsonRequired] public bool Enabled { get; set; } = true;
    public ProjectObservation Observation { get; set; } = new();
    public int Id { get; set; }
    public int SettlementId { get; set; }
    public BuildingKind Kind { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public double ConstructionProgress { get; set; }
    public double ConstructionRequired { get; set; } = 30;
    public double Health { get; set; } = 100;
    public int WorkSlots { get; set; } = 3;
    public List<int> Workers { get; set; } = [];
    public long LastWorkedTick { get; set; } = -100;
    [JsonIgnore] public bool IsCompleted => ConstructionProgress >= ConstructionRequired && Health > 0;
}

public sealed class SettlementResearch
{
    public ProjectObservation Observation { get; set; } = new();
    public int LastCompletionEventId { get; set; }
    public int SettlementId { get; set; }
    public ResearchKind? ActiveProject { get; set; }
    public double Progress { get; set; }
    public double RequiredProgress { get; set; }
    public List<ResearchKind> Completed { get; set; } = [];
}

public sealed class LocalPolicy
{
    public int SettlementId { get; set; }
    public PolicyKind Kind { get; set; }
    public bool PlayerOverride { get; set; }
    public long DecidedTick { get; set; }
    public string Reason { get; set; } = "尚无递送议题，维持均衡政策";
    public int EvidenceFactId { get; set; }
    public long EvidenceObservedTick { get; set; }
}

public sealed class NationInstitution
{
    public int NationId { get; set; }
    public InstitutionKind Kind { get; set; }
    public PolicyKind? PlayerPolicy { get; set; }
    public string LastDecision { get; set; } = "等待本地议事及代表送达的报告";
    public long LastDecisionTick { get; set; }
}

public sealed class InstitutionReport
{
    public int EventId { get; set; }
    public int RecipientSettlementId { get; set; }
    public int FactId { get; set; }
    public int OriginResidentId { get; set; }
    public int RepresentativeId { get; set; }
    public Profession ReportedProfession { get; set; }
    public AgentFactKind Topic { get; set; }
    public int SubjectId { get; set; }
    public double Value { get; set; }
    public double Confidence { get; set; }
    public long ObservedTick { get; set; }
    public long ReceivedTick { get; set; }
}

public sealed class CulturalContact
{
    public int ResidentId { get; set; }
    public int CultureId { get; set; }
    public double Exposure { get; set; }
    public long LastContactTick { get; set; }
}

public readonly record struct TerrainParameters(double MovementCost, byte Fertility, double FoodYield,
    double WoodYield, double StoneYield, double OreYield, double ManaRate);

/// <summary>Physical biome differences shared by gathering, movement and local magic.</summary>
public static class TerrainRules
{
    public static TerrainParameters For(TerrainType terrain) => terrain switch
    {
        TerrainType.DeepWater => new(double.PositiveInfinity, 0, 0, 0, 0, 0, 0.5),
        TerrainType.Water => new(double.PositiveInfinity, 5, 0, 0, 0, 0, 0.8),
        TerrainType.Sand => new(1.4, 20, 0.12, 0, 0.25, 0.01, 0.4),
        TerrainType.Grass => new(1, 85, 0.5, 0.08, 0.05, 0, 1),
        TerrainType.Forest => new(1.5, 75, 0.35, 0.7, 0.04, 0, 1.8),
        TerrainType.Mountain => new(double.PositiveInfinity, 5, 0, 0, 0.8, 0.5, 1.4),
        TerrainType.Snow => new(1.8, 8, 0.08, 0, 0.4, 0.25, 0.8),
        TerrainType.Hills => new(1.6, 45, 0.2, 0.15, 0.6, 0.35, 1.3),
        TerrainType.Wetland => new(2, 95, 0.65, 0.22, 0.02, 0, 2),
        TerrainType.Desert => new(1.7, 8, 0.05, 0, 0.3, 0.12, 0.35),
        TerrainType.River => new(double.PositiveInfinity, 80, 0.7, 0, 0.2, 0.03, 1.7),
        TerrainType.Tundra => new(1.8, 20, 0.13, 0.08, 0.3, 0.15, 0.7),
        _ => new(1, 50, 0.2, 0.1, 0.1, 0, 1)
    };
    public static byte Fertility(TerrainType terrain) => For(terrain).Fertility;
    public static double MovementCost(TerrainType terrain) => For(terrain).MovementCost;
}
