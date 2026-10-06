using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

/// <summary>生产、服务、交通及种族专属设施的类别。</summary>
public enum BuildingKind
{
    /// <summary>农场。</summary>
    Farm,
    /// <summary>工坊。</summary>
    Workshop,
    /// <summary>学舍。</summary>
    Academy,
    /// <summary>驿站。</summary>
    Waystation,
    /// <summary>无线信号塔。</summary>
    SignalTower,
    /// <summary>奥术圣所。</summary>
    ArcaneSanctum,
    /// <summary>医馆。</summary>
    Infirmary,
    /// <summary>冶炼厂。</summary>
    Foundry,
    /// <summary>动力工厂。</summary>
    PowerPlant,
    /// <summary>自动化农场。</summary>
    AutomatedFarm,
    /// <summary>精密制造中心。</summary>
    Fabricator,
    /// <summary>魔晶凝炼室。</summary>
    Crystallizer,
    /// <summary>符文温室。</summary>
    RunicGarden,
    /// <summary>以太转化炉。</summary>
    AetherForge,
    /// <summary>山路。</summary>
    MountainPass,
    /// <summary>桥梁。</summary>
    Bridge,
    /// <summary>码头。</summary>
    Dock,
    /// <summary>航空工场。</summary>
    Airfield,
    /// <summary>城镇中心。</summary>
    TownCenter,
    /// <summary>船坞。</summary>
    Shipyard,
    /// <summary>伐木营地。</summary>
    LumberCamp,
    /// <summary>采石场。</summary>
    Quarry,
    /// <summary>水井。</summary>
    Well,
    /// <summary>粮仓。</summary>
    Granary,
    /// <summary>住宅。</summary>
    Housing,
    /// <summary>集市。</summary>
    Market,
    /// <summary>瞭望塔。</summary>
    Watchtower,
    /// <summary>议事大厅。</summary>
    AssemblyHall,
    /// <summary>贸易行会。</summary>
    TradeGuild,
    /// <summary>精灵圣林。</summary>
    SacredGrove,
    /// <summary>草药园。</summary>
    HerbGarden,
    /// <summary>矮人锻炉。</summary>
    DwarvenForge,
    /// <summary>矿业大厅。</summary>
    MiningHall,
    /// <summary>狩猎营地。</summary>
    HuntingCamp,
    /// <summary>战鼓台。</summary>
    WarDrum,
    /// <summary>蓄水池。</summary>
    Reservoir,
    /// <summary>医院。</summary>
    Hospital,
    /// <summary>药房。</summary>
    Apothecary,
    /// <summary>消防站。</summary>
    FireStation,
    /// <summary>图书馆。</summary>
    Library,
    /// <summary>勘测所。</summary>
    SurveyOffice,
    /// <summary>机械工场。</summary>
    MachineWorkshop,
    /// <summary>军械厂。</summary>
    Arsenal,
    /// <summary>护甲工坊。</summary>
    Armory,
    /// <summary>炼金实验室。</summary>
    AlchemyLab,
    /// <summary>结界塔。</summary>
    WardTower,
    /// <summary>雷霆尖塔。</summary>
    StormSpire,
    /// <summary>共生林地。</summary>
    GroveSanctuary,
    /// <summary>折跃门。</summary>
    Waygate,
    /// <summary>牧场。</summary>
    Pasture,
    /// <summary>水产养殖厂。</summary>
    Aquaculture,
}

/// <summary>聚落可以研究或通过通信掌握的知识项目。</summary>
public enum ResearchKind
{
    /// <summary>农业。</summary>
    Agriculture,
    /// <summary>驿路运输。</summary>
    Logistics,
    /// <summary>信号网络。</summary>
    SignalNetwork,
    /// <summary>奥术基础。</summary>
    ArcaneArts,
    /// <summary>工业冶炼。</summary>
    Industry,
    /// <summary>电气化。</summary>
    Electrification,
    /// <summary>自动化农业。</summary>
    Automation,
    /// <summary>先进计算与制造。</summary>
    AdvancedComputing,
    /// <summary>晶体凝炼。</summary>
    Crystalcraft,
    /// <summary>符文生产。</summary>
    RunicEngineering,
    /// <summary>高阶以太工艺。</summary>
    AetherMastery,
    /// <summary>航空运输。</summary>
    Aviation,
    /// <summary>灌溉。</summary>
    Irrigation,
    /// <summary>林业。</summary>
    Forestry,
    /// <summary>医学。</summary>
    Medicine,
    /// <summary>科学方法。</summary>
    ScientificMethod,
    /// <summary>高效冶炼。</summary>
    EfficientSmelting,
    /// <summary>能源回收。</summary>
    EnergyRecycling,

    // 编号 18 和 23 曾误用于文明结果，保留这些编号以避免混淆研究含义。
    /// <summary>魔力协调。</summary>
    ManaAttunement = 19,
    /// <summary>修复法术。</summary>
    Restoration,
    /// <summary>奥术学术。</summary>
    ArcaneScholarship,
    /// <summary>地脉网络。</summary>
    Leylines,
    /// <summary>土木工程。</summary>
    CivilEngineering = 24,
    /// <summary>公共卫生。</summary>
    Sanitation,
    /// <summary>药物制备。</summary>
    Pharmacology,
    /// <summary>消防工程。</summary>
    FireEngineering,
    /// <summary>教育。</summary>
    Education,
    /// <summary>制图学。</summary>
    Cartography,
    /// <summary>机械工程。</summary>
    MechanicalEngineering,
    /// <summary>机械工具。</summary>
    Toolmaking,
    /// <summary>弹道学。</summary>
    Ballistics,
    /// <summary>个人防护。</summary>
    ProtectiveEquipment,
    /// <summary>铁路运输。</summary>
    RailTransport,
    /// <summary>观测学。</summary>
    Observation,
    /// <summary>元素法术。</summary>
    Elementalism,
    /// <summary>结界学。</summary>
    Warding,
    /// <summary>炼金药剂。</summary>
    Alchemy,
    /// <summary>自然共生。</summary>
    NatureBinding,
    /// <summary>空间魔法。</summary>
    SpatialMagic,
    /// <summary>战斗魔法。</summary>
    BattleMagic,
}

/// <summary>国家组织议事和处理报告的制度形式。</summary>
public enum InstitutionKind
{
    /// <summary>议事会。</summary>
    Council,
    /// <summary>君主制。</summary>
    Monarchy,
    /// <summary>行会议会。</summary>
    GuildCouncil,
}

/// <summary>聚落生产与公共事务的政策方向。</summary>
public enum PolicyKind
{
    /// <summary>均衡发展。</summary>
    Balanced,
    /// <summary>粮食保障。</summary>
    FoodSecurity,
    /// <summary>防御。</summary>
    Defense,
    /// <summary>学术。</summary>
    Scholarship,
    /// <summary>公共健康。</summary>
    PublicHealth,
}

/// <summary>居民可施放的治疗、增益、战斗和环境法术。</summary>
public enum SpellKind
{
    /// <summary>治疗。</summary>
    Heal,
    /// <summary>丰饶祝福。</summary>
    HarvestBlessing,
    /// <summary>护盾。</summary>
    Shield,
    /// <summary>火焰攻击。</summary>
    Ember,
    /// <summary>冰霜弹。</summary>
    FrostBolt,
    /// <summary>连锁闪电。</summary>
    ChainLightning,
    /// <summary>呼雨。</summary>
    RainCall,
    /// <summary>符文护甲。</summary>
    RuneWard,
}

/// <summary>需要保存的文化、设施、本地研究、制度，以及机构实际收到的报告。</summary>
public sealed class SocietyState
{
    /// <summary>是否允许新的魔法发展和施法。</summary>
    public bool MagicEnabled { get; set; } = true;
    /// <summary>独立的文化定义集合。</summary>
    public List<CultureDefinition> Cultures { get; set; } = [];
    /// <summary>实际设施及其施工、生产和服务状态。</summary>
    public List<Building> Buildings { get; set; } = [];
    /// <summary>各聚落掌握的研究和进行中的项目。</summary>
    public List<SettlementResearch> Research { get; set; } = [];
    /// <summary>各聚落当前政策及决策依据。</summary>
    public List<LocalPolicy> Policies { get; set; } = [];
    /// <summary>各国家的制度和决策记录。</summary>
    public List<NationInstitution> Institutions { get; set; } = [];
    /// <summary>机构实际收到的报告，是制度决策使用的报告集合。</summary>
    public List<InstitutionReport> Reports { get; set; } = [];
    /// <summary>居民接触不同文化的记录。</summary>
    public List<CulturalContact> CulturalContacts { get; set; } = [];
}

/// <summary>独立于种族和国家的文化名称及合作、创新和亲自然权重。</summary>
public sealed class CultureDefinition
{
    /// <summary>文化的稳定 ID。</summary>
    public int Id { get; set; }
    /// <summary>文化的显示名称。</summary>
    public string Name { get; set; } = "";
    /// <summary>合作倾向权重，范围为 0 至 1。</summary>
    public double Cooperation { get; set; } = 0.5;
    /// <summary>创新倾向权重，范围为 0 至 1。</summary>
    public double Innovation { get; set; } = 0.5;
    /// <summary>亲自然倾向权重，范围为 0 至 1。</summary>
    public double NatureAffinity { get; set; } = 0.5;
}

/// <summary>实际设施的归属、施工、生命值、劳动及生产或服务记录。</summary>
public sealed partial class Building
{
    /// <summary>牧场或养殖厂实际饲养的动物物种。</summary>
    [JsonRequired]
    public WildlifeKind LivestockKind { get; set; }

    /// <summary>设施内实际饲养的动物数量。</summary>
    [JsonRequired]
    public double LivestockPopulation { get; set; }

    /// <summary>设施已经完成的生产批次数。</summary>
    [JsonRequired]
    public int ProductionBatches { get; set; }

    /// <summary>设施已经完成的服务次数。</summary>
    [JsonRequired]
    public int ServiceActions { get; set; }

    /// <summary>最近一次提供服务的模拟日序。</summary>
    [JsonRequired]
    public long LastServiceTick { get; set; } = -100;

    /// <summary>是否允许设施运营，关闭后保留建筑本身。</summary>
    [JsonRequired]
    public bool Enabled { get; set; } = true;

    /// <summary>本轮施工的事件、贡献者和进度采样记录。</summary>
    public ProjectObservation Observation { get; set; } = new();
    /// <summary>建筑的稳定 ID。</summary>
    public int Id { get; set; }
    /// <summary>关联或归属聚落的稳定 ID。</summary>
    public int SettlementId { get; set; }
    /// <summary>设施类别。</summary>
    public BuildingKind Kind { get; set; }
    /// <summary>所在地点的横向地格坐标。</summary>
    public int X { get; set; }
    /// <summary>所在地点的纵向地格坐标。</summary>
    public int Y { get; set; }
    /// <summary>已经累计的建造施工量。</summary>
    public double ConstructionProgress { get; set; }
    /// <summary>完成建造所需的总施工量。</summary>
    public double ConstructionRequired { get; set; } = 30;
    /// <summary>当前建筑生命值，影响存续和运营。</summary>
    public double Health { get; set; } = 100;
    /// <summary>同时容纳的劳动岗位数。</summary>
    public int WorkSlots { get; set; } = 3;
    /// <summary>当前登记的工人居民 ID。</summary>
    public List<int> Workers { get; set; } = [];
    /// <summary>最近一次居民在此劳动的模拟日序。</summary>
    public long LastWorkedTick { get; set; } = -100;

    /// <summary>建造进度是否达标且建筑仍有生命值。</summary>
    [JsonIgnore]
    public bool IsCompleted => ConstructionProgress >= ConstructionRequired && Health > 0;
}

/// <summary>聚落已掌握的研究及当前项目进度，包含从外地收到的知识。</summary>
public sealed class SettlementResearch
{
    /// <summary>当前研究项目的事件、贡献者和进度采样记录。</summary>
    public ProjectObservation Observation { get; set; } = new();
    /// <summary>最近一次完成研究关联的事件 ID。</summary>
    public int LastCompletionEventId { get; set; }
    /// <summary>关联或归属聚落的稳定 ID。</summary>
    public int SettlementId { get; set; }
    /// <summary>当前研究项目，空值表示没有进行中的项目。</summary>
    public ResearchKind? ActiveProject { get; set; }
    /// <summary>当前研究已经累计的工作量。</summary>
    public double Progress { get; set; }
    /// <summary>完成当前研究所需的总工作量。</summary>
    public double RequiredProgress { get; set; }
    /// <summary>本聚落已经研究完成或通过递送掌握的知识。</summary>
    public List<ResearchKind> Completed { get; set; } = [];
}

/// <summary>聚落选定的政策及其依据，也可由玩家覆盖选择。</summary>
public sealed class LocalPolicy
{
    /// <summary>关联或归属聚落的稳定 ID。</summary>
    public int SettlementId { get; set; }
    /// <summary>当前采用的本地政策。</summary>
    public PolicyKind Kind { get; set; }
    /// <summary>当前政策是否由玩家指定。</summary>
    public bool PlayerOverride { get; set; }
    /// <summary>本次政策决定的模拟日序。</summary>
    public long DecidedTick { get; set; }
    /// <summary>选择此政策的实际理由。</summary>
    public string Reason { get; set; } = "尚无递送议题，维持均衡政策";
    /// <summary>政策决定采用的信息依据 ID。</summary>
    public int EvidenceFactId { get; set; }
    /// <summary>该信息依据最初观察发生的日序。</summary>
    public long EvidenceObservedTick { get; set; }
}

/// <summary>国家的制度形式、可选的玩家指定政策及最近一次制度决策。</summary>
public sealed class NationInstitution
{
    /// <summary>关联或归属国家的稳定 ID。</summary>
    public int NationId { get; set; }
    /// <summary>当前国家制度形式。</summary>
    public InstitutionKind Kind { get; set; }
    /// <summary>玩家指定的政策，空值表示由机构自主决策。</summary>
    public PolicyKind? PlayerPolicy { get; set; }
    /// <summary>最近一次制度决策的说明。</summary>
    public string LastDecision { get; set; } = "等待本地议事及代表送达的报告";
    /// <summary>最近一次制度决策的模拟日序。</summary>
    public long LastDecisionTick { get; set; }
}

/// <summary>聚落机构已收到的议题，保留最初观察者、其当时职业和观察时间。</summary>
public sealed class InstitutionReport
{
    /// <summary>关联的世界事件 ID，0 表示未关联事件。</summary>
    public int EventId { get; set; }
    /// <summary>实际接收报告的聚落 ID。</summary>
    public int RecipientSettlementId { get; set; }
    /// <summary>报告所依据的信息记录 ID。</summary>
    public int FactId { get; set; }
    /// <summary>最初观察者的居民 ID。</summary>
    public int OriginResidentId { get; set; }
    /// <summary>将报告递交给机构的代表居民 ID。</summary>
    public int RepresentativeId { get; set; }
    /// <summary>最初观察者当时的职业，用于议题权重计算。</summary>
    public Profession ReportedProfession { get; set; }
    /// <summary>报告的议题类别。</summary>
    public AgentFactKind Topic { get; set; }
    /// <summary>议题关联主体的 ID，含义由议题类别决定。</summary>
    public int SubjectId { get; set; }
    /// <summary>议题的观测值，含义由议题类别决定。</summary>
    public double Value { get; set; }
    /// <summary>报告的可信度。</summary>
    public double Confidence { get; set; }
    /// <summary>最初观察发生的模拟日序。</summary>
    public long ObservedTick { get; set; }
    /// <summary>机构实际收到报告的模拟日序。</summary>
    public long ReceivedTick { get; set; }
}

/// <summary>居民接触某种文化的累计程度和最近接触时间。</summary>
public sealed class CulturalContact
{
    /// <summary>关联居民的稳定 ID。</summary>
    public int ResidentId { get; set; }
    /// <summary>所接触文化的稳定 ID。</summary>
    public int CultureId { get; set; }
    /// <summary>居民接触该文化的累计程度。</summary>
    public double Exposure { get; set; }
    /// <summary>最近接触该文化的模拟日序。</summary>
    public long LastContactTick { get; set; }
}

/// <summary>地形的基础通行成本、肥力、采集产量和魔力恢复参数。</summary>
/// <param name="MovementCost">基础步行成本，正无穷表示不可步行。</param>
/// <param name="Fertility">生成地形时的基础肥力。</param>
/// <param name="FoodYield">基础食物采集产量系数。</param>
/// <param name="WoodYield">基础木材采集产量系数。</param>
/// <param name="StoneYield">基础石材采集产量系数。</param>
/// <param name="OreYield">基础矿石采集产量系数。</param>
/// <param name="ManaRate">本地魔力恢复速率系数。</param>
public readonly record struct TerrainParameters(
    double MovementCost,
    byte Fertility,
    double FoodYield,
    double WoodYield,
    double StoneYield,
    double OreYield,
    double ManaRate);

/// <summary>提供采集、移动和本地魔法共用的地形基础参数。</summary>
public static class TerrainRules
{
    /// <summary>返回地形的基础通行、肥力、采集和魔力参数。</summary>
    /// <param name="terrain">待查询或设置的地形类别。</param>
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

    /// <summary>返回地形的基础肥力。</summary>
    /// <param name="terrain">待查询或设置的地形类别。</param>
    public static byte Fertility(TerrainType terrain)
    {
        return For(terrain).Fertility;
    }

    /// <summary>返回地形基础步行成本，不可步行时为正无穷。</summary>
    /// <param name="terrain">待查询或设置的地形类别。</param>
    public static double MovementCost(TerrainType terrain)
    {
        return For(terrain).MovementCost;
    }
}
