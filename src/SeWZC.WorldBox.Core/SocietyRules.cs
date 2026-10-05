using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

public enum BuildingKind
{
    Farm,
    Workshop,
    Academy,
    Waystation,
    SignalTower,
    ArcaneSanctum,
    Infirmary,
    Foundry,
    PowerPlant,
    AutomatedFarm,
    Fabricator,
    Crystallizer,
    RunicGarden,
    AetherForge,
    MountainPass,
    Bridge,
    Dock,
    Airfield,
    TownCenter,
    Shipyard,
    LumberCamp,
    Quarry,
    Well,
    Granary,
    Housing,
    Market,
    Watchtower,
    AssemblyHall,
    TradeGuild,
    SacredGrove,
    HerbGarden,
    DwarvenForge,
    MiningHall,
    HuntingCamp,
    WarDrum,
    Reservoir,
    Hospital,
    Apothecary,
    FireStation,
    Library,
    SurveyOffice,
    MachineWorkshop,
    Arsenal,
    Armory,
    AlchemyLab,
    WardTower,
    StormSpire,
    GroveSanctuary,
    Waygate,
    Pasture,
    Aquaculture,
}

public enum ResearchKind
{
    Agriculture,
    Logistics,
    SignalNetwork,
    ArcaneArts,
    Industry,
    Electrification,
    Automation,
    AdvancedComputing,
    Crystalcraft,
    RunicEngineering,
    AetherMastery,
    Aviation,
    Irrigation,
    Forestry,
    Medicine,
    ScientificMethod,
    EfficientSmelting,
    EnergyRecycling,

    // 18 and 23 were incorrectly assigned to civilization outcomes. They are reserved.
    ManaAttunement = 19,
    Restoration,
    ArcaneScholarship,
    Leylines,
    CivilEngineering = 24,
    Sanitation,
    Pharmacology,
    FireEngineering,
    Education,
    Cartography,
    MechanicalEngineering,
    Toolmaking,
    Ballistics,
    ProtectiveEquipment,
    RailTransport,
    Observation,
    Elementalism,
    Warding,
    Alchemy,
    NatureBinding,
    SpatialMagic,
    BattleMagic,
}

public enum InstitutionKind
{
    Council,
    Monarchy,
    GuildCouncil,
}

public enum PolicyKind
{
    Balanced,
    FoodSecurity,
    Defense,
    Scholarship,
    PublicHealth,
}

public enum SpellKind
{
    Heal,
    HarvestBlessing,
    Shield,
    Ember,
    FrostBolt,
    ChainLightning,
    RainCall,
    RuneWard,
}

/// <summary>需要保存的文化、设施、本地研究、制度，以及机构实际收到的报告。</summary>
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

/// <summary>实际设施的归属、施工、生命值、劳动及生产或服务记录。</summary>
public sealed partial class Building
{
    [JsonRequired]
    public WildlifeKind LivestockKind { get; set; }

    [JsonRequired]
    public double LivestockPopulation { get; set; }

    [JsonRequired]
    public int ProductionBatches { get; set; }

    [JsonRequired]
    public int ServiceActions { get; set; }

    [JsonRequired]
    public long LastServiceTick { get; set; } = -100;

    [JsonRequired]
    public bool Enabled { get; set; } = true;

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

    [JsonIgnore]
    public bool IsCompleted => ConstructionProgress >= ConstructionRequired && Health > 0;
}

/// <summary>聚落已掌握的研究及当前项目进度，包含从外地收到的知识。</summary>
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

/// <summary>聚落选定的政策及其依据，也可由玩家覆盖选择。</summary>
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

/// <summary>国家的制度形式、可选的玩家指定政策及最近一次制度决策。</summary>
public sealed class NationInstitution
{
    public int NationId { get; set; }
    public InstitutionKind Kind { get; set; }
    public PolicyKind? PlayerPolicy { get; set; }
    public string LastDecision { get; set; } = "等待本地议事及代表送达的报告";
    public long LastDecisionTick { get; set; }
}

/// <summary>聚落机构已收到的议题，保留最初观察者、其当时职业和观察时间。</summary>
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

public readonly record struct TerrainParameters(
    double MovementCost,
    byte Fertility,
    double FoodYield,
    double WoodYield,
    double StoneYield,
    double OreYield,
    double ManaRate);

/// <summary>Physical biome differences shared by gathering, movement and local magic.</summary>
public static class TerrainRules
{
    public static TerrainParameters For(TerrainType terrain)
    {
        return terrain switch
        {
            TerrainType.DeepWater => new TerrainParameters(double.PositiveInfinity, 0, 0, 0, 0, 0, 0.5),
            TerrainType.Lake => new TerrainParameters(double.PositiveInfinity, 80, 0, 0, 0, 0, 1.7),
            TerrainType.Water => new TerrainParameters(double.PositiveInfinity, 5, 0, 0, 0, 0, 0.8),
            TerrainType.Sand => new TerrainParameters(1.4, 20, 0.12, 0, 0.25, 0.01, 0.4),
            TerrainType.DryFertile => new TerrainParameters(1.1, 85, 0.5, 0.04, 0.05, 0, .8),
            TerrainType.Grass => new TerrainParameters(1, 85, 0.5, 0.08, 0.05, 0, 1),
            TerrainType.Forest => new TerrainParameters(1.5, 75, 0.35, 0.7, 0.04, 0, 1.8),
            TerrainType.Mountain => new TerrainParameters(double.PositiveInfinity, 5, 0, 0, 0.8, 0.5, 1.4),
            TerrainType.Snow => new TerrainParameters(1.8, 8, 0.08, 0, 0.4, 0.25, 0.8),
            TerrainType.Hills => new TerrainParameters(1.6, 45, 0.2, 0.15, 0.6, 0.35, 1.3),
            TerrainType.Wetland => new TerrainParameters(2, 95, 0.65, 0.22, 0.02, 0, 2),
            TerrainType.Desert => new TerrainParameters(1.7, 8, 0.05, 0, 0.3, 0.12, 0.35),
            TerrainType.River => new TerrainParameters(double.PositiveInfinity, 80, 0.7, 0, 0.2, 0.03, 1.7),
            TerrainType.Tundra => new TerrainParameters(1.8, 20, 0.13, 0.08, 0.3, 0.15, 0.7),
            TerrainType.Stream => new TerrainParameters(2.5, 75, .3, 0, .1, 0, 1.5),
            TerrainType.LargeRiver => new TerrainParameters(double.PositiveInfinity, 80, .7, 0, .2, .03, 1.7),
            TerrainType.Meadow => new TerrainParameters(1, 90, .65, .05, .04, 0, 1.1),
            TerrainType.Woodland => new TerrainParameters(1.3, 65, .4, .45, .08, 0, 1.5),
            TerrainType.Rainforest => new TerrainParameters(2, 85, .5, .9, .03, 0, 2.2),
            TerrainType.Savanna => new TerrainParameters(1.2, 45, .28, .08, .08, 0, .8),
            TerrainType.Scrub => new TerrainParameters(1.4, 25, .12, .12, .25, .08, .6),
            TerrainType.Floodplain => new TerrainParameters(1.2, 95, .7, .12, .03, 0, 1.4),
            TerrainType.AlpineMeadow => new TerrainParameters(1.7, 50, .3, .08, .45, .2, 1.1),
            _ => new TerrainParameters(1, 50, 0.2, 0.1, 0.1, 0, 1),
        };
    }

    public static byte Fertility(TerrainType terrain)
    {
        return For(terrain).Fertility;
    }

    public static double MovementCost(TerrainType terrain)
    {
        return For(terrain).MovementCost;
    }
}
