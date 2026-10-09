using SeWZC.WorldBox.Core.Runtime;

namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    private static void ValidateWorldRules(WorldRules? rules)
    {
        if (rules is null || !double.IsFinite(rules.GatheringRate) || rules.GatheringRate is < .25 or > 3
            || !double.IsFinite(rules.CombatDamageRate) || rules.CombatDamageRate is < .25 or > 3 ||
            rules.Conflict is < 0 or > 3 || rules.DisasterFrequency is < 0 or > 3
            || rules.DisasterStrength is < 1 or > 3 || !double.IsFinite(rules.DevelopmentRate)
            || rules.DevelopmentRate is < .5 or > 3 || !double.IsFinite(rules.MagicRate) ||
            rules.MagicRate is < .5 or > 3)
            throw new ArgumentException("世界规则数值超出范围。");
    }

    /// <summary>校验并复制新的模拟规则，同时设置自然灾害和魔法开关。</summary>
    /// <param name="rules">校验后复制并使用的世界规则。</param>
    /// <param name="disasters">是否允许自主自然灾害。</param>
    /// <param name="magic">是否允许新的魔法发展和施法。</param>
    public void ConfigureWorld(WorldRules rules, bool disasters, bool magic)
    {
        ValidateWorldRules(rules);
        Current.Replace(Current.Value with { Rules = rules, NaturalDisasters = disasters });
        Current.Society = Current.Society with { MagicEnabled = magic };
        AddEvent(WorldEventKind.Editor, "玩家调整世界规则，新的选择按新规则执行；已有项目与成果保留。");
    }

    /// <summary>查询聚落当前的发展概况。</summary>
    /// <param name="settlementId">聚落 ID。</param>
    public DevelopmentSummary GetDevelopment(int settlementId)
    {
        var town = RequireTown(settlementId);
        var research = Current.Society.Research.First(r => r.SettlementId == town.Id);
        var construction = Current.Buildings.FirstOrDefault(b => b.SettlementId == town.Id && !b.IsCompleted);
        var stage = research.Completed.Count >= 3 ? "区域网络" :
            research.Completed.Count > 0 ? "专业分工" :
            town.Resources.Food >= town.Population * 2 ? "积累余粮" : "建立家园";
        if (research.Completed.Any(k => ProductionRules.For(k) is not null))
            stage = GetAdvancementStage(town.Id);
        if (town.IsExpanding)
        {
            return new DevelopmentSummary(stage, "扩充为" + SettlementTierName(town.Tier + 1), "居民到城镇中心施工；城镇中心等级独立",
                town.ExpansionProgress / town.ExpansionRequired);
        }

        if (construction is not null)
        {
            return new DevelopmentSummary(stage, "修建" + BuildingName(construction.Kind),
                Current.Tick - construction.LastWorkedTick > 12 ? "等待工人实际到场；可查看居民任务" : "工人正在现场施工",
                construction.ConstructionProgress / construction.ConstructionRequired);
        }

        if (research.ActiveProject is { } project)
        {
            var academy = Current.Buildings.FirstOrDefault(b =>
                b.SettlementId == town.Id && b.Kind == BuildingKind.Academy && b.IsCompleted);
            return new DevelopmentSummary(stage, "研究" + project.Name,
                academy is null ? "需要已建成学舍" : Current.Tick - academy.LastWorkedTick > 12 ? "等待学者到学舍工作" : "学者正在推进研究",
                research.Progress / Math.Max(1, research.RequiredProgress));
        }

        return new DevelopmentSummary(stage, town.DevelopmentGoal, town.DevelopmentBlocker, 0);
    }

    /// <summary>检查设施放置、研究和材料条件；可放置时返回空值，否则返回原因。</summary>
    /// <param name="settlementId">聚落 ID。</param>
    /// <param name="kind">设施类别。</param>
    /// <param name="x">横向地格坐标。</param>
    /// <param name="y">纵向地格坐标。</param>
    /// <param name="gift">是否按赐予方式校验，跳过普通研究前置和材料检查；地形及运营限制仍适用。</param>
    /// <param name="direction">桥梁通行轴向，空值时根据现场连岸条件推断。</param>
    /// <param name="bridgeLevel">建造的桥梁等级，范围为 1 至 3；普通建筑忽略此参数。</param>
    /// <param name="founding">是否为建村选址，允许在可登记地块安排初始设施。</param>
    public string? FacilityPlacementError(int settlementId, BuildingKind kind, int x, int y, bool gift = false,
        BridgeDirection? direction = null, int bridgeLevel = 1, bool founding = false)
    {
        if (!Enum.IsDefined(kind))
            return "未知的建筑类型";
        if (kind == BuildingKind.TownCenter)
            return "每处聚落的中心由定居和重建维护，无需另行放置";
        if (!_settlements.TryGetValue(settlementId, out var town))
            return "先选择归属聚落";
        if (!InBounds(x, y) || !BuildingTerrainValid(kind, Current.Tiles[Index(x, y)]))
        {
            return IsWaterfrontBuilding(kind) ? "船坞和码头需要水中的近岸地块" :
                kind == BuildingKind.Bridge ? "桥梁需要河流或浅水" :
                kind == BuildingKind.MountainPass ? "山路需要山地" : "需要可通行的陆地";
        }

        if (kind == BuildingKind.Well && WellWaterYield(Current.Tiles[Index(x, y)]) <= 0)
            return "水井需要地块供水量高于 0.02，请选择供水更充足的地块";
        if (!CanBuildRacialFacility(settlementId, kind))
            return "需要当地有该种族的成年居民";
        if (kind == BuildingKind.SacredGrove &&
            (!IsForestTerrain(Current.Tiles[Index(x, y)].Terrain) || !Current.Society.MagicEnabled))
            return "精灵圣林需要森林和开放的魔法规则";
        if (kind == BuildingKind.Bridge &&
            BridgePlacementError(x, y, direction ?? InferBridgeDirection(x, y), bridgeLevel) is
            { } bridgeError)
            return bridgeError;
        var range = kind is BuildingKind.MountainPass or BuildingKind.Bridge ? 24 : Math.Max(8, town.MaxClaimRadius);
        if (Distance(x, y, town.X, town.Y) > range)
            return $"距归属聚落超过 {range} 格";
        if (kind is BuildingKind.MountainPass or BuildingKind.Bridge &&
            !Directions.Any(d => Walkable(x + d.X, y + d.Y)))
            return "需要相邻的可通行施工位置，逐段向前建设";
        if (IsWaterfrontBuilding(kind) && !Directions.Any(d => Walkable(x + d.X, y + d.Y)
                                                               && !IsWaterTerrain(Current.Tiles[Index(x + d.X, y + d.Y)]
                                                                   .Terrain)))
            return "需要紧邻自然陆岸，居民从岸边施工和工作";
        var tile = Current.Tiles[Index(x, y)];
        if (tile.Terrain == TerrainType.Mountain && kind != BuildingKind.MountainPass && !Current.Residents.Any(p =>
                p.SettlementId == settlementId && p.Race == RaceKind.Dwarf && p.Health > 0 && p.Age >= 14))
            return "山地建设需要当地成年矮人";
        if (tile.FireTicks > 0)
            return "此处正在燃烧";
        if (!IsPublicInfrastructure(kind))
        {
            if (tile.NationId != 0 && tile.NationId != town.NationId)
                return "此处属于其他国家";
            if (tile.ClaimedSettlementId != 0 && tile.ClaimedSettlementId != town.Id)
                return "此地已由其他城镇独占登记";
            if (IsWaterfrontBuilding(kind))
            {
                if (!Directions.Any(d =>
                        InBounds(x + d.X, y + d.Y) && !IsWaterTerrain(Current.Tiles[Index(x + d.X, y + d.Y)].Terrain)
                                                   && Current.Tiles[Index(x + d.X, y + d.Y)].ClaimedSettlementId ==
                                                   town.Id))
                    return "需要紧邻本城镇已占领的陆岸";
            }
            else if (tile.ClaimedSettlementId != town.Id &&
                     !(founding && CanClaimTile(town, Index(x, y), RaceKind.Dwarf)))
                return "请先实地占领此地，再建造建筑";
        }

        if (kind == BuildingKind.Pasture && (tile.Fertility < 25 || IsWaterTerrain(tile.Terrain)))
            return "牧场需要肥力至少 25 的陆地";
        if (kind == BuildingKind.Aquaculture && !Circle(x, y, 1).Any(i => IsFreshWater(Current.Tiles[i])))
            return "水产养殖厂需要紧邻河湖的陆地";
        if (kind == BuildingKind.Aquaculture && !gift && !HasResearch(settlementId, Advancement.Logistics))
            return "需要先掌握驿路运输";
        if (kind == BuildingKind.Well && DailyWaterYield(tile) < .025)
            return "水井需要地块供水量至少 0.025";
        if (kind is BuildingKind.LumberCamp or BuildingKind.Quarry && !Circle(x, y, 1).Any(i => i != Index(x, y)
                && Current.Tiles[i].ResourceAmount > 0 && (kind == BuildingKind.LumberCamp
                    ? IsForestTerrain(Current.Tiles[i].Terrain)
                    : TerrainRules.For(Current.Tiles[i].Terrain).StoneYield +
                    TerrainRules.For(Current.Tiles[i].Terrain).OreYield >= .5)))
            return "需要紧邻实际森林或石矿资源";
        if (Current.Buildings.Count >= MaxBuildings - 256)
            return "世界建筑数量已达上限";
        if (Current.Buildings.Any(b => b.X == x && b.Y == y))
            return "此处已有建筑";
        if ((kind == BuildingKind.ArcaneSanctum || ProductionRules.For(kind)?.Research.Magic == true ||
             ResearchRules.Unlocking(kind)?.Magic == true) && !Current.Society.MagicEnabled)
            return "规则已关闭新的魔法发展";
        if (kind == BuildingKind.SignalTower && (!HasResearch(settlementId, Advancement.Electrification) ||
                                                 !HasResearch(settlementId, Advancement.SignalNetwork)))
            return "无线信号塔需要电气化与信号网络";
        if (!gift && kind == BuildingKind.SacredGrove && !HasResearch(settlementId, Advancement.ArcaneArts))
            return "需要当地掌握奥术基础";
        if (gift)
            return null;
        if (ResearchRules.Unlocking(kind) is { } unlock && (!HasResearch(settlementId, unlock)
                                                            || !HasResearchPrerequisites(settlementId,
                                                                unlock.Prerequisites)))
            return "当地尚未掌握" + unlock.Name + "及其前置";
        if (kind is BuildingKind.MountainPass or BuildingKind.Bridge &&
            !HasResearch(settlementId, Advancement.Logistics))
            return "需要先掌握驿路运输";
        if (kind is BuildingKind.Waystation or BuildingKind.Dock && !HasResearch(settlementId, Advancement.Logistics))
            return "当地尚未掌握驿路运输";
        if (kind == BuildingKind.SignalTower && !HasResearch(settlementId, Advancement.SignalNetwork))
            return "当地尚未掌握信号网络";
        if (kind == BuildingKind.ArcaneSanctum && !HasResearch(settlementId, Advancement.ArcaneArts))
            return "当地尚未掌握奥术基础";
        if (ProductionRules.For(kind) is { } advancement && (!HasResearch(settlementId, advancement.Research)
                                                             || !HasResearchPrerequisites(settlementId,
                                                                 advancement.Research.Prerequisites)))
            return "当地尚未掌握" + advancement.Research.Name + "及其前置";
        return MissingResources(town.Resources, FacilityCost(kind, bridgeLevel));
    }

    /// <summary>比较现有库存与所需成本，返回缺少的资源说明；足够时返回空值。</summary>
    /// <param name="stock">当前资源库存。</param>
    /// <param name="cost">操作所需的资源数量。</param>
    public static string? MissingResources(in ResourceStock stock, in ResourceStock cost)
    {
        var missing = new List<string>();
        foreach (var kind in ResourceStock.Kinds)
            if (stock.Get(kind) + .000001 < cost.Get(kind))
                missing.Add($"{ResourceStock.Name(kind)}缺 {cost.Get(kind) - stock.Get(kind):0.#}");
        return missing.Count == 0 ? null : string.Join("\n", missing);
    }

    /// <summary>比较现有库存与所需成本，返回缺少的资源说明；足够时返回空值。</summary>
    /// <param name="stock">当前资源库存。</param>
    /// <param name="cost">操作所需的资源数量。</param>
    public static string? MissingResources(in ResourceStock stock, ResourceAmounts cost)
    {
        var missing = new List<string>();
        foreach (var kind in ResourceStock.Kinds)
            if (stock.Get(kind) + .000001 < cost.Get(kind))
                missing.Add($"{ResourceStock.Name(kind)}缺 {cost.Get(kind) - stock.Get(kind):0.#}");
        return missing.Count == 0 ? null : string.Join("\n", missing);
    }

    /// <summary>按赐予规则直接放置完工设施，并返回新建筑 ID。</summary>
    /// <param name="settlementId">聚落 ID。</param>
    /// <param name="kind">设施类别。</param>
    /// <param name="x">横向地格坐标。</param>
    /// <param name="y">纵向地格坐标。</param>
    /// <param name="direction">桥梁通行轴向，空值时根据现场连岸条件推断。</param>
    /// <param name="bridgeLevel">建造的桥梁等级，范围为 1 至 3；普通建筑忽略此参数。</param>
    public int GrantFacility(int settlementId, BuildingKind kind, int x, int y, BridgeDirection? direction = null,
        int bridgeLevel = 1)
    {
        return PlaceFacility(settlementId, kind, x, y, true, direction, bridgeLevel);
    }

    /// <summary>判断建筑是否满足本地运营条件。</summary>
    /// <param name="building">要检查运营条件的建筑。</param>
    public bool IsBuildingOperational(Building building)
    {
        return IsFacilityOperating(building)
               && (building.Kind != BuildingKind.Well ||
                   WellWaterYield(Current.Tiles[Index(building.X, building.Y)]) > 0)
               && (ResearchRules.Unlocking(building.Kind) is not { } unlock ||
                   (HasResearch(building.SettlementId, unlock)
                    && HasResearchPrerequisites(building.SettlementId, unlock.Prerequisites)))
               && (building.Kind != BuildingKind.SignalTower ||
                   (HasResearch(building.SettlementId, Advancement.SignalNetwork) &&
                    HasResearch(building.SettlementId, Advancement.Electrification)));
    }

    /// <summary>检查笔刷范围内修建道路的条件；可修建时返回空值，否则返回原因。</summary>
    /// <param name="settlementId">聚落 ID。</param>
    /// <param name="x">横向地格坐标。</param>
    /// <param name="y">纵向地格坐标。</param>
    /// <param name="radius">道路笔刷的作用半径，以地格为单位。</param>
    public string? RoadPlacementError(int settlementId, int x, int y, int radius = 0)
    {
        if (!_settlements.TryGetValue(settlementId, out var town))
            return "先选择负责修路的聚落";
        if (!InBounds(x, y) || Distance(x, y, town.X, town.Y) > 24)
            return "距聚落超过 24 格";
        if (radius is < 0 or > 4)
            return "道路范围无效";
        var count = Circle(x, y, radius).Count(i => Current.Tiles[i].IsWalkable && Current.Tiles[i].RoadLevel == 0);
        if (count == 0)
            return "此处不可修路，或已有道路";
        return MissingResources(town.Resources, new ResourceStock { Wood = count * .5, Stone = count });
    }

    private void DeliverLocalDiscoveries(ResidentCursor person, SettlementCursor home)
    {
        foreach (var site in person.Agent.Memory.Where(f =>
                     f.Kind == AgentFactKind.FoundingSite && f.LearnedTick < Current.Tick))
        {
            var prior = home.PublicKnowledge.FirstOrDefault(f =>
                f.Kind == AgentFactKind.FoundingSite && f.SubjectId == site.SubjectId);
            if (prior is not null && prior.ObservedTick >= site.ObservedTick)
                continue;
            var delivered = site with { LearnedTick = Current.Tick, SourceResidentId = person.Id };
            AddPublicFact(home, delivered);
        }

        foreach (var report in person.Agent.Memory
                     .Where(f => f.Kind == AgentFactKind.WarReport && f.LearnedTick < Current.Tick).ToArray())
            ReceiveWarReport(home, report);
        if (person.Profession is not (Profession.Trader or Profession.Messenger or Profession.Representative))
            return;
        foreach (var fact in person.Agent.Memory.Where(f =>
                     f.LearnedTick < Current.Tick && f.Kind is AgentFactKind.SettlementLocation
                         or AgentFactKind.TradeExchange or AgentFactKind.DiplomaticNotice).ToArray())
        {
            var old = home.PublicKnowledge.FirstOrDefault(f =>
                f.Kind == fact.Kind && f.SubjectId == fact.SubjectId && f.TargetNationId == fact.TargetNationId);
            if (old is not null && old.ObservedTick >= fact.ObservedTick)
                continue;
            var delivered = fact with { LearnedTick = Current.Tick, SourceResidentId = person.Id };
            AddPublicFact(home, delivered);
            ReceiveDiplomaticNotice(home, delivered);
        }
    }

    internal void ReceiveDiplomaticNotice(SettlementCursor town, AgentFact fact)
    {
        if (fact.Kind != AgentFactKind.DiplomaticNotice || fact.SubjectId == town.NationId || fact.Value is < 0 or > 2
            || !_nations.ContainsKey(fact.SubjectId) || fact.Confidence < .4 ||
            town.Id != _nations[town.NationId].CapitalId)
            return;
        // 宣战须实际递送且指向本国，才能成为本地军令，避免机构直接读取远方事实。
        if (fact.TargetNationId != town.NationId)
            return;
        var status = (DiplomaticStatus)(int)fact.Value;
        if (status == DiplomaticStatus.Allied)
        {
            var relation = Relation(town.NationId, fact.SubjectId);
            if (!Current.Rules.Alliances || relation.Status != DiplomaticStatus.Neutral ||
                relation.AllianceOfferNationId != fact.SubjectId
                || relation.AllianceOfferTick != fact.ObservedTick || Current.Tick - fact.ObservedTick > 600)
                return;
            var knowsSender = town.PublicKnowledge.Any(f =>
                f.Kind == AgentFactKind.SettlementLocation && (int)f.Value == fact.SubjectId && f.Confidence >= .4 &&
                Current.Tick - f.ObservedTick < 1200);
            if (!knowsSender)
                return;
            relation = PublishRelation(relation with
            {
                Status = DiplomaticStatus.Allied,
                LastChangedTick = Current.Tick,
                AllianceOfferNationId = 0,
                Reason = "结盟提议已实际送达，对方依据已有接触消息接受",
            });
            var alliance = AddEvent(WorldEventKind.Diplomacy,
                $"{_nations[town.NationId].Name}收到并接受{_nations[fact.SubjectId].Name}的结盟提议。", town.X, town.Y);
            alliance = PublishEvent(alliance with
            {
                SecondNationId = fact.SubjectId,
                CauseEventId = relation.LastEventId,
            });
            relation = PublishRelation(relation with { LastEventId = alliance.Id });
            return;
        }

        var prior = town.PublicKnowledge.Where(f =>
                f.SubjectId == fact.SubjectId && f.Kind is AgentFactKind.WarOrder or AgentFactKind.PeaceOrder)
            .OrderByDescending(f => f.ObservedTick).FirstOrDefault();
        if (prior is not null && prior.ObservedTick >= fact.ObservedTick)
            return;
        if (status == DiplomaticStatus.Neutral)
            PublishRelation(Relation(town.NationId, fact.SubjectId).WithLocalOpinion(town.NationId, 0));
        PublishDiplomaticOrder(town.NationId, fact.SubjectId, status, fact.X, fact.Y, fact.ObservedTick,
            eventId: fact.EventId, objective: WarObjective.DefendHomeland);
    }

    private static int LocalOpinion(DiplomaticRelation relation, int nationId)
    {
        return nationId == relation.FirstNationId
            ? relation.FirstOpinion
            : relation.SecondOpinion;
    }

    private void TickDiplomacy()
    {
        if (Current.Tick % 60 != 0)
            return;
        var assessments = new List<DiplomaticAssessment>();
        foreach (var nation in Current.Nations)
        {
            if (!_settlements.TryGetValue(nation.CapitalId, out var capital))
                continue;
            var contacts = capital.PublicKnowledge.Where(f => f.Kind == AgentFactKind.SettlementLocation &&
                                                              f.LearnedTick < Current.Tick
                                                              && f.Confidence >= .4 &&
                                                              Current.Tick - f.ObservedTick <= 1200 &&
                                                              f.Value != nation.Id && f.Value > 0)
                .GroupBy(f => (int)f.Value).Select(g => g.OrderByDescending(f => f.ObservedTick).First())
                .OrderBy(f => f.Value).ToArray();
            foreach (var contact in contacts)
            {
                var otherId = (int)contact.Value;
                if (!_nations.TryGetValue(otherId, out var other))
                    continue;
                var relation = Relation(nation.Id, otherId);
                if (relation.LastEvaluatedTick == Current.Tick)
                    continue;
                var ownFood = capital.Resources.Food;
                var cooperation = GetCulture(capital.CultureId).Cooperation;
                var tradeReport = capital.PublicKnowledge
                    .Where(f => f.Kind == AgentFactKind.TradeExchange && f.SubjectId == otherId &&
                                Current.Tick - f.ObservedTick <= 360).OrderByDescending(f => f.ObservedTick)
                    .FirstOrDefault();
                var trade = tradeReport is not null;
                var nearby = Distance(capital.X, capital.Y, contact.X, contact.Y) <= 28;
                var otherFood = capital.PublicKnowledge
                    .Where(f => f.Kind == AgentFactKind.FoodSupply && f.SubjectId == contact.SubjectId &&
                                f.LearnedTick < Current.Tick && f.Confidence >= .5 &&
                                Current.Tick - f.ObservedTick <= 180)
                    .OrderByDescending(f => f.ObservedTick).FirstOrDefault();
                var bothScarce = ownFood < capital.Population && otherFood is { Value: < 12 };
                var abundant = ownFood >= capital.Population * 3 && otherFood is { Value: >= 36 };
                var pressure = nearby &&
                               (ownFood < capital.Population || GetLocalPolicy(capital.Id) == PolicyKind.Defense);
                var change = trade ? 10 :
                    abundant ? 6 :
                    pressure ? -(3 + Current.Rules.Conflict * (bothScarce ? 6 : 3)) :
                    cooperation >= .55 ? 4 :
                    Current.Rules.Conflict >= 2 && nearby && ownFood < capital.Population * 2 ? -5 : 1;
                var reason = trade ? "收到实际贸易交付的报告，往来改善关系" :
                    bothScarce ? "本地缺粮，收到的对方粮情也显示短缺，邻近资源竞争加剧" :
                    abundant ? "本地资源充足，收到的对方粮情也充足，争夺意愿减弱" :
                    pressure ? "收到邻近聚落的消息，本地粮食或防务压力加剧竞争" : "依据已经送达的聚落消息与当地合作倾向评估关系";
                assessments.Add(new DiplomaticAssessment(nation, other, capital, contact, relation, ownFood, change,
                    reason, tradeReport));
            }
        }

        // 各首都只使用自己收到的评估和地点；展示用的双方态度均值不能进入国家决策。
        foreach (var group in assessments.GroupBy(a => a.Relation)
                     .OrderBy(group => group.Key.FirstNationId).ThenBy(group => group.Key.SecondNationId))
        {
            var sides = group.OrderBy(a => a.Capital.X).ThenBy(a => a.Capital.Y)
                .ThenBy(a => a.Contact.X).ThenBy(a => a.Contact.Y).ThenBy(a => a.Nation.Id).ToArray();
            var relation = group.Key;
            relation = relation with { LastEvaluatedTick = Current.Tick };
            relation = relation with { LastContactTick = Math.Max(relation.LastContactTick, sides.Max(a => a.Contact.ObservedTick)) };
            foreach (var side in sides)
            {
                relation = relation.WithLocalOpinion(side.Nation.Id, LocalOpinion(relation, side.Nation.Id) + side.Change);
                var first = side.Nation.Id == relation.FirstNationId;
                var started = first ? relation.FirstEscalationTick : relation.SecondEscalationTick;
                if (LocalOpinion(relation, side.Nation.Id) <= -25 && side.Change < 6 &&
                    relation.Status != DiplomaticStatus.War)
                {
                    if (started == 0)
                    {
                        started = Current.Tick;
                        var dispute = AddEvent(WorldEventKind.Diplomacy,
                            $"{side.Nation.Name}与{side.Other.Name}的竞争发展为外交争端：{side.Reason}。", side.Capital.X,
                            side.Capital.Y, causeEventId: relation.LastEventId);
                        dispute = PublishEvent(dispute with
                        {
                            SecondNationId = side.Other.Id,
                            Importance = EventImportance.Notable,
                        });
                        relation = relation with { LastEventId = dispute.Id };
                    }
                }
                else if (LocalOpinion(relation, side.Nation.Id) > -25 || side.Change >= 6)
                    started = 0;

                if (first)
                    relation = relation with { FirstEscalationTick = started };
                else
                    relation = relation with { SecondEscalationTick = started };
            }

            relation = relation with { Reason = sides.Length == 1 ? sides[0].Reason : "双方各自依据已送达消息与当地情况累计态度；所示关系为双方态度均值" };
            relation = PublishRelation(relation);
            if (Current.Tick - relation.LastChangedTick < 360)
                continue;
            // 每轮只处理一种外交动作，停战或宣战优先于结盟，避免同日立即反转关系。
            if (relation.Status == DiplomaticStatus.War)
            {
                var peacemaker = sides.Where(a => Current.Rules.Peace && (Current.Tick - relation.LastChangedTick >= 720
                                                                          || a.Food < Math.Max(10,
                                                                              a.Capital.Population * .5)))
                    .OrderBy(a => a.Food / Math.Max(10, a.Capital.Population * .5)).FirstOrDefault();
                if (peacemaker is not null)
                {
                    ChangeAutonomousDiplomacy(peacemaker.Nation, peacemaker.Other, relation, peacemaker.Contact,
                        DiplomaticStatus.Neutral, "战事持续或本地补给不足，宣布停战并休养");
                }

                continue;
            }

            var declarer = sides.Where(a => Current.Rules.Wars && Current.Tick >= a.Nation.Military.RecoveryUntilTick &&
                                            Current.Rules.Conflict > 0 && LocalOpinion(relation, a.Nation.Id) <= -55
                                            && a.Food > 20 && a.Capital.Population >= 18
                                            && (a.Nation.Id == relation.FirstNationId
                                                ? relation.FirstEscalationTick
                                                : relation.SecondEscalationTick) > 0
                                            && Current.Tick - (a.Nation.Id == relation.FirstNationId
                                                ? relation.FirstEscalationTick
                                                : relation.SecondEscalationTick) >= 180)
                .OrderBy(a => LocalOpinion(relation, a.Nation.Id)).FirstOrDefault();
            if (declarer is not null)
            {
                ChangeAutonomousDiplomacy(declarer.Nation, declarer.Other, relation, declarer.Contact,
                    DiplomaticStatus.War, declarer.Reason);
                continue;
            }

            if (!Current.Rules.Alliances || relation.Status != DiplomaticStatus.Neutral
                                         || (relation.AllianceOfferNationId != 0 &&
                                             Current.Tick - relation.AllianceOfferTick <= 600))
                continue;
            var proposer = sides.Where(a => LocalOpinion(relation, a.Nation.Id) >= 55)
                .OrderByDescending(a => LocalOpinion(relation, a.Nation.Id)).FirstOrDefault();
            if (proposer is null)
                continue;
            var nation = proposer.Nation;
            var other = proposer.Other;
            var capital = proposer.Capital;
            relation = PublishRelation(relation with
            {
                AllianceOfferNationId = nation.Id,
                AllianceOfferTick = Current.Tick,
                Reason = "友好往来促成结盟提议，等待实际送达与回应",
            });
            AddPublicFact(capital, new AgentFact
            {
                Id = NewId(),
                Kind = AgentFactKind.DiplomaticNotice,
                SubjectId = nation.Id,
                TargetNationId = other.Id,
                Value = (int)DiplomaticStatus.Allied,
                X = capital.X,
                Y = capital.Y,
                ObservedTick = Current.Tick,
                LearnedTick = Current.Tick,
                OriginResidentId = capital.RepresentativeId,
                SourceResidentId = capital.RepresentativeId,
                OriginProfession = Profession.Representative,
                Text = "友好往来促成结盟提议，请对方议事回应",
            });
            var proposal = AddEvent(WorldEventKind.Diplomacy, $"{nation.Name}向{other.Name}提出结盟，等待消息实际送达。", capital.X,
                capital.Y);
            proposal = PublishEvent(proposal with
            {
                Action = EventAction.Declaration,
                SettlementId = capital.Id,
                EvidenceFactId = proposer.TradeReport?.Id ?? proposer.Contact.Id,
                SecondNationId = other.Id,
                CauseEventId = proposer.TradeReport?.EventId > 0
                    ? proposer.TradeReport.EventId
                    : relation.LastEventId,
            });
            relation = PublishRelation(relation with { LastEventId = proposal.Id });
        }
    }

    private void ChangeAutonomousDiplomacy(NationCursor nation, NationCursor other, DiplomaticRelation relation,
        AgentFact contact,
        DiplomaticStatus status, string reason)
    {
        var previous = relation.LastEventId;
        relation = PublishRelation(relation with { Status = status, LastChangedTick = Current.Tick, Reason = reason });
        if (status == DiplomaticStatus.Neutral)
            relation = PublishRelation(relation.WithLocalOpinion(nation.Id, 0));
        var capital = _settlements[nation.CapitalId];
        var entry = AddEvent(status == DiplomaticStatus.War ? WorldEventKind.War : WorldEventKind.Diplomacy,
            $"{nation.Name}与{other.Name}{(status == DiplomaticStatus.War ? "开战" : status == DiplomaticStatus.Allied ? "结盟" : "停战")}：{reason}。消息须实际传往对方与前线。",
            capital.X, capital.Y);
        entry = PublishEvent(entry with
        {
            SecondNationId = other.Id,
            CauseEventId = contact.EventId > 0 ? contact.EventId : previous,
        });
        if (previous > 0 && previous != entry.CauseEventId)
            entry = PublishEvent(entry with { AdditionalCauseEventIds = entry.AdditionalCauseEventIds.Add(previous) });
        entry = PublishEvent(entry with
        {
            Importance = EventImportance.Major,
            Action = EventAction.Declaration,
            SettlementId = capital.Id,
            EvidenceFactId = contact.Id,
        });
        PublishDiplomaticOrder(nation.Id, other.Id, status, contact.X, contact.Y,
            targetSettlementId: contact.Kind == AgentFactKind.SettlementLocation ? contact.SubjectId : 0,
            eventId: entry.Id);
        AddPublicFact(capital, new AgentFact
        {
            Id = NewId(),
            EventId = entry.Id,
            Kind = AgentFactKind.DiplomaticNotice,
            SubjectId = nation.Id,
            TargetNationId = other.Id,
            Value = (int)status,
            X = capital.X,
            Y = capital.Y,
            ObservedTick = Current.Tick,
            LearnedTick = Current.Tick,
            OriginResidentId = capital.RepresentativeId,
            SourceResidentId = capital.RepresentativeId,
            OriginProfession = Profession.Representative,
            Text = $"{nation.Name}的外交声明：{reason}",
        });

        relation = PublishRelation(relation with { LastEventId = entry.Id });
    }

    private void TickMigrationAndSecession()
    {
        if (Current.Tick % 60 != 0)
            return;
        foreach (var town in Current.Settlements.ToArray())
        {
            var reports = Current.Society.Reports.Where(r =>
                    r.RecipientSettlementId == town.Id && r.Confidence >= .5 && Current.Tick - r.ObservedTick < 180)
                .ToArray();
            var hardship = reports.Any(r => r.Topic == AgentFactKind.ReliefRequest && r.Value > 55);
            town.Unrest = Math.Clamp(town.Unrest + (hardship ? 5 + Current.Rules.Conflict : -4), 0, 100);
            if (Current.Rules.Secession && town.Unrest >= 80 && Current.Tick - town.LastPoliticalChangeTick >= 1200
                && Current.Nations.Count < 64 && _nations[town.NationId].CapitalId != town.Id && town.Population >= 12
                && Current.Settlements.Count(t => t.NationId == town.NationId) > 1)
            {
                var parent = town.NationId;
                var id = SplitSettlement(town.Id, town.Name + "自由邦");
                town.Replace(town.Value with { Unrest = 20, LastPoliticalChangeTick = Current.Tick });
                var entry = AddEvent(WorldEventKind.Founding, $"{town.Name}长期收到未解决的困苦诉求，宣布自治建国。", town.X, town.Y);
                entry = PublishEvent(entry with
                {
                    NationId = id,
                    SecondNationId = parent,
                    Action = EventAction.Secession,
                    SettlementId = town.Id,
                });
                var evidence = reports.Where(r => r.Topic == AgentFactKind.ReliefRequest && r.Value > 55)
                    .OrderByDescending(r => r.ObservedTick).FirstOrDefault();
                entry = PublishEvent(entry with
                {
                    EvidenceFactId = evidence?.FactId ?? 0,
                    CauseEventId = evidence?.EventId ?? 0,
                });
            }
        }

        if (!Current.Rules.Migration)
            return;
        foreach (var person in Current.Residents.Where(r =>
                         r.Age >= 16 && r.ArmyId == 0 && r.Hunger > 65 && r.Agent.DestinationSettlementId == 0
                         && !r.Agent.Goal.PlayerDirected && r.Agent.Goal.Kind != AgentGoalKind.Migrate)
                     .OrderBy(r => r.Id)
                     .ToArray())
        {
            if (person.Agent.Goal.Kind is AgentGoalKind.Gather or AgentGoalKind.Hunt or AgentGoalKind.Fish
                && person.Inventory.Food < FoodUse(person) * 8)
                continue;
            var destination = person.Agent.Memory.Where(f => f.Kind == AgentFactKind.FoodSupply &&
                                                             f.SubjectId != person.SettlementId
                                                             && f.Value > 50 && f.ReliabilityAt(Current.Tick) >= .5)
                .OrderByDescending(f => f.Value).FirstOrDefault();
            if (destination is null || !_settlements.TryGetValue(destination.SubjectId, out var town))
                continue;
            if (IsKnownHostile(person, town.NationId))
                continue;
            person.Replace(person.Value with
            {
                Agent = person.Agent with
                {
                    Goal = new AgentGoal
                    {
                        Kind = AgentGoalKind.Migrate,
                        TargetX = destination.X,
                        TargetY = destination.Y,
                        TargetSettlementId = town.Id,
                        StartedTick = Current.Tick,
                        ReviewTick = Current.Tick + 360,
                        EvidenceFactId = destination.Id,
                        CauseEventId = destination.EventId,
                        Reason = "长期饥饿，依据收到的粮情步行寻找可接纳的新家园",
                    },
                    NextThinkTick = Current.Tick + 6,
                },
            });
        }
    }

    private void ActOnMigration(ResidentCursor person)
    {
        var goal = person.Agent.Goal;
        if (Distance(person.X, person.Y, goal.TargetX, goal.TargetY) > 1)
        {
            MoveAgentTowards(person, goal.TargetX, goal.TargetY);
            person.Activity = ResidentActivity.Wandering;
            return;
        }

        if (!_settlements.TryGetValue(goal.TargetSettlementId, out var town) || Distance(person.X, person.Y, town.X,
                                                                                 town.Y) > 1
                                                                             || town.Resources.Food < 10 ||
                                                                             _citizens[town.Id].Count >=
                                                                             GetHousingCapacity(town.Id) ||
                                                                             IsKnownHostile(person, town.NationId))
        {
            person.Replace(person.Value with
            {
                Agent = person.Agent with
                {
                    Goal = person.Agent.Goal with { Kind = AgentGoalKind.Idle },
                    NextThinkTick = Current.Tick,
                },
            });
            return;
        }

        var old = person.SettlementId;
        person.Replace(person.Value with { SettlementId = town.Id, NationId = town.NationId });
        UpdateLocalWorkMembership(person, old);
        person.Agent = person.Agent.WithGoal(new AgentGoal
        {
            Kind = AgentGoalKind.ReturnHome,
            TargetX = town.X,
            TargetY = town.Y,
            TargetSettlementId = town.Id,
            StartedTick = Current.Tick,
            Reason = "实地抵达后确认新家园可以接纳",
        });
        RememberAgentFact(person,
            MakeAgentFact(person, AgentFactKind.SettlementLocation, town.Id, town.X, town.Y, town.NationId,
                "步行抵达的新家园"));
        if (_citizens.TryGetValue(old, out var previous))
            previous.Remove(person);
        _citizens[town.Id].Add(person);
        var entry = AddEvent(WorldEventKind.Growth, $"{person.Name}依据获知的粮情，步行迁入{town.Name}。", town.X, town.Y);
        entry = PublishEvent(entry with
        {
            ResidentId = person.Id,
            Action = EventAction.Migration,
            SettlementId = town.Id,
            SecondSettlementId = old,
            EvidenceFactId = goal.EvidenceFactId,
            CauseEventId = goal.CauseEventId,
        });
        RecordLife(person, $"从原家园步行迁入{town.Name}，抵达后获接纳。", entry);
    }

    private sealed record DiplomaticAssessment(
        NationCursor Nation,
        NationCursor Other,
        SettlementCursor Capital,
        AgentFact Contact,
        DiplomaticRelation Relation,
        double Food,
        int Change,
        string Reason,
        AgentFact? TradeReport);
}
