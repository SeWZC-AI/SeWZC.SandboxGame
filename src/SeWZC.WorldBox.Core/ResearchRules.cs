namespace SeWZC.WorldBox.Core;

/// <summary>研究项目目录及知识解锁规则。</summary>
public static class ResearchRules
{
    private static readonly IReadOnlyDictionary<ResearchKind, ResearchDefinition> ByKind;
    private static readonly IReadOnlyList<ResearchKind> TechnologyRoute;
    private static readonly IReadOnlyList<ResearchKind> MagicRoute;
    private static readonly IReadOnlyDictionary<BuildingKind, ResearchDefinition> ByBuilding;
    private static readonly IReadOnlyDictionary<Profession, ResearchDefinition> ByProfession;
    private static readonly IReadOnlyDictionary<SpellKind, ResearchDefinition> BySpell;

    static ResearchRules()
    {
        // 索引与路线必须等待知识图谱初始化完成。
        ByKind = All.ToDictionary(r => r.Kind);
        TechnologyRoute = Array.AsReadOnly(All.Where(r => !r.Magic || r.Shared).Select(r => r.Kind).ToArray());
        MagicRoute = Array.AsReadOnly(All.Where(r => r.Magic || r.Shared).Select(r => r.Kind).ToArray());
        ByBuilding = All.SelectMany(r => r.UnlockedBuildings.Select(k => (Kind: k, Definition: r)))
            .ToDictionary(p => p.Kind, p => p.Definition);
        ByProfession = All.SelectMany(r => r.UnlockedProfessions.Select(k => (Kind: k, Definition: r)))
            .ToDictionary(p => p.Kind, p => p.Definition);
        BySpell = All.SelectMany(r => r.UnlockedSpells.Select(k => (Kind: k, Definition: r)))
            .ToDictionary(p => p.Kind, p => p.Definition);
    }

    /// <summary>全部研究节点的定义。</summary>
    public static IReadOnlyList<ResearchDefinition> All { get; } = Array.AsReadOnly(new[]
    {
        new ResearchDefinition(ResearchKind.Agriculture, "农业改良", ResearchBranch.Resources, "基础", false, [],
            new ResourceAmounts { Food = 20, Wood = 15 }, 60, "农场实际采收增加 35%，解锁现场驯养、投喂和繁殖的牧场。", [BuildingKind.Pasture]),
        new(ResearchKind.Logistics, "驿路运输", ResearchBranch.Resources, "基础", false, [],
            new ResourceAmounts { Food = 20, Wood = 20, Stone = 10 }, 60, "解锁驿站、桥梁、山路、船坞与码头。",
            [
                BuildingKind.Waystation, BuildingKind.Bridge, BuildingKind.MountainPass, BuildingKind.Shipyard,
                BuildingKind.Dock,
            ]),
        new(ResearchKind.Irrigation, "灌溉农学", ResearchBranch.Resources, "专业", false, [ResearchKind.Agriculture],
            new ResourceAmounts { Food = 25, Wood = 15, Stone = 10 }, 90, "农场及进阶粮食设施实际产量增加 25%；仍受当地肥力与干旱约束。"),
        new(ResearchKind.Forestry, "林业与勘采", ResearchBranch.Resources, "专业", false, [ResearchKind.Logistics],
            new ResourceAmounts { Food = 25, Wood = 10, Stone = 10 }, 90, "实地采集木材、石材、矿石与阶段矿藏的效率增加 25%。"),
        new(ResearchKind.Medicine, "公共医学", ResearchBranch.Resources, "专业", false, [ResearchKind.Agriculture],
            new ResourceAmounts { Food = 30, Wood = 10, Ore = 5 }, 90, "医务所现场治疗效率增加 50%。"),
        new(ResearchKind.ScientificMethod, "实验科学", ResearchBranch.Industry, "启蒙", false,
            [ResearchKind.Agriculture, ResearchKind.Logistics], new ResourceAmounts { Food = 30, Wood = 15, Ore = 8 },
            100, "当地学舍实际研究效率增加 25%。"),
        From(ResearchKind.Industry, ResearchBranch.Industry), new(ResearchKind.EfficientSmelting, "精炼冶金",
            ResearchBranch.Industry, "工业", false,
            [ResearchKind.Industry],
            new ResourceAmounts { Food = 35, Alloy = 5, Ore = 10 }, 120, "冶炼厂与矮人锻炉每批合金产量增加 25%，原料仍须实际运输。"),
        From(ResearchKind.Electrification, ResearchBranch.Industry), new(ResearchKind.EnergyRecycling, "能源循环",
            ResearchBranch.Industry, "近现代", false,
            [ResearchKind.Electrification],
            new ResourceAmounts { Food = 35, Alloy = 8, EnergyCells = 8 }, 120, "动力工厂每批动力单元产量增加 50%。"),
        new(ResearchKind.SignalNetwork, "信号网络", ResearchBranch.Transport, "近现代", false, [ResearchKind.Electrification],
            new ResourceAmounts { Food = 25, Alloy = 8, EnergyCells = 6 }, 100, "解锁无线信号塔通信，仍须实际值守与视线连通。"),
        From(ResearchKind.Aviation, ResearchBranch.Transport), From(ResearchKind.Automation, ResearchBranch.Transport),
        From(ResearchKind.AdvancedComputing, ResearchBranch.Transport), new(ResearchKind.ArcaneArts, "奥术基础",
            ResearchBranch.Restoration, "基础奥术", true, [],
            new ResourceAmounts { Food = 25, Wood = 15, Ore = 8 }, 100, "解锁奥术圣所训练，合格施法者可使用治疗、丰饶、护盾与火花。"),
        new(ResearchKind.ManaAttunement, "魔力调谐", ResearchBranch.Restoration, "奥术", true, [ResearchKind.ArcaneArts],
            new ResourceAmounts { Food = 30, Stone = 10, Ore = 5 }, 100, "当地居民实际魔力恢复速度增加 50%。"),
        new(ResearchKind.Restoration, "生命修复", ResearchBranch.Restoration, "奥术", true,
            [ResearchKind.ManaAttunement, ResearchKind.Medicine],
            new ResourceAmounts { Food = 35, Stone = 15, Ore = 8 }, 120, "治疗法术的实际生命恢复增加 50%；消耗个人魔力并受距离约束。"),
        From(ResearchKind.Crystalcraft, ResearchBranch.Runes), new(ResearchKind.ArcaneScholarship, "奥术传承",
            ResearchBranch.Restoration, "晶体魔法", true,
            [ResearchKind.Crystalcraft],
            new ResourceAmounts { Food = 35, Stone = 15, Crystals = 5 }, 120, "当地研究效率增加 25%，圣所训练效率增加 50%。"),
        From(ResearchKind.RunicEngineering, ResearchBranch.Runes), new(ResearchKind.Leylines, "地脉共鸣",
            ResearchBranch.Runes, "符文文明", true,
            [ResearchKind.RunicEngineering, ResearchKind.ManaAttunement],
            new ResourceAmounts { Food = 40, Stone = 20, Crystals = 10 }, 150, "魔晶凝炼室及以太转化炉每批实际产量增加 25%。"),
        From(ResearchKind.AetherMastery, ResearchBranch.Runes), new(ResearchKind.CivilEngineering, "土木水利",
            ResearchBranch.PublicHealth, "城建", false,
            [ResearchKind.Irrigation, ResearchKind.Logistics],
            new ResourceAmounts { Food = 30, Wood = 15, Stone = 15 }, 100, "解锁蓄水站。水务工人到实际水源取水，仍与野外取水共享每日额度。",
            [BuildingKind.Reservoir]),
        new(ResearchKind.Education, "公共教育", ResearchBranch.Survey, "教育", false, [ResearchKind.Logistics],
            new ResourceAmounts { Food = 30, Wood = 20 }, 100, "解锁图书馆与文献师，现场传授当地已有研究；知识仍需人员或通信送达异地。",
            [BuildingKind.Library], [Profession.Archivist]),
        From(ResearchKind.Pharmacology, ResearchBranch.PublicHealth), new(ResearchKind.Sanitation, "临床与防疫",
            ResearchBranch.PublicHealth, "公共卫生", false,
            [ResearchKind.Pharmacology, ResearchKind.CivilEngineering],
            new ResourceAmounts { Food = 35, Wood = 15, Stone = 20 }, 120, "解锁医院和医师。医师领取真实药品，治疗现场患者并建立短期疾病免疫。",
            [BuildingKind.Hospital], [Profession.Physician]),
        new(ResearchKind.FireEngineering, "消防工程", ResearchBranch.PublicHealth, "消防", false,
            [ResearchKind.CivilEngineering],
            new ResourceAmounts { Food = 30, Wood = 15, Stone = 15 }, 100, "解锁消防站和消防员。补充随身用水、扑救近处火灾，并修复受损建筑。",
            [BuildingKind.FireStation], [Profession.Firefighter]),
        new(ResearchKind.Cartography, "实地测绘",
            ResearchBranch.Survey, "勘察", false, [ResearchKind.Education, ResearchKind.Forestry],
            new ResourceAmounts { Food = 30, Wood = 15, Stone = 10 }, 100, "解锁勘测所和测绘员。现场发现水源、城镇与危险，形成有观察时间的报告。",
            [BuildingKind.SurveyOffice], [Profession.Surveyor]),
        new(ResearchKind.MechanicalEngineering, "机械工程", ResearchBranch.Industry, "机械", false,
            [ResearchKind.Industry, ResearchKind.ScientificMethod],
            new ResourceAmounts { Food = 35, Alloy = 5, Ore = 8 }, 120, "解锁工程师。工程师携带并消耗机械工具，加快实际施工；无工具时按普通工人劳动。",
            Professions: [Profession.Engineer]),
        From(ResearchKind.Toolmaking, ResearchBranch.Industry), From(ResearchKind.Ballistics, ResearchBranch.Military),
        new(
            ResearchKind.ProtectiveEquipment, "防护装备", ResearchBranch.Military, "军备", false,
            [ResearchKind.Ballistics, ResearchKind.Toolmaking],
            new ResourceAmounts { Food = 35, Alloy = 10 }, 120, "解锁装备工坊。到场居民领取实物合金制成护甲；受击时防护值逐次耗损。",
            [BuildingKind.Armory]),
        new(ResearchKind.RailTransport, "轨道交通", ResearchBranch.Transport, "近现代", false,
            [ResearchKind.MechanicalEngineering, ResearchKind.Logistics],
            new ResourceAmounts { Food = 40, Alloy = 12 }, 140, "解锁铺设铁路命令：花费石材和合金，升级己方已修道路，使陆地居民沿轨道移动更快。",
            Action: ResearchAction.Rail),
        new(ResearchKind.Observation, "光学观测", ResearchBranch.Communication, "近现代", false,
            [ResearchKind.Cartography, ResearchKind.SignalNetwork],
            new ResourceAmounts { Food = 35, Alloy = 8, EnergyCells = 5 }, 120,
            "勘测所观测范围由 4 格扩展至 6 格，山体仍遮挡视线；报告可经已值守信号塔传递。"),
        new(ResearchKind.Elementalism, "元素塑形", ResearchBranch.Warding, "元素魔法", true, [ResearchKind.ManaAttunement],
            new ResourceAmounts { Food = 35, Crystals = 5 }, 120, "解锁寒冰箭。攻击可见且已知交战的敌人，使其短暂冻结，期间无法移动。",
            Spells: [SpellKind.FrostBolt]),
        new(ResearchKind.Warding, "符文守卫", ResearchBranch.Warding, "结界", true,
            [ResearchKind.RunicEngineering, ResearchKind.Restoration],
            new ResourceAmounts { Food = 40, Stone = 20, Crystals = 10 }, 150,
            "解锁结界塔和符文护盾。消耗个人魔力与随身魔晶，给塔旁居民施加可耗损的个人护盾。",
            [BuildingKind.WardTower], Spells: [SpellKind.RuneWard]),
        From(ResearchKind.Alchemy, ResearchBranch.Restoration), new(ResearchKind.NatureBinding, "自然契约",
            ResearchBranch.Restoration, "自然魔法", true,
            [ResearchKind.Restoration, ResearchKind.Forestry],
            new ResourceAmounts { Food = 35, Stone = 15, Crystals = 8 }, 120,
            "解锁共生林苑、园艺师和唤雨。用水、魔力恢复真实树木覆盖，雨水解除近处干旱并压制火势。", [BuildingKind.GroveSanctuary], [Profession.Gardener],
            [SpellKind.RainCall]),
        new(ResearchKind.SpatialMagic, "空间折跃", ResearchBranch.Runes, "空间魔法", true,
            [ResearchKind.AetherMastery, ResearchKind.Leylines],
            new ResourceAmounts { Food = 50, Stone = 25, Crystals = 20 }, 180,
            "解锁折跃门与传送命令。两座同国、可运营的门相距至多 24 格；本人到门旁，消耗 30 魔力和 2 份随身魔晶，连同背包抵达目标门。", [BuildingKind.Waygate],
            Action: ResearchAction.Waygate),
        new(ResearchKind.BattleMagic, "战场法术", ResearchBranch.Warding, "战斗魔法", true,
            [ResearchKind.Elementalism, ResearchKind.Warding],
            new ResourceAmounts { Food = 45, Stone = 20, Crystals = 15 }, 160,
            "解锁风暴尖塔、战斗法师和连锁闪电。需要现场施法者、实际魔力和魔晶，仅攻击已知敌军。", [BuildingKind.StormSpire], [Profession.Battlemage],
            [SpellKind.ChainLightning]),
    });

    /// <summary>查询指定研究的节点定义。</summary>
    /// <param name="kind">研究项目。</param>
    public static ResearchDefinition For(ResearchKind kind)
    {
        return ByKind.TryGetValue(kind, out var value) ? value : throw new ArgumentOutOfRangeException(nameof(kind));
    }

    /// <summary>返回科技或魔法路线的全部研究，包含共同基础项目。</summary>
    /// <param name="magic">是否查询魔法路线；关闭时查询科技路线。</param>
    public static IReadOnlyList<ResearchKind> Route(bool magic)
    {
        return magic ? MagicRoute : TechnologyRoute;
    }

    /// <summary>查找解锁指定建筑的研究，没有对应研究时返回空值。</summary>
    /// <param name="kind">设施类别。</param>
    public static ResearchDefinition? Unlocking(BuildingKind kind)
    {
        return ByBuilding.GetValueOrDefault(kind);
    }

    /// <summary>查找解锁指定职业的研究，没有对应研究时返回空值。</summary>
    /// <param name="kind">职业类别。</param>
    public static ResearchDefinition? Unlocking(Profession kind)
    {
        return ByProfession.GetValueOrDefault(kind);
    }

    /// <summary>查找解锁指定法术的研究，没有对应研究时返回空值。</summary>
    /// <param name="kind">法术类别。</param>
    public static ResearchDefinition? Unlocking(SpellKind kind)
    {
        return BySpell.GetValueOrDefault(kind);
    }

    private static ResearchDefinition From(ResearchKind kind, ResearchBranch branch)
    {
        var a = AdvancementRules.For(kind)!;
        return new ResearchDefinition(kind, a.Name, branch, a.Stage, a.Magic, a.Prerequisites, a.ResearchCost, 180,
            "解锁" + a.FacilityName + "。\n" + WorldEngine.ProductionRecipe(a.Facility),
            kind == ResearchKind.Industry ? [a.Facility, BuildingKind.Aquaculture] : [a.Facility],
            kind == ResearchKind.Ballistics ? [Profession.Ranger] : null);
    }
}
