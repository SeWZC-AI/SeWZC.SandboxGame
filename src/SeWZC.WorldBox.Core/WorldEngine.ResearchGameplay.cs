using SeWZC.WorldBox.Core.Runtime;

namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    /// <summary>校验本地研究和居民条件后指定研究解锁的职业。</summary>
    /// <param name="residentId">居民 ID。</param>
    /// <param name="job">职业。</param>
    public void AssignResearchProfession(int residentId, Profession job)
    {
        var person = Residents.FirstOrDefault(r => r.Id == residentId) ?? throw new ArgumentException("居民不存在");
        var unlock = ResearchRules.Unlocking(job) ?? throw new ArgumentException("此岗位不属于研究解锁职业");
        if (!HasResearch(person.SettlementId, unlock) ||
            !HasResearchPrerequisites(person.SettlementId, unlock.Prerequisites))
            throw new InvalidOperationException("当地须掌握" + unlock.Name + "及其前置");
        if (person.Age < 14 || person.Health <= 0 || person.ArmyId != 0 || person.Agent.DestinationSettlementId != 0)
            throw new InvalidOperationException("只能分配当地未出征、未在异地递送的成年居民");
        if (job is Profession.Battlemage or Profession.Gardener && person.MagicTalent < 25)
            throw new InvalidOperationException("此岗位需要魔法天赋至少 25");
        person.Replace(person.Value with
        {
            Profession = job,
            Agent = person.Agent with
            {
                JobChangedTick = SimulationTick, WorkplaceId = 0, WorkAreaIndex = -1, NextThinkTick = SimulationTick,
            },
        });
        person.Agent =
            person.Agent.WithGoal(new AgentGoal { Kind = AgentGoalKind.Idle, TargetX = person.X, TargetY = person.Y });
        RecordLife(person, "根据已掌握的研究，接受" + ProfessionName(job) + "岗位。");
    }

    /// <summary>让已到场的本地建造者、工程师或消防员消耗随身石材修复邻近建筑。</summary>
    /// <param name="residentId">居民 ID。</param>
    /// <param name="buildingId">待操作建筑的稳定 ID。</param>
    public void RepairBuilding(int residentId, int buildingId)
    {
        var person = Residents.FirstOrDefault(r => r.Id == residentId);
        var b = FindBuilding(buildingId);
        if (person is null || b is null || person.Health <= 0 || person.Age < 14 ||
            person.Profession is not (Profession.Builder or Profession.Engineer or Profession.Firefighter)
            || !BuildingGroundOwned(b.Value) || b.Value.SettlementId != person.SettlementId ||
            Distance(person.X, person.Y, b.Value.X, b.Value.Y) > 1
            || SimulationTick - person.MoveStartedTick < person.MoveDurationTicks || b.Value.Health is <= 0 or >= 100 ||
            Tiles[Index(b.Value.X, b.Value.Y)].Value.FireTicks > 0)
            throw new InvalidOperationException("需本地建造者、工程师或消防员到受损设施 1 格内，且火势已扑灭");
        if (person.Inventory.Stone < .5)
            throw new InvalidOperationException("需要随身石材 0.5");
        person.Inventory = person.Inventory with { Stone = person.Inventory.Stone - .5 };
        b.Replace(b.Value with
        {
            Health = Math.Min(100, b.Value.Health + 10),
            ServiceActions = Math.Min(1_000_000_000, b.Value.ServiceActions + 1),
            LastServiceTick = SimulationTick,
        });
        EmitVisual(WorldVisualKind.Construction, b.Value.X, b.Value.Y);
    }

    /// <summary>核对路线研究、设施就绪和实际生产记录，返回文明目标达成情况。</summary>
    /// <param name="settlementId">聚落 ID。</param>
    /// <param name="magic">是否查询魔法路线；关闭时查询科技路线。</param>
    public CivilizationProgress GetCivilizationProgress(int settlementId, bool magic)
    {
        _ = RequireTown(settlementId);
        var route = ResearchRules.Route(magic);
        var missingResearch =
            route.Where(k => !HasResearch(settlementId, k)).Select(research => research.Name).ToArray();
        // 文明目标以实际生产链为依据，避免和平聚落被迫建造闲置军备或不适用本地地形的运输设施。
        var required = ProductionRules.All.Where(a => route.Contains(a.Research)
                                                      && a.Output is not (ResourceKind.Medicine
                                                          or ResourceKind.Ammunition))
            .Select(a => a.Facility)
            .Concat(magic ? new[] { BuildingKind.Academy, BuildingKind.ArcaneSanctum } : new[] { BuildingKind.Academy })
            .Distinct().ToArray();
        var buildings = Buildings.Where(b => b.Value.SettlementId == settlementId).ToArray();

        bool Ready(Building b)
        {
            return b.Enabled && b.IsCompleted && !b.IsUpgrading && b.Health >= 50
                   && BuildingGroundOwned(b) && Tiles[Index(b.X, b.Y)].Value.FireTicks == 0
                   && BuildingTerrainValid(b.Kind, Tiles[Index(b.X, b.Y)].Value);
        }

        var missingFacilities = required.Where(k => !buildings.Any(b => b.Value.Kind == k && Ready(b.Value))).Select(BuildingName)
            .ToArray();
        var unproven = required.Where(k => ProductionRules.For(k) is not null
                                           && !buildings.Any(b => b.Value.Kind == k && Ready(b.Value) && b.Value.ProductionBatches > 0))
            .Select(BuildingName).ToArray();
        return new CivilizationProgress(magic, route.Count - missingResearch.Length, route.Count,
            required.Length - missingFacilities.Length,
            required.Length, missingResearch, missingFacilities, unproven);
    }

    /// <summary>返回职业的实际劳动职责说明。</summary>
    /// <param name="job">职业。</param>
    public static string ProfessionDescription(Profession job)
    {
        return job switch
        {
            Profession.Engineer => "施工时消耗工具，提升现场施工效率；也可参与机械生产。",
            Profession.Physician => "把随身药品运至医院，治疗医院 3 格内的伤病居民并建立短期免疫。",
            Profession.Firefighter => "从消防站补充实际用水；扑救眼前火灾并用石材修复附近受损设施。",
            Profession.Ranger => "携带弹药，对 4 格内已知交战敌军射击；每次耗 1 弹药，间隔至少 3 tick，山体遮挡。",
            Profession.Archivist => "在图书馆把当地已有研究传授给现场居民，知识经正常递送继续传播。",
            Profession.Battlemage => "完成奥术训练后参与风暴尖塔、结界工作；行军时依已知军令施放战场法术。",
            Profession.Surveyor => "在勘测所形成眼前水源、城镇及危险的实地报告，带有观察时刻。",
            Profession.Gardener => "在共生林苑消耗用水与个人魔力，恢复周围树木覆盖；树木成熟后可供采伐。",
            Profession.Laborer => "无固定专业岗位时协助眼前实际生产、建设与搬运；缺粮时仍可自行觅食。",
            _ => ProfessionName(job),
        };
    }

    /// <summary>返回法术的中文名称。</summary>
    /// <param name="spell">法术类别。</param>
    public static string SpellName(SpellKind spell)
    {
        return spell switch
        {
            SpellKind.Heal => "治疗",
            SpellKind.HarvestBlessing => "丰饶祝福",
            SpellKind.Shield => "守护结界",
            SpellKind.Ember => "战斗火花",
            SpellKind.FrostBolt => "寒冰箭",
            SpellKind.ChainLightning => "连锁闪电",
            SpellKind.RainCall => "唤雨",
            SpellKind.RuneWard => "符文护盾",
            _ => spell.ToString(),
        };
    }

    /// <summary>返回施放一次法术所需的魔力量。</summary>
    /// <param name="spell">法术类别。</param>
    public static double SpellManaCost(SpellKind spell)
    {
        return spell switch
        {
            SpellKind.Heal => 16,
            SpellKind.HarvestBlessing => 25,
            SpellKind.Shield => 22,
            SpellKind.FrostBolt => 24,
            SpellKind.ChainLightning => 36,
            SpellKind.RainCall => 28,
            SpellKind.RuneWard => 20,
            _ => 20,
        };
    }

    private static double PersonalSpellCost(RaceKind race, SpellKind spell)
    {
        return SpellManaCost(spell) * (
            (race == RaceKind.Elf && spell == SpellKind.Heal) || (race == RaceKind.Dwarf && spell == SpellKind.Shield)
                                                              || (race == RaceKind.Orc && spell == SpellKind.Ember)
                ? .85
                : 1);
    }

    /// <summary>检查施法者是否掌握法术及前置研究；已解锁时返回空值，否则返回原因。</summary>
    /// <param name="casterId">施法居民的稳定 ID。</param>
    /// <param name="spell">法术类别。</param>
    public string? SpellUnlockError(int casterId, SpellKind spell)
    {
        var caster = FindLiveResident(casterId);
        if (caster is null)
            return "施法居民不存在";
        var research = ResearchRules.Unlocking(spell);
        return research is not null && (!HasResearch(caster.SettlementId, research)
                                        || !HasResearchPrerequisites(caster.SettlementId, research.Prerequisites))
            ? "当地需要掌握" + research.Name + "及其前置"
            : null;
    }

    /// <summary>检查本地研究、地点和材料后铺设笔刷范围内的铁路。</summary>
    /// <param name="settlementId">聚落 ID。</param>
    /// <param name="x">横向地格坐标。</param>
    /// <param name="y">纵向地格坐标。</param>
    /// <param name="radius">铁路笔刷的作用半径，以地格为单位。</param>
    public void BuildRail(int settlementId, int x, int y, int radius = 1)
    {
        var town = RequireTown(settlementId);
        if (!HasResearch(settlementId, Advancement.RailTransport))
            throw new InvalidOperationException("需要先掌握轨道交通");
        if (ResearchPrerequisiteError(settlementId, Advancement.RailTransport) is { } prerequisite)
            throw new InvalidOperationException(prerequisite);
        if (!InBounds(x, y) || radius is < 0 or > 4 || Distance(x, y, town.Value.X, town.Value.Y) > 24)
            throw new ArgumentException("铁路须位于聚落 24 格内，笔刷半径为 0–4");
        var tiles = Circle(x, y, radius).Where(i => Tiles[i].Value.NationId == town.Value.NationId
                                                    && Tiles[i].Value.IsWalkable &&
                                                    !IsWaterTerrain(Tiles[i].Value.Terrain) &&
                                                    Tiles[i].Value.RoadLevel == 1).ToArray();
        if (tiles.Length == 0)
            throw new InvalidOperationException("范围内没有己方可升级的陆地道路");
        town.Replace(town.Value.WithResources(Spend(town.Value.Resources, new ResourceStock { Stone = tiles.Length, Alloy = tiles.Length * .5 })));
        foreach (var i in tiles)
            Tiles[i].Replace(Tiles[i].Value.WithRoadLevel(2));
        AddEvent(WorldEventKind.Construction, town.Value.Name + "铺设铁路，道路须实际相连才能供居民沿线通行。", x, y, EventAction.Completed,
            town.Value.Id);
        RefreshTotals();
    }

    /// <summary>检查铺设铁路的条件；可铺设时返回空值，否则返回原因。</summary>
    /// <param name="settlementId">聚落 ID。</param>
    /// <param name="x">横向地格坐标。</param>
    /// <param name="y">纵向地格坐标。</param>
    /// <param name="radius">铁路笔刷的作用半径，以地格为单位。</param>
    public string? RailPlacementError(int settlementId, int x, int y, int radius = 0)
    {
        if (!_settlements.TryGetValue(settlementId, out var town))
            return "先选择负责铺轨的聚落";
        if (!HasResearch(settlementId, Advancement.RailTransport))
            return "需要先掌握轨道交通";
        if (ResearchPrerequisiteError(settlementId, Advancement.RailTransport) is { } prerequisite)
            return prerequisite;
        if (!InBounds(x, y) || radius is < 0 or > 4 || Distance(x, y, town.Value.X, town.Value.Y) > 24)
            return "目标须在聚落 24 格内";
        var count = Circle(x, y, radius).Count(i => Tiles[i].Value.NationId == town.Value.NationId &&
                                                    Tiles[i].Value.IsWalkable
                                                    && !IsWaterTerrain(Tiles[i].Value.Terrain) &&
                                                    Tiles[i].Value.RoadLevel == 1);
        return count == 0
            ? "此处没有本国可升级的陆地道路"
            : MissingResources(town.Value.Resources, new ResourceStock { Stone = count, Alloy = count * .5 });
    }

    private string ExpansionFacilityStatus(Building b)
    {
        var unlock = ResearchRules.Unlocking(b.Kind)!;
        if (!HasResearch(b.SettlementId, unlock) ||
            ResearchPrerequisiteError(b.SettlementId, unlock) is not null)
            return "缺少本地研究：" + unlock.Name + "及其前置";
        if (b.Kind == BuildingKind.Waygate)
        {
            var destinations = Buildings.Count(other =>
                other.Value.Id != b.Id && other.Value.Kind == BuildingKind.Waygate && GateReady(other.Value)
                && RequireTown(other.Value.SettlementId).Value.NationId == RequireTown(b.SettlementId).Value.NationId &&
                Distance(b.X, b.Y, other.Value.X, other.Value.Y) <= 24);
            return $"折跃门已就绪\n24 格内同国可用目标门 {destinations}\n传送须本人到门旁，消耗个人魔力 30、随身魔晶 2";
        }

        var job = PreferredExpansionJob(b.Kind);
        var staff = Residents.Count(p => p.SettlementId == b.SettlementId && p.Age >= 14 && p.Health > 0
                                                 && p.ArmyId == 0 && (job is null || p.Profession == job) &&
                                                 Distance(p.X, p.Y, b.X, b.Y) <= 1);
        return $"累计现场服务 {b.ServiceActions} 次\n到场人员 {staff}"
               + (job is { } role ? "\n岗位：" + ProfessionName(role) : "")
               + (ExpansionSupply(b.Kind) is { } supply
                   ? $"\n随身补给目标：{ResourceStock.Name(supply.Kind)} {supply.Amount:0.##}，不足时本人返仓取料"
                   : "")
               + (b.Kind == BuildingKind.Reservoir ? "\n从相邻河湖取水；水装入背包后返仓" : "");
    }

    /// <summary>检查居民经折跃门到达指定目标门的条件；可旅行时返回空值，否则返回原因。</summary>
    /// <param name="residentId">居民 ID。</param>
    /// <param name="destinationId">目的地折跃门的建筑 ID。</param>
    public string? WaygateTravelError(int residentId, int destinationId)
    {
        var person = Residents.FirstOrDefault(r => r.Id == residentId);
        var destination = FindBuilding(destinationId);
        if (person is null || person.Health <= 0 || person.Age < 14 || person.ArmyId != 0 ||
            person.TravelMode != TravelMode.Foot)
            return "需要活着的成年步行居民，且未编入军队";
        if (person.FrozenUntilTick > SimulationTick || SimulationTick - person.MoveStartedTick < person.MoveDurationTicks)
            return "等待当前移动或冻结结束";
        if (destination is null || destination.Value.Kind != BuildingKind.Waygate || !GateReady(destination.Value)
            || RequireTown(destination.Value.SettlementId).Value.NationId != person.NationId)
            return "目标须为同国、已完工且掌握空间折跃的可用折跃门";
        var source = Buildings.FirstOrDefault(b =>
            b.Value.Id != destinationId && b.Value.Kind == BuildingKind.Waygate && GateReady(b.Value)
            && RequireTown(b.Value.SettlementId).Value.NationId == person.NationId && Distance(person.X, person.Y, b.Value.X, b.Value.Y) <= 1
            && Distance(b.Value.X, b.Value.Y, destination.Value.X, destination.Value.Y) <= 24);
        if (source is null)
            return "本人须到另一座可用折跃门 1 格内，两门相距至多 24 格";
        if (person.MagicTalent < 25 || person.MagicTraining < 8)
            return "需要魔法天赋至少 25、训练至少 8";
        if (person.Mana < 30 || person.Inventory.Crystals < 2)
            return "需要个人魔力 30 和随身魔晶 2";
        if (!RaceTerrainRules.CanWalk(Tiles[Index(destination.Value.X, destination.Value.Y)].Value, person.Race))
            return "目标门地形不可通行";
        return null;
    }

    private bool GateReady(Building b)
    {
        return b.Enabled && b.IsCompleted && b.Health >= 50 && !b.IsUpgrading && BuildingGroundOwned(b)
               && Tiles[Index(b.X, b.Y)].Value.FireTicks == 0 && HasResearch(b.SettlementId, Advancement.SpatialMagic)
               && ResearchPrerequisiteError(b.SettlementId, Advancement.SpatialMagic) is null;
    }

    /// <summary>消耗居民随身魔晶及魔力，使其从附近可用折跃门抵达指定目标门。</summary>
    /// <param name="residentId">居民 ID。</param>
    /// <param name="destinationId">目的地折跃门的建筑 ID。</param>
    public void TravelByWaygate(int residentId, int destinationId)
    {
        if (WaygateTravelError(residentId, destinationId) is { } error)
            throw new InvalidOperationException(error);
        var person = Residents.First(r => r.Id == residentId);
        var target = FindBuilding(destinationId)!;
        var source = Buildings.First(b =>
            b.Value.Id != target.Value.Id && b.Value.Kind == BuildingKind.Waygate && GateReady(b.Value)
            && RequireTown(b.Value.SettlementId).Value.NationId == person.NationId && Distance(person.X, person.Y, b.Value.X, b.Value.Y) <= 1
            && Distance(b.Value.X, b.Value.Y, target.Value.X, target.Value.Y) <= 24);
        person.Mana -= 30;
        person.Inventory = person.Inventory with { Crystals = person.Inventory.Crystals - 2 };
        person.X = person.FromX = target.Value.X;
        person.Y = person.FromY = target.Value.Y;
        person.Replace(person.Value with { MoveStartedTick = SimulationTick, MoveDurationTicks = 1 });
        person.Replace(person.Value with
        {
            Agent = person.Agent with
            {
                DaytimeGoal = null,
                Goal = new AgentGoal
                {
                    Kind = AgentGoalKind.Idle,
                    TargetX = target.Value.X,
                    TargetY = target.Value.Y,
                    Reason = "本人携带背包经折跃门抵达",
                },
                NextThinkTick = SimulationTick + 1,
            },
        });
        source.Replace(source.Value with
        {
            ServiceActions = Math.Min(1_000_000_000, source.Value.ServiceActions + 1),
            LastServiceTick = SimulationTick,
        });
        target.Replace(target.Value with
        {
            ServiceActions = Math.Min(1_000_000_000, target.Value.ServiceActions + 1),
            LastServiceTick = SimulationTick,
        });
        EmitVisual(WorldVisualKind.Waygate, target.Value.X, target.Value.Y, 2, source.Value.X, source.Value.Y);
        AddEvent(WorldEventKind.Magic, person.Name + "携带真实背包经折跃门抵达，消耗个人魔力 30、魔晶 2。", target.Value.X, target.Value.Y,
            residentId: person.Id);
    }

    /// <summary>检查射手、目标、距离和弹药；可攻击时返回空值，否则返回原因。</summary>
    /// <param name="attackerId">发起远程攻击的居民 ID。</param>
    /// <param name="targetId">要攻击的敌方居民 ID。</param>
    public string? RangedAttackError(int attackerId, int targetId)
    {
        var person = Residents.FirstOrDefault(r => r.Id == attackerId);
        var target = Residents.FirstOrDefault(r => r.Id == targetId);
        if (person is null || target is null || person.Health <= 0 || target.Health <= 0 || person.Age < 14 ||
            person.NationId == target.NationId)
            return "攻击者或目标无效";
        if (person.Profession != Profession.Ranger || !HasResearch(person.SettlementId, Advancement.Ballistics) ||
            ResearchPrerequisiteError(person.SettlementId, Advancement.Ballistics) is not null)
            return "需要掌握弹道学的游击射手";
        if (person.FrozenUntilTick > SimulationTick || SimulationTick - person.LastRangedAttackTick < 3)
            return "冻结中或射击尚未冷却";
        if (!IsKnownHostile(person, target.NationId))
            return "本人未收到对目标国家的交战军令";
        if (Distance(person.X, person.Y, target.X, target.Y) > 4 ||
            !ClearSignalLine(person.X, person.Y, target.X, target.Y))
            return "目标须在 4 格内且山体不遮挡";
        return person.Inventory.Ammunition < 1 ? "随身弹药不足 1" : null;
    }

    /// <summary>校验攻击条件后消耗随身弹药，对指定敌方居民造成远程伤害。</summary>
    /// <param name="attackerId">发起远程攻击的居民 ID。</param>
    /// <param name="targetId">要攻击的敌方居民 ID。</param>
    public void RangedAttack(int attackerId, int targetId)
    {
        if (RangedAttackError(attackerId, targetId) is { } error)
            throw new InvalidOperationException(error);
        var person = Residents.First(r => r.Id == attackerId);
        var target = Residents.First(r => r.Id == targetId);
        person.Replace(person.Value with
        {
            Inventory = person.Inventory with { Ammunition = person.Inventory.Ammunition - 1 },
            LastRangedAttackTick = SimulationTick,
        });
        DamageResident(target, TryAbsorbShieldDamage(target, 12 * Rules.CombatDamageRate), DeathCause.Battle);
        EmitVisual(WorldVisualKind.Battle, target.X, target.Y, 1, person.X, person.Y);
        AddEvent(WorldEventKind.War, person.Name + "消耗一份随身弹药，向已知交战目标射击。", target.X, target.Y, residentId: person.Id);
    }

    private bool ExpansionFacilityHasWork(Building b, ResidentCursor person)
    {
        var research = ResearchRules.Unlocking(b.Kind);
        if (research is null || !HasResearch(b.SettlementId, research) ||
            !HasResearchPrerequisites(b.SettlementId, research.Prerequisites))
            return false;
        return b.Kind switch
        {
            BuildingKind.Reservoir => person.Inventory.Water < 6 && WaterSourceForStation(b) >= 0,
            BuildingKind.Hospital =>
                person.Profession == Profession.Physician && FindLocalWorkPatient(b, true) is not null
                                                          && (person.Inventory.Medicine >= .25 ||
                                                              RequireTown(b.SettlementId).Value.Resources.Medicine >= .25),
            BuildingKind.FireStation => person.Profession == Profession.Firefighter &&
                                        ((person.Inventory.Water < 5 &&
                                          RequireTown(b.SettlementId).Value.Resources.Water >= .5)
                                         || (RepairTargetForStation(b) is not null && (person.Inventory.Stone >= .5 ||
                                             RequireTown(b.SettlementId).Value.Resources.Stone >= .5))),
            BuildingKind.Library => person.Profession == Profession.Archivist && SimulationTick - b.LastServiceTick >= 12,
            BuildingKind.SurveyOffice => person.Profession == Profession.Surveyor &&
                                         SimulationTick - b.LastServiceTick >= 12,
            BuildingKind.Armory => person.Armor < 30 && RequireTown(b.SettlementId).Value.Resources.Alloy >= 2,
            BuildingKind.WardTower => person.MagicTalent >= 25 && person.MagicTraining >= 8 && person.Mana >= 8 &&
                                      SimulationTick - b.LastServiceTick >= 12
                                      && (person.Inventory.Crystals >= .25 ||
                                          RequireTown(b.SettlementId).Value.Resources.Crystals >= .25)
                                      && LocalWardPatient(b) is not null,
            BuildingKind.StormSpire => person.Profession == Profession.Battlemage && person.MagicTraining >= 8 &&
                                       person.Mana >= 12
                                       && (person.Inventory.Crystals >= .5 ||
                                           RequireTown(b.SettlementId).Value.Resources.Crystals >= .5) &&
                                       LocalHostile(person, b.X, b.Y, 5) is not null,
            BuildingKind.GroveSanctuary => person.Profession == Profession.Gardener && person.MagicTalent >= 25 &&
                                           person.MagicTraining >= 8 && person.Mana >= 2
                                           && (person.Inventory.Water >= .25 ||
                                               RequireTown(b.SettlementId).Value.Resources.Water >= .25)
                                           && Circle(b.X, b.Y, 2).Any(i =>
                                               CanRestoreTrees(Tiles[i].Value) && Tiles[i].Value.Plants.Trees < .7),
            _ => false,
        };
    }

    private int WaterSourceForStation(Building b)
    {
        return Circle(b.X, b.Y, 1).Where(i => AvailableWater(i % Width, i / Width) > .05
                                              && (i == Index(b.X, b.Y) || IsFreshWater(Tiles[i].Value)))
            .OrderByDescending(i => GetDailyWaterCapacity(i % Width, i / Width)).FirstOrDefault(-1);
    }

    private ResidentCursor? LocalWardPatient(Building b)
    {
        return Residents.Where(p => p.SettlementId == b.SettlementId && p.Health > 0
                                                                             && p.PersonalWard < 12 &&
                                                                             Distance(p.X, p.Y, b.X, b.Y) <= 3)
            .OrderBy(p => p.PersonalWard).ThenBy(p => p.Id).FirstOrDefault();
    }

    private ResidentCursor? LocalHostile(ResidentCursor person, int x, int y, int radius)
    {
        return Residents.Where(p => p.Health > 0
                                            && p.NationId != person.NationId && Distance(p.X, p.Y, x, y) <= radius &&
                                            IsKnownHostile(person, p.NationId)
                                            && ClearSignalLine(x, y, p.X, p.Y)).OrderBy(p => p.Id).FirstOrDefault();
    }

    private bool WorkExpansionFacility(StateReference<Building> b, ResidentCursor person, StateReference<Settlement> town, double effort)
    {
        // 服务人员须先从家乡仓库实际携带限量物资到岗，避免远程消耗库存。
        bool Supply(ResourceKind kind, double amount, double reserve = 0)
        {
            if (person.Inventory.Get(kind) + .000001 >= amount + reserve)
                return true;
            if (Distance(person.X, person.Y, town.Value.X, town.Value.Y) > 1)
                return false;
            var take = Math.Min(town.Value.Resources.Get(kind), Math.Max(0, amount + reserve - person.Inventory.Get(kind)));
            town.Replace(town.Value.WithResources(town.Value.Resources.WithAmount(kind, town.Value.Resources.Get(kind) - take)));
            person.Inventory = person.Inventory.WithAmount(kind, person.Inventory.Get(kind) + take);
            return person.Inventory.Get(kind) + .000001 >= amount + reserve;
        }

        var done = false;
        switch (b.Value.Kind)
        {
            case BuildingKind.Reservoir:
                var source = WaterSourceForStation(b.Value);
                if (source >= 0)
                    done = DrawWater(person, source, Math.Min(3, 2 * effort)) > 0;
                break;
            case BuildingKind.Hospital:
                var patient = FindLocalWorkPatient(b.Value);
                if (patient is not null && Supply(ResourceKind.Medicine, .25))
                {
                    person.Inventory = person.Inventory with { Medicine = person.Inventory.Medicine - .25 };
                    patient.Replace(patient.Value with
                    {
                        Health = Math.Min(100, patient.Health + 3 * effort),
                        SicknessTicks = Math.Max(0, patient.SicknessTicks - SimulationTime.TicksPerDay),
                        DiseaseImmuneUntilTick = Math.Max(patient.DiseaseImmuneUntilTick,
                            SimulationTick + SimulationTime.TicksPerYear),
                    });
                    EmitVisual(WorldVisualKind.Heal, patient.X, patient.Y);
                    done = true;
                }

                break;
            case BuildingKind.FireStation:
                var damaged = Buildings.Where(other => other.Value.SettlementId == b.Value.SettlementId &&
                                                               other.Value.Health is > 0 and < 100
                                                               && Distance(person.X, person.Y, other.Value.X,
                                                                   other.Value.Y) <= 1 &&
                                                               Tiles[Index(other.Value.X, other.Value.Y)].Value
                                                                   .FireTicks ==
                                                               0).OrderBy(other => other.Value.Health)
                    .ThenBy(other => other.Value.Id).FirstOrDefault();
                if (damaged is not null && Supply(ResourceKind.Stone, .1))
                {
                    person.Inventory = person.Inventory with { Stone = person.Inventory.Stone - .1 };
                    damaged.Replace(damaged.Value with { Health = Math.Min(100, damaged.Value.Health + 2 * effort) });
                    done = true;
                }

                break;
            case BuildingKind.Library:
                var project = Society.Research.First(r => r.SettlementId == town.Value.Id);
                if (project.Completed.Count == 0)
                    break;
                var knowledge = project.Completed[(int)(SimulationTick / 12 % project.Completed.Count)];
                var fact = MakeAgentFact(person, AgentFactKind.Research, town.Value.Id, b.Value.X, b.Value.Y, knowledge.Id,
                    "在图书馆研读当地已有的" + knowledge.Name);
                foreach (var pupil in Residents.Where(p =>
                             p.SettlementId == town.Value.Id && p.Health > 0 && Distance(p.X, p.Y, b.Value.X, b.Value.Y) <= 2))
                    RememberAgentFact(pupil, fact);
                done = true;
                break;
            case BuildingKind.SurveyOffice:
                SurveyFromOffice(b.Value, person);
                done = true;
                break;
            case BuildingKind.Armory:
                if (Supply(ResourceKind.Alloy, 2))
                {
                    person.Replace(person.Value with
                    {
                        Inventory = person.Inventory with { Alloy = person.Inventory.Alloy - 2 }, Armor = 30,
                    });
                    done = true;
                }

                break;
            case BuildingKind.WardTower:
                var wardPatient = LocalWardPatient(b.Value);
                if (wardPatient is not null && Supply(ResourceKind.Crystals, .25))
                {
                    person.Inventory = person.Inventory with { Crystals = person.Inventory.Crystals - .25 };
                    person.Mana -= 8;
                    wardPatient.Replace(wardPatient.Value with { PersonalWard = Math.Max(wardPatient.PersonalWard, 24) });
                    EmitVisual(WorldVisualKind.Shield, wardPatient.X, wardPatient.Y);
                    done = true;
                }

                break;
            case BuildingKind.StormSpire:
                var target = LocalHostile(person, b.Value.X, b.Value.Y, 5);
                if (target is not null && Supply(ResourceKind.Crystals, .5))
                {
                    person.Inventory = person.Inventory with { Crystals = person.Inventory.Crystals - .5 };
                    person.Mana -= 12;
                    DamageResident(target, TryAbsorbShieldDamage(target, 16 * effort * Rules.CombatDamageRate),
                        DeathCause.Magic);
                    EmitVisual(WorldVisualKind.Ember, target.X, target.Y, 1, b.Value.X, b.Value.Y);
                    done = true;
                }

                break;
            case BuildingKind.GroveSanctuary:
                if (!Supply(ResourceKind.Water, .25, .5))
                    break;
                var ground = Circle(b.Value.X, b.Value.Y, 2)
                    .FirstOrDefault(i => CanRestoreTrees(Tiles[i].Value) && Tiles[i].Value.Plants.Trees < .7, -1);
                if (ground < 0)
                    break;
                person.Inventory = person.Inventory with { Water = person.Inventory.Water - .25 };
                person.Mana -= 2;
                var tile = Tiles[ground];
                var plants = tile.Value.Plants;
                plants = plants with { Trees = Math.Min(.7, plants.Trees + .02 * effort) };
                var total = plants.Total;
                if (total > 1)
                {
                    for (var i = 0; i < 4; i++)
                        plants = plants.WithCoverage((PlantKind)i, plants.Get((PlantKind)i) / total);
                }

                tile.Replace(tile.Value.WithPlants(plants));
                if (plants.Trees >= .5 && tile.Value.SettlementId == 0 &&
                    tile.Value.Terrain is TerrainType.Grass or TerrainType.DryFertile)
                    tile.Replace(tile.Value.WithTerrain(TerrainType.Woodland));
                var capacity = NaturalResourceCapacity(tile.Value);
                if (tile.Value.ResourceAmount < capacity)
                    tile.Replace(tile.Value.WithResourceAmount(Math.Min(capacity, tile.Value.ResourceAmount + effort)));
                EmitVisual(WorldVisualKind.Harvest, ground % Width, ground / Width);
                done = true;
                break;
        }

        if (done)
        {
            b.Replace(b.Value with
            {
                ServiceActions = Math.Min(1_000_000_000, b.Value.ServiceActions + 1),
                LastServiceTick = SimulationTick,
            });
        }

        return done;
    }

    private void SurveyFromOffice(Building b, ResidentCursor person)
    {
        var range = HasResearch(b.SettlementId, Advancement.Observation) ? 6 : 4;
        foreach (var town in Settlements)
            if (Distance(b.X, b.Y, town.Value.X, town.Value.Y) <= range && ClearSignalLine(b.X, b.Y, town.Value.X, town.Value.Y))
            {
                RememberAgentFact(person,
                    MakeAgentFact(person, AgentFactKind.SettlementLocation, town.Value.Id, town.Value.X, town.Value.Y, town.Value.NationId,
                        "从勘测所实际观察到城镇"));
            }

        var danger = Circle(b.X, b.Y, range).FirstOrDefault(i =>
            Distance(b.X, b.Y, i % Width, i / Width) <= range
            && Tiles[i].Value.FireTicks > 0 && ClearSignalLine(b.X, b.Y, i % Width, i / Width), -1);
        if (danger >= 0)
        {
            RememberAgentFact(person,
                MakeAgentFact(person, AgentFactKind.Danger, 0, danger % Width, danger / Width,
                    Tiles[danger].Value.FireTicks, "从勘测所观察到火情"));
        }

        var water = Circle(b.X, b.Y, range).FirstOrDefault(i =>
            Distance(b.X, b.Y, i % Width, i / Width) <= range
            && AvailableWater(i % Width, i / Width) > .05
            && ClearSignalLine(b.X, b.Y, i % Width, i / Width), -1);
        if (water >= 0)
        {
            RememberAgentFact(person,
                MakeAgentFact(person, AgentFactKind.WaterSource, water + 1, water % Width,
                    water / Width, 1,
                    "从勘测所观察到实际水源"));
        }
    }

    private static bool CanRestoreTrees(Tile tile)
    {
        return tile.IsWalkable && !IsWaterTerrain(tile.Terrain) && tile.Improvement == LandImprovement.None
               && tile.Fertility >= 40 && tile.NaturalWaterYield >= .004 && tile.FireTicks == 0;
    }

    private static Profession? PreferredExpansionJob(BuildingKind kind)
    {
        return kind switch
        {
            BuildingKind.Hospital => Profession.Physician,
            BuildingKind.FireStation => Profession.Firefighter,
            BuildingKind.Library => Profession.Archivist,
            BuildingKind.SurveyOffice => Profession.Surveyor,
            BuildingKind.MachineWorkshop => Profession.Engineer,
            BuildingKind.Arsenal => Profession.Ranger,
            BuildingKind.StormSpire => Profession.Battlemage,
            BuildingKind.GroveSanctuary => Profession.Gardener,
            _ => null,
        };
    }

    private bool ExpansionJobHasNearbyWork(ResidentCursor person)
    {
        IReadOnlyList<StateReference<Building>>? buildings = _localWorkQueriesActive
            ? _localWorkBuildings.GetValueOrDefault(person.SettlementId)
            : Buildings;
        if (buildings is null)
            return false;
        foreach (var b in buildings)
            if (b.Value.SettlementId == person.SettlementId && PreferredExpansionJob(b.Value.Kind) == person.Profession && b.Value.Enabled
                && b.Value.IsCompleted && b.Value.Health >= 50 && !b.Value.IsUpgrading && Distance(person.X, person.Y, b.Value.X, b.Value.Y) <= 8
                && ExpansionFacilityHasWork(b.Value, person))
                return true;
        return false;
    }

    private bool ActOnBuildingRepair(ResidentCursor person, StateReference<Settlement> home)
    {
        if (person.Agent.Goal.Kind != AgentGoalKind.Work)
            return false;
        var b = FindBuilding(person.Agent.Goal.TargetEntityId);
        if (b is null || b.Value.Health is <= 0 or >= 50 ||
            person.Profession is not (Profession.Builder or Profession.Engineer or Profession.Firefighter))
            return false;
        if (person.Inventory.Stone < .5)
        {
            if (Distance(person.X, person.Y, home.Value.X, home.Value.Y) > 1)
            {
                MoveAgentTowards(person, home.Value.X, home.Value.Y);
                return true;
            }

            var take = Math.Min(home.Value.Resources.Stone, .5 - person.Inventory.Stone);
            home.Replace(home.Value.WithResources(home.Value.Resources with { Stone = home.Value.Resources.Stone - take }));
            person.Inventory = person.Inventory with { Stone = person.Inventory.Stone + take };
        }

        if (Distance(person.X, person.Y, b.Value.X, b.Value.Y) > 1)
        {
            MoveAgentTowards(person, b.Value.X, b.Value.Y);
            return true;
        }

        if (person.Inventory.Stone >= .5 && Tiles[Index(b.Value.X, b.Value.Y)].Value.FireTicks == 0)
            TryWorkAtBuilding(person);
        return true;
    }

    private static (ResourceKind Kind, double Amount)? ExpansionSupply(BuildingKind kind)
    {
        return kind switch
        {
            BuildingKind.Hospital => (ResourceKind.Medicine, 2),
            BuildingKind.FireStation => (ResourceKind.Water, 5.5),
            BuildingKind.Armory => (ResourceKind.Alloy, 2),
            BuildingKind.WardTower => (ResourceKind.Crystals, 1),
            BuildingKind.StormSpire => (ResourceKind.Crystals, 2),
            BuildingKind.GroveSanctuary => (ResourceKind.Water, 3),
            _ => null,
        };
    }

    private bool ActOnExpansionFacility(ResidentCursor person, StateReference<Settlement> home)
    {
        var goal = person.Agent.Goal;
        if (goal.Kind != AgentGoalKind.Work)
            return false;
        var b = FindBuilding(goal.TargetEntityId);
        if (b is null || b.Value.Kind < BuildingKind.Reservoir || !b.Value.IsCompleted || b.Value.IsUpgrading ||
            ProductionRules.For(b.Value.Kind) is not null)
            return false;
        if (!ExpansionFacilityHasWork(b.Value, person))
        {
            person.Agent = person.Agent with { NextThinkTick = SimulationTick };
            return true;
        }

        if (b.Value.Kind == BuildingKind.FireStation && RepairTargetForStation(b.Value) is { } repair
                                               && (person.Inventory.Stone >= .5 || home.Value.Resources.Stone >= .5))
        {
            if (person.Inventory.Stone < .5)
            {
                person.Agent = person.Agent.WithGoal(goal = goal with
                {
                    TargetX = home.Value.X, TargetY = home.Value.Y, Reason = "返仓领取实际石材，运至受损设施维修",
                });
                if (Distance(person.X, person.Y, home.Value.X, home.Value.Y) > 1)
                {
                    MoveAgentTowards(person, home.Value.X, home.Value.Y);
                    return true;
                }

                var take = Math.Min(home.Value.Resources.Stone, 1 - person.Inventory.Stone);
                home.Replace(home.Value.WithResources(home.Value.Resources with { Stone = home.Value.Resources.Stone - take }));
                person.Inventory = person.Inventory with { Stone = person.Inventory.Stone + take };
            }

            person.Agent = person.Agent.WithGoal(goal = goal with
            {
                TargetX = repair.Value.X,
                TargetY = repair.Value.Y,
                Reason = "携带石材，步行至消防站附近的受损设施维修",
            });
            if (Distance(person.X, person.Y, repair.Value.X, repair.Value.Y) > 1)
            {
                MoveAgentTowards(person, repair.Value.X, repair.Value.Y);
                return true;
            }

            if (SimulationTick - person.MoveStartedTick < person.MoveDurationTicks)
                return true;
            RepairBuilding(person.Id, repair.Value.Id);
            if (repair.Value.Id != b.Value.Id)
            {
                b.Replace(b.Value with
                {
                    ServiceActions = Math.Min(1_000_000_000, b.Value.ServiceActions + 1),
                    LastServiceTick = SimulationTick,
                });
            }

            person.Activity = ResidentActivity.Working;
            return true;
        }

        var minimum = b.Value.Kind switch
        {
            BuildingKind.Hospital => .25,
            BuildingKind.FireStation => 5.5,
            BuildingKind.Armory => 2d,
            BuildingKind.WardTower => .25,
            BuildingKind.StormSpire => .5,
            BuildingKind.GroveSanctuary => .75,
            _ => 0,
        };
        if (ExpansionSupply(b.Value.Kind) is { } supply && person.Inventory.Get(supply.Kind) + .000001 < minimum)
        {
            person.Agent = person.Agent.WithGoal(goal = goal with
            {
                TargetX = home.Value.X,
                TargetY = home.Value.Y,
                Reason = "亲自返仓领取" + ResourceStock.Name(supply.Kind) + "，运至" + BuildingName(b.Value.Kind),
            });
            if (Distance(person.X, person.Y, home.Value.X, home.Value.Y) > 1)
            {
                MoveAgentTowards(person, home.Value.X, home.Value.Y);
                return true;
            }

            var take = Math.Min(home.Value.Resources.Get(supply.Kind),
                Math.Max(0, supply.Amount - person.Inventory.Get(supply.Kind)));
            home.Replace(home.Value.WithResources(home.Value.Resources.WithAmount(supply.Kind, home.Value.Resources.Get(supply.Kind) - take)));
            person.Inventory = person.Inventory.WithAmount(supply.Kind, person.Inventory.Get(supply.Kind) + take);
        }

        person.Agent = person.Agent.WithGoal(goal = goal with
        {
            TargetX = b.Value.X,
            TargetY = b.Value.Y,
            Reason = "携带补给，在" + BuildingName(b.Value.Kind) + "提供现场服务",
        });
        if (Distance(person.X, person.Y, b.Value.X, b.Value.Y) > 0)
        {
            MoveAgentTowards(person, b.Value.X, b.Value.Y);
            return true;
        }

        TryWorkAtBuilding(person);
        person.Activity = ResidentActivity.Working;
        if (b.Value.Kind == BuildingKind.Reservoir && person.Inventory.Water >= 3)
        {
            var returning = new AgentGoal
            {
                Kind = AgentGoalKind.ReturnHome,
                TargetX = home.Value.X,
                TargetY = home.Value.Y,
                TargetSettlementId = home.Value.Id,
                StartedTick = SimulationTick,
                Reason = "蓄水站取水后亲自运回粮仓",
            };
            ChangeWorkReservation(person.Agent.Goal, returning);
            person.Replace(person.Value with
            {
                Agent = person.Agent with { Goal = returning, NextThinkTick = SimulationTick + 30 },
            });
        }

        return true;
    }

    private StateReference<Building>? RepairTargetForStation(Building station)
    {
        return Buildings
            .Where(other => other.Value.SettlementId == station.SettlementId && other.Value.Health is > 0 and < 100
                                                                       && BuildingGroundOwned(other.Value) &&
                                                                       Distance(other.Value.X, other.Value.Y, station.X,
                                                                           station.Y) <= 4
                                                                       && Tiles[Index(other.Value.X, other.Value.Y)].Value
                                                                           .FireTicks == 0 &&
                                                                       ClearSignalLine(station.X, station.Y, other.Value.X,
                                                                           other.Value.Y))
            .OrderBy(other => other.Value.Health).ThenBy(other => other.Value.Id).FirstOrDefault();
    }
}
