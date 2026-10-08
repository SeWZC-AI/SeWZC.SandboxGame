namespace SeWZC.WorldBox.Core;

public sealed partial record Advancement
{
    private static readonly IReadOnlyDictionary<int, Advancement> ById;

    static Advancement()
    {
        ById = All.ToDictionary(research => research.Id);
    }

    /// <summary>农业改良。</summary>
    public static Advancement Agriculture { get; } = new(0, nameof(Agriculture), "农业改良", ResearchBranch.Resources,
        "基础", false, [], new ResourceAmounts { Food = 20, Wood = 15 }, 60, "农场实际采收增加 35%，解锁现场驯养、投喂和繁殖的牧场。",
        [BuildingKind.Pasture]);

    /// <summary>驿路运输。</summary>
    public static Advancement Logistics { get; } = new(1, nameof(Logistics), "驿路运输", ResearchBranch.Resources,
        "基础", false, [], new ResourceAmounts { Food = 20, Wood = 20, Stone = 10 }, 60, "解锁驿站、桥梁、山路、船坞与码头。",
        [
            BuildingKind.Waystation, BuildingKind.Bridge, BuildingKind.MountainPass, BuildingKind.Shipyard,
            BuildingKind.Dock,
        ]);

    /// <summary>灌溉农学。</summary>
    public static Advancement Irrigation { get; } = new(12, nameof(Irrigation), "灌溉农学", ResearchBranch.Resources,
        "专业", false, [Agriculture], new ResourceAmounts { Food = 25, Wood = 15, Stone = 10 }, 90,
        "农场及进阶粮食设施实际产量增加 25%；仍受当地肥力与干旱约束。");

    /// <summary>林业与勘采。</summary>
    public static Advancement Forestry { get; } = new(13, nameof(Forestry), "林业与勘采", ResearchBranch.Resources,
        "专业", false, [Logistics], new ResourceAmounts { Food = 25, Wood = 10, Stone = 10 }, 90,
        "实地采集木材、石材、矿石与阶段矿藏的效率增加 25%。");

    /// <summary>公共医学。</summary>
    public static Advancement Medicine { get; } = new(14, nameof(Medicine), "公共医学", ResearchBranch.Resources, "专业",
        false, [Agriculture], new ResourceAmounts { Food = 30, Wood = 10, Ore = 5 }, 90, "医务所现场治疗效率增加 50%。");

    /// <summary>实验科学。</summary>
    public static Advancement ScientificMethod { get; } = new(15, nameof(ScientificMethod), "实验科学",
        ResearchBranch.Industry, "启蒙", false, [Agriculture, Logistics],
        new ResourceAmounts { Food = 30, Wood = 15, Ore = 8 }, 100, "当地学舍实际研究效率增加 25%。");

    /// <summary>工业冶炼。</summary>
    public static Advancement Industry { get; } = new(4, nameof(Industry), "工业冶炼", ResearchBranch.Industry, "工业",
        false, [Agriculture, Logistics], new ResourceAmounts { Food = 35, Wood = 25, Ore = 15 }, 180, null,
        [BuildingKind.Foundry, BuildingKind.Aquaculture]);

    /// <summary>精炼冶金。</summary>
    public static Advancement EfficientSmelting { get; } = new(16, nameof(EfficientSmelting), "精炼冶金",
        ResearchBranch.Industry, "工业", false, [Industry], new ResourceAmounts { Food = 35, Alloy = 5, Ore = 10 },
        120, "冶炼厂与矮人锻炉每批合金产量增加 25%，原料仍须实际运输。");

    /// <summary>电气化。</summary>
    public static Advancement Electrification { get; } = new(5, nameof(Electrification), "电气化",
        ResearchBranch.Industry, "近现代", false, [Industry], new ResourceAmounts { Food = 40, Alloy = 12, Ore = 15 },
        180, null, [BuildingKind.PowerPlant]);

    /// <summary>能源循环。</summary>
    public static Advancement EnergyRecycling { get; } = new(17, nameof(EnergyRecycling), "能源循环",
        ResearchBranch.Industry, "近现代", false, [Electrification],
        new ResourceAmounts { Food = 35, Alloy = 8, EnergyCells = 8 }, 120, "动力工厂每批动力单元产量增加 50%。");

    /// <summary>信号网络。</summary>
    public static Advancement SignalNetwork { get; } = new(2, nameof(SignalNetwork), "信号网络",
        ResearchBranch.Transport, "近现代", false, [Electrification],
        new ResourceAmounts { Food = 25, Alloy = 8, EnergyCells = 6 }, 100, "解锁无线信号塔通信，仍须实际值守与视线连通。");

    /// <summary>航空运输。</summary>
    public static Advancement Aviation { get; } = new(11, nameof(Aviation), "航空运输", ResearchBranch.Transport, "航空",
        false, [Electrification, SignalNetwork], new ResourceAmounts { Food = 40, Alloy = 15, EnergyCells = 10 },
        180, null, [BuildingKind.Airfield]);

    /// <summary>自动化农业。</summary>
    public static Advancement Automation { get; } = new(6, nameof(Automation), "自动化农业", ResearchBranch.Transport,
        "自动化", false, [Electrification], new ResourceAmounts { Food = 50, Alloy = 20, EnergyCells = 20 }, 180,
        null, [BuildingKind.AutomatedFarm]);

    /// <summary>先进计算与制造。</summary>
    public static Advancement AdvancedComputing { get; } = new(7, nameof(AdvancedComputing), "先进计算与制造",
        ResearchBranch.Transport, "未来制造", false, [Automation],
        new ResourceAmounts { Food = 60, Alloy = 30, EnergyCells = 30 }, 180, null, [BuildingKind.Fabricator]);

    /// <summary>奥术基础。</summary>
    public static Advancement ArcaneArts { get; } = new(3, nameof(ArcaneArts), "奥术基础", ResearchBranch.Restoration,
        "基础奥术", true, [], new ResourceAmounts { Food = 25, Wood = 15, Ore = 8 }, 100,
        "解锁奥术圣所训练，合格施法者可使用治疗、丰饶、护盾与火花。");

    /// <summary>魔力调谐。</summary>
    public static Advancement ManaAttunement { get; } = new(18, nameof(ManaAttunement), "魔力调谐",
        ResearchBranch.Restoration, "奥术", true, [ArcaneArts],
        new ResourceAmounts { Food = 30, Stone = 10, Ore = 5 }, 100, "当地居民实际魔力恢复速度增加 50%。");

    /// <summary>生命修复。</summary>
    public static Advancement Restoration { get; } = new(19, nameof(Restoration), "生命修复",
        ResearchBranch.Restoration, "奥术", true, [ManaAttunement, Medicine],
        new ResourceAmounts { Food = 35, Stone = 15, Ore = 8 }, 120, "治疗法术的实际生命恢复增加 50%；消耗个人魔力并受距离约束。");

    /// <summary>晶体凝炼。</summary>
    public static Advancement Crystalcraft { get; } = new(8, nameof(Crystalcraft), "晶体凝炼", ResearchBranch.Runes,
        "晶体魔法", true, [ArcaneArts], new ResourceAmounts { Food = 30, Stone = 15, Ore = 12 }, 180, null,
        [BuildingKind.Crystallizer]);

    /// <summary>奥术传承。</summary>
    public static Advancement ArcaneScholarship { get; } = new(20, nameof(ArcaneScholarship), "奥术传承",
        ResearchBranch.Restoration, "晶体魔法", true, [Crystalcraft],
        new ResourceAmounts { Food = 35, Stone = 15, Crystals = 5 }, 120, "当地研究效率增加 25%，圣所训练效率增加 50%。");

    /// <summary>符文生产。</summary>
    public static Advancement RunicEngineering { get; } = new(9, nameof(RunicEngineering), "符文生产",
        ResearchBranch.Runes, "符文文明", true, [Crystalcraft, Logistics],
        new ResourceAmounts { Food = 40, Stone = 20, Crystals = 12 }, 180, null, [BuildingKind.RunicGarden]);

    /// <summary>地脉共鸣。</summary>
    public static Advancement Leylines { get; } = new(21, nameof(Leylines), "地脉共鸣", ResearchBranch.Runes, "符文文明",
        true, [RunicEngineering, ManaAttunement], new ResourceAmounts { Food = 40, Stone = 20, Crystals = 10 },
        150, "魔晶凝炼室及以太转化炉每批实际产量增加 25%。");

    /// <summary>高阶以太工艺。</summary>
    public static Advancement AetherMastery { get; } = new(10, nameof(AetherMastery), "高阶以太工艺",
        ResearchBranch.Runes, "以太文明", true, [RunicEngineering],
        new ResourceAmounts { Food = 60, Stone = 30, Crystals = 25 }, 180, null, [BuildingKind.AetherForge]);

    /// <summary>土木水利。</summary>
    public static Advancement CivilEngineering { get; } = new(22, nameof(CivilEngineering), "土木水利",
        ResearchBranch.PublicHealth, "城建", false, [Irrigation, Logistics],
        new ResourceAmounts { Food = 30, Wood = 15, Stone = 15 }, 100, "解锁蓄水站。水务工人到河湖岸边取水，装入背包后实际返仓。",
        [BuildingKind.Reservoir]);

    /// <summary>公共教育。</summary>
    public static Advancement Education { get; } = new(26, nameof(Education), "公共教育", ResearchBranch.Survey, "教育",
        false, [Logistics], new ResourceAmounts { Food = 30, Wood = 20 }, 100,
        "解锁图书馆与文献师，现场传授当地已有研究；知识仍需人员或通信送达异地。", [BuildingKind.Library], [Profession.Archivist]);

    /// <summary>药物制备。</summary>
    public static Advancement Pharmacology { get; } = new(24, nameof(Pharmacology), "药物制备",
        ResearchBranch.PublicHealth, "公共卫生", false, [Medicine, Education],
        new ResourceAmounts { Food = 35, Wood = 15, Ore = 5 }, 180, null, [BuildingKind.Apothecary]);

    /// <summary>临床与防疫。</summary>
    public static Advancement Sanitation { get; } = new(23, nameof(Sanitation), "临床与防疫",
        ResearchBranch.PublicHealth, "公共卫生", false, [Pharmacology, CivilEngineering],
        new ResourceAmounts { Food = 35, Wood = 15, Stone = 20 }, 120, "解锁医院和医师。医师领取真实药品，治疗现场患者并建立短期疾病免疫。",
        [BuildingKind.Hospital], [Profession.Physician]);

    /// <summary>消防工程。</summary>
    public static Advancement FireEngineering { get; } = new(25, nameof(FireEngineering), "消防工程",
        ResearchBranch.PublicHealth, "消防", false, [CivilEngineering],
        new ResourceAmounts { Food = 30, Wood = 15, Stone = 15 }, 100, "解锁消防站和消防员。补充随身用水、扑救近处火灾，并修复受损建筑。",
        [BuildingKind.FireStation], [Profession.Firefighter]);

    /// <summary>实地测绘。</summary>
    public static Advancement Cartography { get; } = new(27, nameof(Cartography), "实地测绘", ResearchBranch.Survey,
        "勘察", false, [Education, Forestry], new ResourceAmounts { Food = 30, Wood = 15, Stone = 10 }, 100,
        "解锁勘测所和测绘员。现场发现水源、城镇与危险，形成有观察时间的报告。", [BuildingKind.SurveyOffice], [Profession.Surveyor]);

    /// <summary>机械工程。</summary>
    public static Advancement MechanicalEngineering { get; } = new(28, nameof(MechanicalEngineering), "机械工程",
        ResearchBranch.Industry, "机械", false, [Industry, ScientificMethod],
        new ResourceAmounts { Food = 35, Alloy = 5, Ore = 8 }, 120, "解锁工程师。工程师携带并消耗机械工具，加快实际施工；无工具时按普通工人劳动。",
        professions: [Profession.Engineer]);

    /// <summary>机械工具。</summary>
    public static Advancement Toolmaking { get; } = new(29, nameof(Toolmaking), "机械工具", ResearchBranch.Industry,
        "机械工程", false, [MechanicalEngineering, EfficientSmelting], new ResourceAmounts { Food = 35, Alloy = 8 },
        180, null, [BuildingKind.MachineWorkshop]);

    /// <summary>弹道学。</summary>
    public static Advancement Ballistics { get; } = new(30, nameof(Ballistics), "弹道学", ResearchBranch.Military,
        "工程军备", false, [Industry, Cartography], new ResourceAmounts { Food = 35, Alloy = 8, Coal = 5 }, 180, null,
        [BuildingKind.Arsenal], [Profession.Ranger]);

    /// <summary>防护装备。</summary>
    public static Advancement ProtectiveEquipment { get; } = new(31, nameof(ProtectiveEquipment), "防护装备",
        ResearchBranch.Military, "军备", false, [Ballistics, Toolmaking],
        new ResourceAmounts { Food = 35, Alloy = 10 }, 120, "解锁装备工坊。到场居民领取实物合金制成护甲；受击时防护值逐次耗损。",
        [BuildingKind.Armory]);

    /// <summary>轨道交通。</summary>
    public static Advancement RailTransport { get; } = new(32, nameof(RailTransport), "轨道交通",
        ResearchBranch.Transport, "近现代", false, [MechanicalEngineering, Logistics],
        new ResourceAmounts { Food = 40, Alloy = 12 }, 140, "解锁铺设铁路命令：花费石材和合金，升级己方已修道路，使陆地居民沿轨道移动更快。",
        action: ResearchAction.Rail);

    /// <summary>光学观测。</summary>
    public static Advancement Observation { get; } = new(33, nameof(Observation), "光学观测",
        ResearchBranch.Communication, "近现代", false, [Cartography, SignalNetwork],
        new ResourceAmounts { Food = 35, Alloy = 8, EnergyCells = 5 }, 120,
        "勘测所观测范围由 4 格扩展至 6 格，山体仍遮挡视线；报告可经已值守信号塔传递。");

    /// <summary>元素塑形。</summary>
    public static Advancement Elementalism { get; } = new(34, nameof(Elementalism), "元素塑形", ResearchBranch.Warding,
        "元素魔法", true, [ManaAttunement], new ResourceAmounts { Food = 35, Crystals = 5 }, 120,
        "解锁寒冰箭。攻击可见且已知交战的敌人，使其短暂冻结，期间无法移动。", spells: [SpellKind.FrostBolt]);

    /// <summary>符文守卫。</summary>
    public static Advancement Warding { get; } = new(35, nameof(Warding), "符文守卫", ResearchBranch.Warding, "结界",
        true, [RunicEngineering, Restoration], new ResourceAmounts { Food = 40, Stone = 20, Crystals = 10 }, 150,
        "解锁结界塔和符文护盾。消耗个人魔力与随身魔晶，给塔旁居民施加可耗损的个人护盾。", [BuildingKind.WardTower], spells: [SpellKind.RuneWard]);

    /// <summary>炼金药剂。</summary>
    public static Advancement Alchemy { get; } = new(36, nameof(Alchemy), "炼金药剂", ResearchBranch.Restoration,
        "奥术与修复", true, [Crystalcraft, Pharmacology], new ResourceAmounts { Food = 35, Crystals = 8 }, 180, null,
        [BuildingKind.AlchemyLab]);

    /// <summary>自然契约。</summary>
    public static Advancement NatureBinding { get; } = new(37, nameof(NatureBinding), "自然契约",
        ResearchBranch.Restoration, "自然魔法", true, [Restoration, Forestry],
        new ResourceAmounts { Food = 35, Stone = 15, Crystals = 8 }, 120,
        "解锁共生林苑、园艺师和唤雨。用水、魔力恢复真实树木覆盖，雨水解除近处干旱并压制火势。", [BuildingKind.GroveSanctuary], [Profession.Gardener],
        [SpellKind.RainCall]);

    /// <summary>空间折跃。</summary>
    public static Advancement SpatialMagic { get; } = new(38, nameof(SpatialMagic), "空间折跃", ResearchBranch.Runes,
        "空间魔法", true, [AetherMastery, Leylines], new ResourceAmounts { Food = 50, Stone = 25, Crystals = 20 }, 180,
        "解锁折跃门与传送命令。两座同国、可运营的门相距至多 24 格；本人到门旁，消耗 30 魔力和 2 份随身魔晶，连同背包抵达目标门。", [BuildingKind.Waygate],
        action: ResearchAction.Waygate);

    /// <summary>战场法术。</summary>
    public static Advancement BattleMagic { get; } = new(39, nameof(BattleMagic), "战场法术", ResearchBranch.Warding,
        "战斗魔法", true, [Elementalism, Warding], new ResourceAmounts { Food = 45, Stone = 20, Crystals = 15 }, 160,
        "解锁风暴尖塔、战斗法师和连锁闪电。需要现场施法者、实际魔力和魔晶，仅攻击已知敌军。", [BuildingKind.StormSpire], [Profession.Battlemage],
        [SpellKind.ChainLightning]);

    /// <summary>按前置依赖顺序排列的全部研究。</summary>
    public static IReadOnlyList<Advancement> All { get; } = Array.AsReadOnly<Advancement>(
    [
        Agriculture,
        Logistics,
        Irrigation,
        Forestry,
        Medicine,
        ScientificMethod,
        Industry,
        EfficientSmelting,
        Electrification,
        EnergyRecycling,
        SignalNetwork,
        Aviation,
        Automation,
        AdvancedComputing,
        ArcaneArts,
        ManaAttunement,
        Restoration,
        Crystalcraft,
        ArcaneScholarship,
        RunicEngineering,
        Leylines,
        AetherMastery,
        CivilEngineering,
        Education,
        Pharmacology,
        Sanitation,
        FireEngineering,
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
    ]);

    /// <summary>按存档或知识消息中的编号查找研究，无对应知识时返回空值。</summary>
    public static Advancement? Find(int id)
    {
        return ById.GetValueOrDefault(id);
    }
}
