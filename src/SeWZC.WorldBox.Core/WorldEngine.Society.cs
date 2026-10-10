using SeWZC.WorldBox.Core.Runtime;

namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    /// <summary>世界设施记录数量的上限。</summary>
    public const int MaxBuildings = 1_536;

    private readonly HashSet<int> _societyResidentIds = [];

    /// <summary>为当前世界及新聚落补齐社会默认状态，已存在的记录保留。</summary>
    public void InitializeSociety()
    {
        if (Society.Cultures.Count == 0)
        {
            Society = Society with
            {
                Cultures = Society.Cultures.AddRange([
                    new CultureDefinition
                    {
                        Id = 1,
                        Name = "河谷互助",
                        Cooperation = 0.85,
                        Innovation = 0.45,
                        NatureAffinity = 0.65,
                    },
                    new CultureDefinition
                    {
                        Id = 2,
                        Name = "山地工艺",
                        Cooperation = 0.55,
                        Innovation = 0.85,
                        NatureAffinity = 0.3,
                    },
                    new CultureDefinition
                    {
                        Id = 3,
                        Name = "林地共生",
                        Cooperation = 0.65,
                        Innovation = 0.5,
                        NatureAffinity = 0.95,
                    },
                    new CultureDefinition
                    {
                        Id = 4,
                        Name = "远行求知",
                        Cooperation = 0.4,
                        Innovation = 0.95,
                        NatureAffinity = 0.45,
                    },
                ]),
            };
        }

        foreach (var nation in Nations)
        {
            var capital = Settlements.FirstOrDefault(s => s.Value.Id == nation.Value.CapitalId);
            if (!Society.Cultures.Any(c => c.Id == nation.Value.CultureId))
            {
                nation.Replace(nation.Value with { CultureId = capital is null ? Society.Cultures[0].Id : InitialCulture(capital.Value.X, capital.Value.Y) });
            }

            if (!Society.Institutions.Any(i => i.NationId == nation.Value.Id))
                Society = Society with
                {
                    Institutions = Society.Institutions.Add(new NationInstitution { NationId = nation.Value.Id }),
                };
        }

        EnsureTownCenters();
        foreach (var town in Settlements)
        {
            if (!Society.Cultures.Any(c => c.Id == town.Value.CultureId))
            {
                town.Replace(town.Value with { CultureId = Nations.FirstOrDefault(n => n.Value.Id == town.Value.NationId)?.Value.CultureId ??
                                 Society.Cultures[0].Id });
            }

            if (!Society.Policies.Any(p => p.SettlementId == town.Value.Id))
            {
                var manual = Society.Institutions.FirstOrDefault(i => i.NationId == town.Value.NationId)
                    ?.PlayerPolicy;
                Society = Society with
                {
                    Policies = Society.Policies.Add(new LocalPolicy
                    {
                        SettlementId = town.Value.Id,
                        Kind = manual ?? PolicyKind.Balanced,
                        PlayerOverride = manual.HasValue,
                    }),
                };
            }

            if (Society.Research.Any(r => r.SettlementId == town.Value.Id))
                continue;
            Society = Society with
            {
                Research = Society.Research.Add(new SettlementResearch { SettlementId = town.Value.Id }),
            };
            // 定居家庭携带初始农场和工坊，后续设施仍须实际建设。
            if (!town.Value.FoundationPending)
            {
                AddFoundingFacility(town, BuildingKind.Farm);
                AddFoundingFacility(town, BuildingKind.Workshop);
                AddFoundingFacility(town, BuildingKind.Housing);
            }
        }

        foreach (var resident in Residents)
        {
            if (Society.Cultures.Any(c => c.Id == resident.Value.CultureId))
                continue;
            resident.Replace(resident.Value with { CultureId = Settlements.FirstOrDefault(s => s.Value.Id == resident.Value.SettlementId)?.Value.CultureId ??
                                 Society.Cultures[0].Id });
            var baseTalent =
                resident.Value.Race switch
                {
                    RaceKind.Elf => 45,
                    RaceKind.Dwarf => 23,
                    RaceKind.Orc => 28,
                    _ => 32,
                };
            resident.Replace(resident.Value with { MagicTalent = baseTalent + unchecked(((uint)resident.Value.Id * 2654435761u) ^ (uint)Seed) % 36 });
        }

        ReconcileSocietyTopology();
        RefreshLocalRepresentatives();
        BalanceLocalWorkforce();
    }

    private int InitialCulture(int x, int y)
    {
        var preferred = Tiles[Index(x, y)].Value.Terrain switch
        {
            TerrainType.Sand or TerrainType.Desert => 4,
            TerrainType.Forest => 3,
            TerrainType.Snow or TerrainType.Hills or TerrainType.Tundra => 2,
            _ => 1,
        };
        return Society.Cultures.Any(c => c.Id == preferred) ? preferred : Society.Cultures[0].Id;
    }

    private void AddFoundingFacility(StateReference<Settlement> town, BuildingKind kind)
    {
        if (Buildings.Count >= MaxBuildings - 256)
            return;
        var position = BestBuildingSite(town, kind, true);
        if (position >= 0)
        {
            var building = new StateReference<Building>(new Building
            {
                Id = NewId(),
                SettlementId = town.Value.Id,
                Kind = kind,
                X = position % Width,
                Y = position / Width,
                ConstructionProgress = 30,
                ConstructionRequired = 30,
            });
            Buildings.Add(building);
            CompleteLandImprovement(building.Value);
        }
    }

    /// <summary>返回建造一级设施所需的资源成本。</summary>
    /// <param name="kind">设施类别。</param>
    public static ResourceStock GetBuildingCost(BuildingKind kind)
    {
        return kind switch
        {
            BuildingKind.Farm => new ResourceStock { Wood = 12, Stone = 3 },
            BuildingKind.Workshop => new ResourceStock { Wood = 18, Stone = 10 },
            BuildingKind.Academy => new ResourceStock { Food = 20, Wood = 30, Stone = 15 },
            BuildingKind.Waystation => new ResourceStock { Wood = 20, Stone = 15 },
            BuildingKind.SignalTower => new ResourceStock { Stone = 30, Alloy = 8, EnergyCells = 5 },
            BuildingKind.ArcaneSanctum => new ResourceStock { Food = 15, Wood = 20, Stone = 25, Ore = 12 },
            BuildingKind.Infirmary => new ResourceStock { Food = 10, Wood = 25, Stone = 10 },
            BuildingKind.MountainPass => new ResourceStock { Wood = 6, Stone = 10 },
            BuildingKind.Bridge => new ResourceStock { Wood = 8, Stone = 4 },
            BuildingKind.Dock => new ResourceStock { Wood = 20, Stone = 12 },
            BuildingKind.TownCenter => new ResourceStock { Wood = 12, Stone = 3 },
            BuildingKind.LumberCamp => new ResourceStock { Wood = 16, Stone = 8 },
            BuildingKind.Quarry => new ResourceStock { Wood = 20, Stone = 12 },
            BuildingKind.Well => new ResourceStock { Wood = 12, Stone = 18 },
            BuildingKind.Granary => new ResourceStock { Wood = 28, Stone = 18 },
            BuildingKind.Housing => new ResourceStock { Wood = 25, Stone = 8 },
            BuildingKind.Market => new ResourceStock { Food = 15, Wood = 30, Stone = 15 },
            BuildingKind.Watchtower => new ResourceStock { Wood = 25, Stone = 20 },
            BuildingKind.AssemblyHall or BuildingKind.TradeGuild => new ResourceStock
            {
                Food = 15, Wood = 30, Stone = 20,
            },
            BuildingKind.SacredGrove or BuildingKind.HerbGarden => new ResourceStock
            {
                Food = 20, Wood = 25, Stone = 15,
            },
            BuildingKind.MiningHall => new ResourceStock { Wood = 20, Stone = 35, Ore = 8 },
            BuildingKind.HuntingCamp or BuildingKind.WarDrum => new ResourceStock { Food = 15, Wood = 25, Stone = 10 },
            BuildingKind.Reservoir => new ResourceStock { Wood = 15, Stone = 25 },
            BuildingKind.Hospital => new ResourceStock { Wood = 25, Stone = 25 },
            BuildingKind.FireStation => new ResourceStock { Wood = 20, Stone = 20 },
            BuildingKind.Library or BuildingKind.SurveyOffice => new ResourceStock { Wood = 25, Stone = 15 },
            BuildingKind.Armory => new ResourceStock { Wood = 20, Stone = 20, Alloy = 8 },
            BuildingKind.WardTower or BuildingKind.StormSpire => new ResourceStock { Stone = 25, Crystals = 10 },
            BuildingKind.GroveSanctuary => new ResourceStock { Wood = 15, Stone = 15, Crystals = 8 },
            BuildingKind.Waygate => new ResourceStock { Stone = 30, Crystals = 15 },
            BuildingKind.Pasture => new ResourceStock { Wood = 18, Stone = 5 },
            BuildingKind.Aquaculture => new ResourceStock { Wood = 25, Stone = 18, Alloy = 5 },
            _ => ProductionRules.For(kind)?.BuildingCost.ToStock() ??
                 throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }

    /// <summary>返回开始指定研究所需的资源成本。</summary>
    /// <param name="research">研究项目。</param>
    public static ResourceStock GetResearchCost(Advancement research)
    {
        return research.Cost.ToStock();
    }

    /// <summary>创建待施工设施并扣除聚落材料，返回建筑 ID。</summary>
    /// <param name="settlementId">聚落 ID。</param>
    /// <param name="kind">设施类别。</param>
    /// <param name="x">横向地格坐标。</param>
    /// <param name="y">纵向地格坐标。</param>
    /// <param name="direction">桥梁通行轴向，空值时根据现场连岸条件推断。</param>
    /// <param name="bridgeLevel">建造的桥梁等级，范围为 1 至 3；普通建筑忽略此参数。</param>
    public int BuildFacility(int settlementId, BuildingKind kind, int x, int y, BridgeDirection? direction = null,
        int bridgeLevel = 1)
    {
        return PlaceFacility(settlementId, kind, x, y, false, direction, bridgeLevel);
    }

    private int PlaceFacility(int settlementId, BuildingKind kind, int x, int y, bool gift,
        BridgeDirection? direction = null, int bridgeLevel = 1)
    {
        if (FacilityPlacementError(settlementId, kind, x, y, gift, direction, bridgeLevel) is { } error)
            throw new InvalidOperationException(error);
        var town = RequireTown(settlementId);
        var tile = Tiles[Index(x, y)];
        if (!gift)
            town.Replace(town.Value.WithResources(Spend(town.Value.Resources, FacilityCost(kind, bridgeLevel))));
        var building = new StateReference<Building>(new Building
        {
            Id = NewId(),
            SettlementId = settlementId,
            Kind = kind,
            X = x,
            Y = y,
            Level = kind == BuildingKind.Bridge ? bridgeLevel : 1,
            Direction = kind == BuildingKind.Bridge
                ? direction ?? InferBridgeDirection(x, y)
                : BridgeDirection.Horizontal,
            ConstructionRequired = kind is BuildingKind.SignalTower or BuildingKind.ArcaneSanctum ? 60 : 30,
            WorkSlots = (kind == BuildingKind.Farm ? 5 : 3) + (kind == BuildingKind.Bridge ? bridgeLevel - 1 : 0),
        });
        Buildings.Add(building);
        if (_localWorkQueriesActive)
        {
            _workBuildingsById[building.Value.Id] = building;
            LocalWorkGroup(_localWorkBuildings, _localWorkBuildingBuffers, settlementId).Add(building);
        }

        if (gift)
        {
            building.Replace(building.Value with { ConstructionProgress = building.Value.ConstructionRequired });
            if (IsForestTerrain(tile.Value.Terrain) && !PreserveBuildingForest(kind))
            {
                tile.Replace(tile.Value.WithTerrain(TerrainType.Grass));
                tile.Replace(tile.Value.WithResourceAmount(0));
            }

            CompleteLandImprovement(building.Value);
            EmitVisual(WorldVisualKind.Construction, x, y);
        }

        // 水上项目起点尚未登记，须在本城镇陆岸旁先登记施工地块，否则归属检查会阻止首次施工。
        if (gift || IsWaterfrontBuilding(kind))
            RegisterBuildingGround(building.Value);
        var projectEvent = AddEvent(WorldEventKind.Construction,
            gift
                ? $"玩家向{town.Value.Name}赐予{BuildingName(kind)}；效果受建筑健康、启用与当地条件限制。"
                : $"{town.Value.Name}备好材料，开始修建{BuildingName(kind)}；居民必须到场施工。", x, y,
            gift ? EventAction.Gifted : EventAction.Started, town.Value.Id);
        building.Replace(building.Value with { Observation = building.Value.Observation with { StartEventId = projectEvent.Id } });
        building.Replace(building.Value with { Observation = building.Value.Observation.Observe(SimulationTick, Rules.DevelopmentRate, building.Value.ConstructionProgress) });
        RefreshTotals();
        return building.Value.Id;
    }

    /// <summary>校验并扣除本地材料，在笔刷范围内没有道路的可通行地格修建道路。</summary>
    /// <param name="settlementId">聚落 ID。</param>
    /// <param name="x">横向地格坐标。</param>
    /// <param name="y">纵向地格坐标。</param>
    /// <param name="radius">道路笔刷的作用半径，以地格为单位。</param>
    public void BuildRoad(int settlementId, int x, int y, int radius = 1)
    {
        var town = RequireTown(settlementId);
        if (!InBounds(x, y) || Distance(x, y, town.Value.X, town.Value.Y) > 24)
            throw new ArgumentException("道路须位于聚落周边 24 格内。");
        if (radius is < 0 or > 4)
            throw new ArgumentOutOfRangeException(nameof(radius), "道路笔刷半径须在 0 到 4 之间。");
        var tiles = Circle(x, y, radius).Where(i => Tiles[i].Value.IsWalkable && Tiles[i].Value.RoadLevel == 0)
            .ToArray();
        if (tiles.Length == 0)
            throw new InvalidOperationException("笔刷内没有可修建道路的土地。");
        town.Replace(town.Value.WithResources(Spend(town.Value.Resources, new ResourceStock { Wood = tiles.Length * 0.5, Stone = tiles.Length })));
        foreach (var index in tiles)
            Tiles[index].Replace(Tiles[index].Value.WithRoadLevel(1));
        RefreshTotals();
    }

    /// <summary>启动聚落研究项目。</summary>
    /// <param name="settlementId">聚落 ID。</param>
    /// <param name="project">研究项目。</param>
    public void StartResearch(int settlementId, Advancement project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var town = RequireTown(settlementId);
        var research = Society.Research.First(r => r.SettlementId == settlementId);
        if (research.Completed.Contains(project))
            throw new InvalidOperationException("当地已经掌握这项知识。");
        if (research.ActiveProject is not null)
            throw new InvalidOperationException("当地已有正在进行的研究。");
        if (!Buildings.Any(b =>
                b.Value.SettlementId == settlementId && b.Value.Kind == BuildingKind.Academy && b.Value.IsCompleted))
            throw new InvalidOperationException("研究需要已建成的学舍与实际到场的研究人员。");
        if (ResearchPrerequisiteError(settlementId, project) is { } prerequisite)
            throw new InvalidOperationException(prerequisite);
        town.Replace(town.Value.WithResources(Spend(town.Value.Resources, GetResearchCost(project))));
        var start = AddEvent(WorldEventKind.Research, $"{town.Value.Name}投入材料，开始研究{project.Name}。", town.Value.X, town.Value.Y,
            EventAction.Started, town.Value.Id);
        PublishResearch(research.Begin(project, start.Id, SimulationTick, Rules.DevelopmentRate));
        RefreshTotals();
    }

    /// <summary>判断指定聚落是否已经掌握某项研究。</summary>
    /// <param name="settlementId">聚落 ID。</param>
    /// <param name="project">研究项目。</param>
    public bool HasResearch(int settlementId, Advancement project)
    {
        return _knowledgeQueriesActive
            ? (uint)project.Id < 64 && (_knowledgeByTown.GetValueOrDefault(settlementId) & (1UL << project.Id)) != 0
            : FindSettlementResearch(settlementId)?.Completed
                .Contains(project) == true;
    }

    private SettlementResearch? FindSettlementResearch(int settlementId)
    {
        if (_localWorkQueriesActive && _localResearch.TryGetValue(settlementId, out var index))
            return Society.Research[index];
        foreach (var research in Society.Research)
            if (research.SettlementId == settlementId)
                return research;
        return null;
    }

    /// <summary>查询聚落所属国家的古代工具等级。</summary>
    /// <param name="settlementId">聚落 ID。</param>
    public int GetLocalTechnologyLevel(int settlementId)
    {
        return 1 + (Society.Research.FirstOrDefault(r => r.SettlementId == settlementId)?.Completed
            .Count(k => k.ImprovesBasicTechnology) ?? 0);
    }

    /// <summary>将实际收到的研究知识记入聚落，关联其递送依据和前因。</summary>
    /// <param name="settlementId">聚落 ID。</param>
    /// <param name="project">研究项目。</param>
    /// <param name="causeEventId">关联的前因事件 ID，0 表示未指定前因。</param>
    /// <param name="evidenceFactId">关联的信息依据 ID，0 表示未指定依据。</param>
    public void GrantReceivedResearch(int settlementId, Advancement project, int causeEventId = 0,
        int evidenceFactId = 0)
    {
        ArgumentNullException.ThrowIfNull(project);
        var town = RequireTown(settlementId);
        var research = Society.Research.First(r => r.SettlementId == settlementId);
        if (research.Completed.Contains(project))
            return;
        research = PublishResearch(research.Learn(project));
        if (_knowledgeQueriesActive)
            _knowledgeByTown[settlementId] = _knowledgeByTown.GetValueOrDefault(settlementId) | (1UL << project.Id);
        if (_nations.TryGetValue(town.Value.NationId, out var nation))
            nation.Replace(nation.Value with { Technology = Math.Max(nation.Value.Technology, GetLocalTechnologyLevel(town.Value.Id)) });
        var entry = AddEvent(WorldEventKind.Research, $"{town.Value.Name}掌握了{project.Name}；知识可由当地居民和信使继续传授。", town.Value.X,
            town.Value.Y,
            EventAction.Completed, town.Value.Id, causeEventId: causeEventId, evidenceFactId: evidenceFactId);
        research = PublishResearch(research with { LastCompletionEventId = entry.Id });
        if (research.ActiveProject == project)
        {
            if (research.Observation.StartEventId > 0 && research.Observation.StartEventId != causeEventId)
                entry = PublishEvent(entry with
                {
                    AdditionalCauseEventIds = entry.AdditionalCauseEventIds.Add(research.Observation.StartEventId),
                });
            foreach (var person in Residents.Where(r => research.Observation.Contributors.Contains(r.Value.Id)))
                RecordLife(person, $"参与{town.Value.Name}的{project.Name}研究，现已掌握成果。", entry,
                    PersonalExperienceKind.Learning);
            research = PublishResearch(research with
            {
                ActiveProject = null, Progress = 0, RequiredProgress = 0, Observation = new ProjectObservation(),
            });
        }
    }

    /// <summary>查找符合居民职业及本地条件的劳动地点，返回是否找到目标。</summary>
    /// <param name="resident">劳动的居民。</param>
    /// <param name="x">找到的劳动地点横向地格坐标；失败时为居民当前位置。</param>
    /// <param name="y">找到的劳动地点纵向地格坐标；失败时为居民当前位置。</param>
    public bool TryGetLocalWorkTarget(Resident resident, out int x, out int y)
    {
        var building = FindLocalWorkBuilding(new StateReference<Resident>(resident), 8, true);
        x = building?.Value.X ?? resident.X;
        y = building?.Value.Y ?? resident.Y;
        return building is not null;
    }

    private StateReference<Building>? FindLocalWorkTarget(StateReference<Resident> resident)
    {
        return FindLocalWorkBuilding(resident, 8, true);
    }

    private int WorkPriority(Building building, StateReference<Resident> resident, bool preferSpecialty)
    {
        if (preferSpecialty && PreferredExpansionJob(building.Kind) == resident.Value.Profession)
            return 0;
        if (preferSpecialty && (!building.IsCompleted || building.IsUpgrading))
            return 3;
        if (!building.IsCompleted || building.IsUpgrading || (building.Kind == BuildingKind.TownCenter &&
                                                              RequireTown(building.SettlementId).Value.IsExpanding))
            return 0;
        if (PreferredExpansionJob(building.Kind) == resident.Value.Profession)
            return 1;
        if (building.Kind == BuildingKind.Reservoir && RequireTown(building.SettlementId).Value.Resources.Water <
            RequireTown(building.SettlementId).Value.Population)
            return 1;
        if (resident.Value.Profession is Profession.Physician or Profession.Firefighter or Profession.Archivist
                or Profession.Surveyor or Profession.Gardener
            && preferSpecialty)
            return 6;
        if (building.Kind == BuildingKind.Armory &&
            resident.Value.Profession is Profession.Soldier or Profession.Ranger)
            return 1;
        if (ProductionRules.For(building.Kind) is { } production)
        {
            if (building.ProductionBatches == 0)
                return 1;
            if (resident.Value.Profession == Profession.Scholar && Society.Research.Any(r =>
                    r.SettlementId == building.SettlementId && r.ActiveProject is not null))
                return 3;
            var stock = RequireTown(building.SettlementId).Value.Resources.Get(production.Output);
            return stock < (production.Output == ResourceKind.Food ? 100 : 80) ? 1 : 4;
        }

        if (building.Kind is BuildingKind.Waystation or BuildingKind.SignalTower)
            return building.LastWorkedTick < SimulationTick - 6 ? 1 : 5;
        if ((resident.Value.Profession is Profession.Mage or Profession.Battlemage ||
             resident.Value.Agent.Goal.Kind == AgentGoalKind.TrainMagic) &&
            building.Kind == BuildingKind.ArcaneSanctum)
            return resident.Value.MagicTraining < 8 ? 1 : 2;
        if ((resident.Value.Profession == Profession.Scholar || resident.Value.Agent.Goal.Kind == AgentGoalKind.Study) &&
            building.Kind == BuildingKind.Academy)
            return 1;
        if (building.Kind == BuildingKind.Academy && resident.Value.Agent.Personality.Ambition > 0.55)
            return 2;
        if (building.Kind == BuildingKind.ArcaneSanctum && resident.Value.MagicTalent >= 45)
            return 2;
        if (resident.Value.Profession == Profession.Farmer && building.Kind == BuildingKind.Farm)
            return 2;
        if (resident.Value.Profession is Profession.Lumberjack or Profession.Miner &&
            building.Kind == BuildingKind.Workshop)
            return 2;
        return 3;
    }

    private bool BuildingHasWork(Building building, StateReference<Resident> resident)
    {
        if (!BuildingGroundOwned(building) || !building.Enabled || !ResidentNeedsRules.CanWork(resident.Value) || resident.Value.ArmyId != 0 ||
            resident.Value.Health <= 0)
            return false;
        if (Tiles[Index(building.X, building.Y)].Value.FireTicks > 0
            || !BuildingTerrainValid(building.Kind, Tiles[Index(building.X, building.Y)].Value))
            return false;
        var sameWorkPeriod = building.IsCompleted && !building.IsUpgrading &&
                             ProductionRules.For(building.Kind) is not null
            ? SimulationTime.DayIndex(building.LastWorkedTick) == SimulationTime.DayIndex(SimulationTick)
            : building.LastWorkedTick == SimulationTick;
        if (sameWorkPeriod && building.Workers.Count >= building.WorkSlots &&
            !building.Workers.Contains(resident.Value.Id))
            return false;
        if (building.Health < 50)
        {
            return building.Health > 0 && resident.Value.Profession is Profession.Builder or Profession.Engineer
                                           or Profession.Firefighter
                                       && (resident.Value.Inventory.Stone >= .5 ||
                                           RequireTown(building.SettlementId).Value.Resources.Stone >= .5);
        }

        if (!building.IsCompleted || building.IsUpgrading)
            return true;
        if (BuildingRace(building.Kind) is { } race && resident.Value.Race != race)
            return false;
        if (ProductionRules.For(building.Kind) is { } production)
            return CanProduce(building, resident, production);
        if (IsHusbandry(building.Kind))
            return HusbandryHasWork(building, resident);
        if (building.Kind >= BuildingKind.Reservoir)
            return ExpansionFacilityHasWork(building, resident);
        if (BuildingRace(building.Kind) is not null)
            return RacialBuildingHasWork(building, resident);
        return building.Kind switch
        {
            BuildingKind.Farm => resident.Value.Agent.Goal.PlayerDirected ||
                                 RequireTown(building.SettlementId).Value.Resources.Food <
                                 ProductionStockTarget(RequireTown(building.SettlementId), ResourceKind.Food) ||
                                 resident.Value.Inventory.Food < TravelReserve(resident),
            BuildingKind.Workshop => (resident.Value.Agent.Goal.PlayerDirected ||
                                      LocalMaterialsNeeded(resident, RequireTown(building.SettlementId))) &&
                                     FindWorkshopResource(building, resident.Value.Profession) >= 0,
            BuildingKind.Academy => HasActiveResearchProject(Society.Research, building.SettlementId),
            BuildingKind.ArcaneSanctum => Society.MagicEnabled && resident.Value.MagicTalent >= 25 &&
                                          resident.Value.MagicTraining < 100,
            BuildingKind.Infirmary => FindLocalWorkPatient(building, true) is not null,
            BuildingKind.MountainPass or BuildingKind.Bridge or BuildingKind.Granary or BuildingKind.Housing
                or BuildingKind.Watchtower => false,
            BuildingKind.TownCenter => RequireTown(building.SettlementId).Value.IsExpanding,
            BuildingKind.LumberCamp => resident.Value.Profession == Profession.Lumberjack &&
                                       (resident.Value.Agent.Goal.PlayerDirected ||
                                        LocalMaterialsNeeded(resident, RequireTown(building.SettlementId))) &&
                                       FindWorkshopResource(building, Profession.Lumberjack) >= 0,
            BuildingKind.Quarry => resident.Value.Profession == Profession.Miner &&
                                   (resident.Value.Agent.Goal.PlayerDirected ||
                                    LocalMaterialsNeeded(resident, RequireTown(building.SettlementId))) &&
                                   FindWorkshopResource(building, Profession.Miner) >= 0,
            BuildingKind.Well => resident.Value.Inventory.Water < WaterReserve(resident) + 3
                                 && WellWaterYield(Tiles[Index(building.X, building.Y)].Value) > 0 &&
                                 AvailableWater(building.X, building.Y) > 0,
            _ => true,
        };
    }

    /// <summary>尝试让到场居民在目标设施施工、升级或劳动，返回是否执行了工作。</summary>
    /// <param name="resident">劳动的居民。</param>
    public bool TryWorkAtBuilding(Resident resident)

    {
        return TryWorkAtBuilding(RequireResident(resident.Id));
    }

    private bool TryWorkAtBuilding(StateReference<Resident> resident)
    {
        if (!ResidentNeedsRules.CanWork(resident.Value))
            return false;
        var building = FindLocalWorkBuilding(resident, 1, false, true);
        var mental = building is not null && IsMentalWork(building.Value);
        if (ResidentNeedsRules.WorkEfficiency(resident.Value, mental) <= 0)
            return false;
        var repairs = building is not null && building.Value.Health < 50;
        var worked = PerformBuildingWork(resident);
        if (worked && IsWorkDay(resident) && !repairs)
            resident.Replace(resident.Value.WithAgent(resident.Value.Agent with
            {
                Fatigue = ResidentNeedsRules.ExertionFatigue(resident.Value, ResidentNeedsRules.PhysicalWorkCostPerTick
                    * (mental ? ResidentNeedsRules.MentalWorkCostRatio : 1) * WorkInterval(resident)),
            }));
        return worked;
    }

    private bool PerformBuildingWork(StateReference<Resident> resident)
    {
        var building = FindLocalWorkBuilding(resident, 1, false, true);
        if (building is null || !_settlements.TryGetValue(building.Value.SettlementId, out var town))
            return false;
        if (!IsWorkDay(resident))
            return true;
        if (building.Value.IsCompleted && !building.Value.IsUpgrading &&
            building.Value.Kind is BuildingKind.Waystation or BuildingKind.SignalTower or BuildingKind.Dock
                or BuildingKind.Market && town.Value.Resources.Food < 0.01)
            return false;
        if (building.Value.IsCompleted && !building.Value.IsUpgrading && building.Value.Kind == BuildingKind.ArcaneSanctum &&
            (!Society.MagicEnabled || town.Value.Resources.Food < 0.03))
            return false;
        var production = ProductionRules.For(building.Value.Kind);
        if (building.Value.IsCompleted && !building.Value.IsUpgrading && production is not null &&
            !HasProductionInputs(resident.Value.Inventory, production))
            return false;
        if (production is null || !building.Value.IsCompleted || building.Value.IsUpgrading
                ? building.Value.LastWorkedTick != SimulationTick
                : SimulationTime.DayIndex(building.Value.LastWorkedTick) != SimulationTime.DayIndex(SimulationTick))
        {
            building.Replace(building.Value with { Workers = building.Value.Workers.Clear(), LastWorkedTick = SimulationTick });
        }

        if (building.Value.Workers.Contains(resident.Value.Id))
            return false;
        building.Replace(building.Value with { Workers = building.Value.Workers.Add(resident.Value.Id) });
        if (building.Value.Health < 50)
        {
            RepairBuilding(resident.Value.Id, building.Value.Id, WorkInterval(resident));
            return true;
        }

        var effort = WorkDays(resident, IsMentalWork(building.Value)) * Math.Clamp(
            (0.6 + resident.Value.Agent.Personality.Diligence * 0.6) * LaborCondition(resident.Value.SicknessTicks, resident.Value.Thirst), 0.1,
            1.2);
        if ((!building.Value.IsCompleted || building.Value.IsUpgrading) && resident.Value.Profession == Profession.Engineer
                                                            && HasResearch(town.Value.Id,
                                                                Advancement.MechanicalEngineering) &&
                                                            resident.Value.Inventory.Tools >= .05)
        {
            resident.Replace(resident.Value.WithInventory(resident.Value.Inventory with { Tools = resident.Value.Inventory.Tools - .05 }));
            effort *= 1.75;
        }

        if (building.Value.IsUpgrading)
        {
            building.Replace(building.Value with
            {
                UpgradeProgress = Math.Min(building.Value.UpgradeRequired,
                building.Value.UpgradeProgress + effort * Rules.DevelopmentRate)
            });
            if (building.Value.UpgradeProgress >= building.Value.UpgradeRequired)
                FinishBuildingUpgrade(building);
            return true;
        }

        if (!building.Value.IsCompleted)
        {
            building.Replace(building.Value with { Observation = building.Value.Observation.AddContributor(resident.Value.Id) });
            building.Replace(building.Value with
            {
                ConstructionProgress = Math.Min(building.Value.ConstructionRequired,
                building.Value.ConstructionProgress + effort * Rules.DevelopmentRate)
            });
            if (building.Value.IsCompleted)
            {
                var ground = Tiles[Index(building.Value.X, building.Value.Y)];
                if (IsForestTerrain(ground.Value.Terrain) && !PreserveBuildingForest(building.Value.Kind))
                {
                    ground.Replace(ground.Value.WithTerrain(TerrainType.Grass));
                    ground.Replace(ground.Value.WithResourceAmount(0));
                }

                CompleteLandImprovement(building.Value);
                EmitVisual(WorldVisualKind.Construction, building.Value.X, building.Value.Y);
                var complete = AddEvent(WorldEventKind.Construction, $"{town.Value.Name}的{BuildingName(building.Value.Kind)}竣工。",
                    building.Value.X, building.Value.Y,
                    EventAction.Completed, town.Value.Id, causeEventId: building.Value.Observation.StartEventId);
                foreach (var person in Residents.Where(r => building.Value.Observation.Contributors.Contains(r.Value.Id)))
                    RecordLife(person, $"参与施工的{BuildingName(building.Value.Kind)}竣工。", complete,
                        PersonalExperienceKind.Achievement);
            }

            return true;
        }

        if (production is not null)
            return Produce(building, resident, production);
        if (IsHusbandry(building.Value.Kind))
            return WorkHusbandry(building, resident, effort * building.Value.Efficiency);
        if (building.Value.Kind >= BuildingKind.Reservoir)
            return WorkExpansionFacility(building, resident, town, effort * building.Value.Efficiency);
        effort *= building.Value.Efficiency * RaceTerrainRules
            .For(resident.Value.Race, Tiles[Index(building.Value.X, building.Value.Y)].Value.Terrain).Productivity;
        if (BuildingRace(building.Value.Kind) is not null)
            return WorkRacialBuilding(building, resident, effort);
        var culture = GetCulture(resident.Value.CultureId);
        switch (building.Value.Kind)
        {
            case BuildingKind.Farm:
                var tile = Tiles[Index(building.Value.X, building.Value.Y)];
                var fertility = tile.Value.Fertility / 100d * (tile.Value.DroughtTicks > 0 ? 0.18 : 1) *
                                (tile.Value.FireTicks > 0 ? 0 : 1);
                var harvest = Math.Min(1.25 * effort * fertility, 0.5 * effort * fertility *
                                                                  (1 + culture.NatureAffinity * 0.2) *
                                                                  (HasResearch(town.Value.Id, Advancement.Agriculture)
                                                                      ? 1.35
                                                                      : 1)
                                                                  * (town.Value.FertilityBoostTicks > 0 ? 1.35 : 1) *
                                                                  GetPolicyProductionMultiplier(town.Value.Id) *
                                                                  (HasResearch(town.Value.Id, Advancement.Irrigation)
                                                                      ? 1.25
                                                                      : 1));
                RecordHarvest(tile, harvest);
                resident.Replace(resident.Value.WithInventory(resident.Value.Inventory with
                {
                    Food = Math.Min(1_000_000, resident.Value.Inventory.Food + harvest),
                }));
                return harvest > 0;
            case BuildingKind.Workshop:
            case BuildingKind.LumberCamp:
            case BuildingKind.Quarry:
                var source = FindWorkshopResource(building.Value, resident.Value.Profession);
                if (source < 0)
                    return false;
                var sourceTile = Tiles[source];
                var yields = TerrainRules.For(sourceTile.Value.Terrain);
                var desired = effort * 0.2 * Rules.GatheringRate *
                              (HasResearch(town.Value.Id, Advancement.Forestry) ? 1.25 : 1) *
                              GatheringTerritoryMultiplier(resident.Value.SettlementId, resident.Value.NationId, sourceTile.Value);
                var amount = resident.Value.Profession == Profession.Miner
                    ? Math.Min(sourceTile.Value.ResourceAmount, desired)
                    : HarvestPlants(sourceTile, desired * NaturalPlantHarvestEfficiency(sourceTile, true), true);
                if (resident.Value.Profession == Profession.Miner)
                {
                    sourceTile.Replace(sourceTile.Value.WithResourceAmount(sourceTile.Value.ResourceAmount - (amount)));
                    resident.Replace(resident.Value.WithInventory(resident.Value.Inventory with
                    {
                        Stone = resident.Value.Inventory.Stone + amount * yields.StoneYield,
                        Ore = resident.Value.Inventory.Ore + amount * yields.OreYield,
                    }));
                }
                else
                {
                    resident.Replace(resident.Value.WithInventory(resident.Value.Inventory with
                    {
                        Wood = resident.Value.Inventory.Wood + amount * yields.WoodYield,
                    }));
                    FinishLogging(sourceTile, source % Width, source / Width);
                }

                RecordHarvest(sourceTile,
                    amount * (resident.Value.Profession == Profession.Miner
                        ? yields.StoneYield + yields.OreYield
                        : yields.WoodYield));
                resident.Replace(resident.Value.WithInventory(resident.Value.Inventory.Clamp(1_000_000)));
                return true;
            case BuildingKind.Academy:
                var research = Society.Research.First(r => r.SettlementId == town.Value.Id);
                if (research.ActiveProject is null)
                    return false;
                var progress = effort * Rules.DevelopmentRate * (1 + EffectiveSettlementRank(town) * .1) *
                               (0.75 + culture.Innovation * 0.5) *
                               (GetLocalPolicy(town.Value.Id) == PolicyKind.Scholarship ? 1.35 : 1) *
                               (HasResearch(town.Value.Id, Advancement.ScientificMethod) ? 1.25 : 1) *
                               (HasResearch(town.Value.Id, Advancement.ArcaneScholarship) ? 1.25 : 1);
                research = PublishResearch(research.AddWork(resident.Value.Id, progress));
                if (research.Progress >= 8 && resident.Value.Profession == Profession.Builder &&
                    !HasTwoLocalWorkers(town.Value.Id, Profession.Scholar))
                    resident.Replace(resident.Value with { Profession = Profession.Scholar,
                        Agent = resident.Value.Agent with { DailyPlan = null, DaytimeGoal = null } });
                if (research.Progress >= research.RequiredProgress)
                {
                    var completed = research.ActiveProject!;
                    GrantReceivedResearch(town.Value.Id, completed, research.Observation.StartEventId);
                    var fact = new AgentFact
                    {
                        Id = NewId(),
                        EventId = research.LastCompletionEventId,
                        Kind = AgentFactKind.Research,
                        SubjectId = town.Value.Id,
                        X = town.Value.X,
                        Y = town.Value.Y,
                        Value = completed.Id,
                        ObservedTick = SimulationTick,
                        LearnedTick = SimulationTick,
                        OriginResidentId = resident.Value.Id,
                        OriginProfession = resident.Value.Profession,
                        SourceResidentId = resident.Value.Id,
                        Text = $"{town.Value.Name}已完成{completed.Name}研究",
                    };
                    AddPublicFact(town, fact);
                    AddResidentFact(resident, fact);
                }

                return true;
            case BuildingKind.Waystation:
            case BuildingKind.SignalTower:
            case BuildingKind.Dock:
            case BuildingKind.Market:
                if (town.Value.Resources.Food < 0.01)
                    return false;
                town.Replace(town.Value.WithResources(town.Value.Resources with { Food = town.Value.Resources.Food - 0.01 }));
                return true;
            case BuildingKind.ArcaneSanctum:
                if (!Society.MagicEnabled || town.Value.Resources.Food < 0.03)
                    return false;
                town.Replace(town.Value.WithResources(town.Value.Resources with { Food = town.Value.Resources.Food - 0.03 }));
                resident.Replace(resident.Value with { MagicTraining = Math.Min(100,
                    resident.Value.MagicTraining + effort * Rules.MagicRate * (0.05 + resident.Value.MagicTalent / 500) *
                    TerrainRules.For(Tiles[Index(resident.Value.X, resident.Value.Y)].Value.Terrain).ManaRate *
                    (HasResearch(town.Value.Id, Advancement.ArcaneScholarship) ? 1.5 : 1)) });
                if (resident.Value.MagicTraining >= 8 && resident.Value.Profession is Profession.Builder or Profession.Scholar &&
                    !HasTwoLocalWorkers(town.Value.Id, Profession.Mage))
                    resident.Replace(resident.Value with { Profession = Profession.Mage,
                        Agent = resident.Value.Agent with { DailyPlan = null, DaytimeGoal = null } });
                resident.Replace(resident.Value.WithMana(Math.Min(100,
                    resident.Value.Mana + 0.15 * effort * (HasResearch(town.Value.Id, Advancement.ManaAttunement) ? 1.5 : 1))));
                return true;
            case BuildingKind.Infirmary:
                if (town.Value.Resources.Food < 0.05)
                    return false;
                var patient = FindLocalWorkPatient(building.Value);
                if (patient is null)
                    return false;
                town.Replace(town.Value.WithResources(town.Value.Resources with { Food = town.Value.Resources.Food - 0.05 }));
                patient.Replace(patient.Value.WithHealth(Math.Min(100,
                    patient.Value.Health + 0.45 * effort * (HasResearch(town.Value.Id, Advancement.Medicine) ? 1.5 : 1))));
                patient.Replace(patient.Value.WithSicknessTicks(Math.Max(0, patient.Value.SicknessTicks
                    - ServiceDurationTicks(resident.Value, building.Value, SimulationTime.TicksPerDay))));
                return true;
            case BuildingKind.TownCenter:
                return WorkOnTownExpansion(town, effort / building.Value.Efficiency);
            case BuildingKind.Well:
                return resident.Value.X == building.Value.X && resident.Value.Y == building.Value.Y && DrawWater(resident,
                    Index(building.Value.X, building.Value.Y),
                    Math.Min(1, effort) *
                    GatheringTerritoryMultiplier(resident.Value.SettlementId, resident.Value.NationId, Tiles[Index(building.Value.X, building.Value.Y)].Value)) > 0;
            default:
                return false;
        }
    }

    private int FindWorkshopResource(Building building, Profession profession)
    {
        var best = -1;
        var bestYield = 0d;
        foreach (var index in Circle(building.X, building.Y, 1))
        {
            var tile = Tiles[index];
            if (tile.Value.ResourceAmount < .5 || tile.Value.FireTicks > 0)
                continue;
            if (profession != Profession.Miner && NaturalPlantHarvestEfficiency(tile, true) < .25)
                continue;
            var yield = TerrainRules.For(tile.Value.Terrain);
            var value = profession == Profession.Miner
                ? yield.StoneYield + yield.OreYield
                : yield.WoodYield * NaturalPlantHarvestEfficiency(tile, true);
            if (value > bestYield || (value == bestYield && value > 0 && index < best))
            {
                best = index;
                bestYield = value;
            }
        }

        return best;
    }

    /// <summary>计算包含种族、道路和地块改良修正的步行成本，越界或不可通行时为正无穷。</summary>
    /// <param name="x">横向地格坐标。</param>
    /// <param name="y">纵向地格坐标。</param>
    /// <param name="race">居民种族。</param>
    public double GetTerrainMoveCost(int x, int y, RaceKind race = RaceKind.Human)
    {
        if (!InBounds(x, y))
            return double.PositiveInfinity;
        return TerrainMoveCost(Tiles[Index(x, y)].Value, race);
    }

    // 已取得不可变地格的局部搜索直接计算成本，不重复查询坐标和定位引用。
    private static double TerrainMoveCost(Tile tile, RaceKind race)
    {
        if (tile.Improvement == LandImprovement.MountainPass && tile.Terrain == TerrainType.Mountain)
        {
            return race == RaceKind.Dwarf
                ? Math.Min(2, 3.5 / (1 + Math.Max(0, tile.RoadLevel - 1) * .25))
                : 3.5 / (1 + Math.Max(0, tile.RoadLevel - 1) * .25);
        }

        if (tile.Improvement == LandImprovement.Bridge && tile.Terrain is TerrainType.Water or TerrainType.River
                or TerrainType.Lake or TerrainType.Stream
                or TerrainType.LargeRiver)
            return 1.2 / (1 + Math.Max(0, tile.BridgeLevel - 1) * .25);
        var cost = race == RaceKind.Dwarf && tile.Terrain == TerrainType.Mountain
            ? 2.5
            : TerrainRules.MovementCost(tile.Terrain);
        cost *= RaceTerrainRules.For(race, tile.Terrain).Movement;
        if (!double.IsFinite(cost))
            return cost;
        return tile.RoadLevel >= 2 ? Math.Max(.35, cost * .3) : tile.RoadLevel > 0 ? Math.Max(0.65, cost * 0.55) : cost;
    }

    /// <summary>计算实际信使在此地的移动速率倍率，包含地形及附近本国驿站加成。</summary>
    /// <param name="x">横向地格坐标。</param>
    /// <param name="y">纵向地格坐标。</param>
    /// <param name="nationId">国家 ID。</param>
    /// <param name="race">居民种族。</param>
    public double MessageTravelMultiplier(int x, int y, int nationId, RaceKind race = RaceKind.Human)
    {
        if (!InBounds(x, y))
            return 0;
        var speed = 1 / GetTerrainMoveCost(x, y, race);
        var bonus = 1d;
        foreach (var town in Settlements)
            if (town.Value.NationId == nationId && Distance(x, y, town.Value.X, town.Value.Y) <= 3)
                bonus = Math.Max(bonus, 1 + EffectiveSettlementRank(town) * .15);
        foreach (var building in Buildings)
            if (building.Value.Kind == BuildingKind.Waystation && IsFacilityOperating(building.Value)
                                                         && _settlements.TryGetValue(building.Value.SettlementId,
                                                             out var town) && town.Value.NationId == nationId &&
                                                         Distance(x, y, building.Value.X, building.Value.Y) <= 3)
                bonus = Math.Max(bonus, 1.25 + (building.Value.Level - 1) * .15);
        speed *= bonus;
        return speed;
    }

    /// <summary>检查两处同国聚落之间是否有可用信号塔路径，并给出递送 tick 数。</summary>
    /// <param name="fromSettlementId">信息递送出发聚落的 ID。</param>
    /// <param name="toSettlementId">信息递送目标聚落的 ID。</param>
    /// <param name="travelTicks">可用路径所需的模拟 tick 数；没有路径时为零。</param>
    public bool CanRelayInformation(int fromSettlementId, int toSettlementId, out int travelTicks)
    {
        travelTicks = 0;
        if (!_settlements.TryGetValue(fromSettlementId, out var from) ||
            !_settlements.TryGetValue(toSettlementId, out var to) || from.Value.NationId != to.Value.NationId)
            return false;
        if (!Buildings.Any(b => b.Value.Kind == BuildingKind.SignalTower && b.Value.IsCompleted))
            return false;
        var towers = Buildings.Where(b => b.Value.Kind == BuildingKind.SignalTower && IsFacilityOperating(b.Value)
                && HasResearch(b.Value.SettlementId, Advancement.SignalNetwork) &&
                HasResearch(b.Value.SettlementId, Advancement.Electrification) &&
                _settlements.TryGetValue(b.Value.SettlementId, out var town) && town.Value.NationId == from.Value.NationId)
            .OrderBy(b => b.Value.Id)
            .ToArray();
        var queue = new Queue<(StateReference<Building> Tower, int Hops)>();
        var visited = new HashSet<int>();
        foreach (var tower in towers.Where(t =>
                     Distance(t.Value.X, t.Value.Y, from.Value.X, from.Value.Y) <= 12 + (t.Value.Level - 1) * 4 &&
                     ClearSignalLine(t.Value.X, t.Value.Y, from.Value.X, from.Value.Y)))
        {
            queue.Enqueue((tower, 1));
            visited.Add(tower.Value.Id);
        }

        while (queue.TryDequeue(out var node))
        {
            if (Distance(node.Tower.Value.X, node.Tower.Value.Y, to.Value.X, to.Value.Y) <= 12 + (node.Tower.Value.Level - 1) * 4 &&
                ClearSignalLine(node.Tower.Value.X, node.Tower.Value.Y, to.Value.X, to.Value.Y))
            {
                travelTicks = node.Hops * 2;
                return true;
            }

            foreach (var tower in towers)
                if (!visited.Contains(tower.Value.Id) &&
                    Distance(tower.Value.X, tower.Value.Y, node.Tower.Value.X, node.Tower.Value.Y) <=
                    24 + (Math.Min(tower.Value.Level, node.Tower.Value.Level) - 1) * 8 &&
                    ClearSignalLine(tower.Value.X, tower.Value.Y, node.Tower.Value.X, node.Tower.Value.Y))
                {
                    visited.Add(tower.Value.Id);
                    queue.Enqueue((tower, node.Hops + 1));
                }
        }

        return false;
    }

    private bool IsFacilityOperating(Building building)
    {
        if (!BuildingGroundOwned(building) || !building.Enabled || !building.IsCompleted || building.IsUpgrading
            || building.Health < 50 ||
            !BuildingTerrainValid(building.Kind, Tiles[Index(building.X, building.Y)].Value)
            || Tiles[Index(building.X, building.Y)].Value.FireTicks > 0)
            return false;
        if (PassiveFacility(building) || building.Kind is BuildingKind.MountainPass or BuildingKind.Bridge
                or BuildingKind.TownCenter or BuildingKind.Well or BuildingKind.Waygate)
            return true;
        if (building.LastWorkedTick < SimulationTick - 12)
            return false;
        var race = BuildingRace(building.Kind);
        foreach (var id in building.Workers)
            if (FindLiveResident(id) is { } worker && worker.Value.SettlementId == building.SettlementId
                                                   && worker.Value.Health > 0 && (race is null ||
                                                                            (worker.Value.Race == race && worker.Value.Age >= ResidentNeedsRules.MinimumWorkAge))
                                                   && Distance(worker.Value.X, worker.Value.Y, building.X, building.Y) <= 1)
                return true;
        return false;
    }

    private bool ClearSignalLine(int x0, int y0, int x1, int y1)
    {
        var steps = Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0));
        for (var i = 1; i < steps; i++)
        {
            var x = x0 + (int)Math.Round((x1 - x0) * i / (double)steps);
            var y = y0 + (int)Math.Round((y1 - y0) * i / (double)steps);
            if (Tiles[Index(x, y)].Value.Terrain == TerrainType.Mountain)
                return false;
        }

        return true;
    }

    /// <summary>设置国家制度形式，并记录玩家干预。</summary>
    /// <param name="nationId">国家 ID。</param>
    /// <param name="kind">国家制度形式。</param>
    public void SetInstitution(int nationId, InstitutionKind kind)
    {
        if (!Enum.IsDefined(kind))
            throw new ArgumentOutOfRangeException(nameof(kind));
        _ = RequireNation(nationId);
        var institution = Society.Institutions.First(i => i.NationId == nationId);
        PublishInstitution(institution with { Kind = kind });
    }

    /// <summary>为国家指定政策，并安排其在相关聚落生效。</summary>
    /// <param name="nationId">国家 ID。</param>
    /// <param name="policy">待应用的政策。</param>
    public void SetPolicy(int nationId, PolicyKind policy)
    {
        if (!Enum.IsDefined(policy))
            throw new ArgumentOutOfRangeException(nameof(policy));
        var nation = RequireNation(nationId);
        var institution = Society.Institutions.First(i => i.NationId == nationId);
        PublishInstitution(institution with { PlayerPolicy = policy });
        foreach (var town in Settlements.Where(s => s.Value.NationId == nationId))
        {
            var local = Society.Policies.First(p => p.SettlementId == town.Value.Id);
            local = PublishPolicy(local with
            {
                Kind = policy, PlayerOverride = true, DecidedTick = SimulationTick, Reason = "玩家直接设定；恢复自治前保持此政策",
            });
        }

        AddEvent(WorldEventKind.Editor, $"{nation.Value.Name}的政策由玩家调整为{PolicyName(policy)}。");
    }

    /// <summary>清除国家和本地的玩家政策覆盖，恢复机构自主选择。</summary>
    /// <param name="nationId">国家 ID。</param>
    public void SetPolicyAutonomy(int nationId)
    {
        _ = RequireNation(nationId);
        var institution = Society.Institutions.First(i => i.NationId == nationId);
        PublishInstitution(institution with { PlayerPolicy = null });
        var towns = Settlements.Where(s => s.Value.NationId == nationId).Select(s => s.Value.Id).ToHashSet();
        Society = Society with
        {
            Policies = Society.Policies.Map(policy => towns.Contains(policy.SettlementId)
                ? policy with { PlayerOverride = false }
                : policy),
        };
    }

    /// <summary>查询聚落当前采用的政策，没有记录时返回均衡政策。</summary>
    /// <param name="settlementId">聚落 ID。</param>
    public PolicyKind GetLocalPolicy(int settlementId)
    {
        return Society.Policies.FirstOrDefault(p => p.SettlementId == settlementId)?.Kind ??
               PolicyKind.Balanced;
    }

    /// <summary>返回本地政策对生产的倍率。</summary>
    /// <param name="settlementId">聚落 ID。</param>
    public double GetPolicyProductionMultiplier(int settlementId)
    {
        return GetLocalPolicy(settlementId) == PolicyKind.FoodSecurity ? 1.25 : 1;
    }

    /// <summary>将已经递送到本地的政策应用到聚落。</summary>
    /// <param name="settlementId">聚落 ID。</param>
    /// <param name="policy">待应用的政策。</param>
    public void ApplyReceivedPolicy(int settlementId, PolicyKind policy)
    {
        if (!Enum.IsDefined(policy))
            return;
        _ = RequireTown(settlementId);
        var local = Society.Policies.First(p => p.SettlementId == settlementId);
        if (local.PlayerOverride)
            return;
        local = PublishPolicy(local with { Kind = policy, DecidedTick = SimulationTick, Reason = "代表已收到递送的政策指令" });
    }

    private void ReceiveSocietyReport(StateReference<Settlement> target, StateReference<Resident> carrier, AgentFact fact)
    {
        if (Distance(carrier.Value.X, carrier.Value.Y, target.Value.X, target.Value.Y) > 2 || fact.ObservedTick > SimulationTick ||
            fact.Confidence is < 0 or > 1 || !double.IsFinite(fact.Value))
            return;
        fact.Topic.Receive(this, target, carrier, fact);
        if (!fact.Topic.CreatesInstitutionReport)
            return;
        if (_knowledgeQueriesActive
                ? !_institutionReports.Add((target.Value.Id, fact.Id))
                : Society.Reports.Any(r => r.RecipientSettlementId == target.Value.Id && r.FactId == fact.Id))
            return;
        Society = Society with
        {
            Reports = Society.Reports.Add(new InstitutionReport
            {
                EventId = fact.EventId,
                RecipientSettlementId = target.Value.Id,
                FactId = fact.Id,
                OriginResidentId = fact.OriginResidentId,
                RepresentativeId = carrier.Value.Id,
                ReportedProfession = fact.OriginProfession,
                Topic = fact.Kind,
                SubjectId = fact.SubjectId,
                Value = fact.Value,
                Confidence = fact.Confidence,
                ObservedTick = fact.ObservedTick,
                ReceivedTick = SimulationTick,
            }),
        };
        if (Society.Reports.Count > 2_048)
        {
            var remove = Society.Reports.Count - 2_048;
            if (_knowledgeQueriesActive)
            {
                for (var index = 0; index < remove; index++)
                {
                    var old = Society.Reports[index];
                    _institutionReports.Remove((old.RecipientSettlementId, old.FactId));
                }
            }

            Society = Society with
            {
                Reports = Society.Reports.RemoveRange(0, Society.Reports.Count - 2_048),
            };
        }
    }

    internal void ReceiveResearchFact(StateReference<Settlement> target, AgentFact fact)
    {
        if (fact.Confidence >= 0.5 && fact.Value == Math.Truncate(fact.Value) &&
            Advancement.Find((int)fact.Value) is { } receivedResearch)
            GrantReceivedResearch(target.Value.Id, receivedResearch, fact.EventId, fact.Id);
    }

    internal void ReceivePolicyFact(StateReference<Settlement> target, AgentFact fact)
    {
        if (fact.Confidence < 0.5 || SimulationTick - fact.ObservedTick > 240 ||
            fact.Value is < 0 or > 4 || fact.Value != Math.Truncate(fact.Value))
            return;
        var policy = Society.Policies.First(p => p.SettlementId == target.Value.Id);
        if (fact.ObservedTick < policy.EvidenceObservedTick)
            return;
        ApplyReceivedPolicy(target.Value.Id, (PolicyKind)(int)fact.Value);
        policy = Society.Policies.First(p => p.SettlementId == target.Value.Id);
        if (!policy.PlayerOverride)
            policy = PublishPolicy(policy with { EvidenceFactId = fact.Id, EvidenceObservedTick = fact.ObservedTick });
    }

    internal void ReceiveCultureFact(StateReference<Resident> carrier, AgentFact fact)
    {
        if (fact.Confidence >= 0.5 && fact.Value is > 0 and <= 100_000 &&
            fact.Value == Math.Truncate(fact.Value))
            ObserveCulture(carrier, (int)fact.Value);
    }

    private void DecideLocalPolicy(StateReference<Settlement> town)
    {
        var institution = Society.Institutions.First(i => i.NationId == town.Value.NationId);
        var local = Society.Policies.First(p => p.SettlementId == town.Value.Id);
        if (institution.PlayerPolicy.HasValue)
        {
            local = PublishPolicy(local with { Kind = institution.PlayerPolicy.Value, PlayerOverride = true });
            return;
        }

        var (candidate, strongest) = SelectLocalPolicy(local, institution, Society.Reports,
            Nations.First(n => n.Value.Id == town.Value.NationId).Value.RepresentativeId,
            GetCulture(town.Value.CultureId).Cooperation, SimulationTick);
        if (strongest is null)
            return;
        var changed = local.Kind != candidate.Kind;
        local = PublishPolicy(candidate);
        institution =
            PublishInstitution(institution with { LastDecision = local.Reason, LastDecisionTick = SimulationTick });
        if (town.Value.Id == Nations.First(n => n.Value.Id == town.Value.NationId).Value.CapitalId)
            Nations.First(n => n.Value.Id == town.Value.NationId).Replace(Nations.First(n => n.Value.Id == town.Value.NationId).Value with { Decision = local.Reason });
        if (changed)
        {
            var policyEvent = AddEvent(WorldEventKind.Policy, $"{town.Value.Name}议事决定采用{PolicyName(local.Kind)}。", town.Value.X,
                town.Value.Y, EventAction.Policy, town.Value.Id, causeEventId: strongest.EventId, evidenceFactId: strongest.FactId);
            AddPublicFact(town, new AgentFact
            {
                Id = NewId(),
                EventId = policyEvent.Id,
                Kind = AgentFactKind.Policy,
                SubjectId = town.Value.Id,
                X = town.Value.X,
                Y = town.Value.Y,
                Value = (int)local.Kind,
                ObservedTick = SimulationTick,
                LearnedTick = SimulationTick,
                OriginResidentId = town.Value.RepresentativeId,
                OriginProfession = Profession.Representative,
                SourceResidentId = town.Value.RepresentativeId,
                Text = local.Reason,
            });
        }
    }

    private static (LocalPolicy Policy, InstitutionReport? Evidence) SelectLocalPolicy(LocalPolicy local,
        NationInstitution institution, IReadOnlyList<InstitutionReport> reports, int representativeId,
        double cooperation, long tick)
    {
        if (local.PlayerOverride)
            return (local, null);
        var latest = reports
            .Where(r => r.RecipientSettlementId == local.SettlementId && r.ObservedTick <= tick)
            .GroupBy(r => (r.OriginResidentId, r.Topic)).Select(g =>
                g.OrderByDescending(r => r.ObservedTick).ThenByDescending(r => r.ReceivedTick).First()).ToArray();
        if (latest.Length == 0)
            return (local, null);
        var scores = new double[5];
        var evidence = new InstitutionReport?[5];
        var largest = new double[5];
        foreach (var report in latest)
        {
            var topic = AgentFactTopic.For(report.Topic);
            var policy = topic.SuggestedPolicy;
            var urgency = topic.Urgency(report.Value);
            var relevance = policy == PolicyKind.FoodSecurity && report.ReportedProfession == Profession.Farmer ? 2 :
                policy == PolicyKind.Defense && report.ReportedProfession == Profession.Soldier ? 2 :
                policy == PolicyKind.Scholarship &&
                report.ReportedProfession is Profession.Miner or Profession.Builder ? 1.8 : 1;
            var authority = institution.Kind switch
            {
                InstitutionKind.Monarchy => report.RepresentativeId ==
                                            representativeId ? 5 :
                    report.ReportedProfession == Profession.Soldier ? 2.5 : 0.6,
                InstitutionKind.GuildCouncil => report.ReportedProfession is Profession.Lumberjack or Profession.Miner
                    or Profession.Builder
                    ? 2.8
                    : 0.8,
                _ => 1 + cooperation * 0.35,
            };
            var score = urgency * report.Confidence * relevance * authority /
                        (1 + (tick - report.ObservedTick) / (2d * SimulationTime.TicksPerMonth));
            scores[(int)policy] += score;
            if (score > largest[(int)policy])
            {
                largest[(int)policy] = score;
                evidence[(int)policy] = report;
            }
        }

        var choice = Enumerable.Range(1, 4).OrderByDescending(i => scores[i]).ThenBy(i => i).First();
        var strongest = evidence[choice];
        if (scores[choice] < 5 || strongest is null)
            return (local, null);
        return (local with
        {
            Kind = (PolicyKind)choice,
            DecidedTick = tick,
            EvidenceFactId = strongest.FactId,
            EvidenceObservedTick = strongest.ObservedTick,
            Reason =
            $"依据代表 {strongest.RepresentativeId} 送达的议题（观察于 {strongest.ObservedTick}，接收于 {strongest.ReceivedTick}），制度与职业相关权重合计 {scores[choice]:0.0}，采用{PolicyName((PolicyKind)choice)}",
        }, strongest);
    }

    /// <summary>校验并修改文化名称。</summary>
    /// <param name="cultureId">文化定义的稳定 ID。</param>
    /// <param name="name">新的名称。</param>
    public void RenameCulture(int cultureId, string name)
    {
        var culture = RequireCulture(cultureId);
        name = (name ?? "").Trim();
        if (name.Length is < 1 or > 40 || name.Any(char.IsControl))
            throw new ArgumentException("文化名称须为 1–40 个可见字符。", nameof(name));
        Society = Society with
        {
            Cultures = Society.Cultures.SetItem(Society.Cultures.IndexOf(culture),
                culture with { Name = name }),
        };
    }

    /// <summary>设置文化的行为倾向权重。</summary>
    /// <param name="cultureId">文化定义的稳定 ID。</param>
    /// <param name="cooperation">合作倾向权重，范围为 0 至 1。</param>
    /// <param name="innovation">创新倾向权重，范围为 0 至 1。</param>
    /// <param name="natureAffinity">亲自然倾向权重，范围为 0 至 1。</param>
    public void SetCultureValues(int cultureId, double cooperation, double innovation, double natureAffinity)
    {
        var culture = RequireCulture(cultureId);
        if (new[] { cooperation, innovation, natureAffinity }.Any(v => !double.IsFinite(v) || v is < 0 or > 1))
            throw new ArgumentOutOfRangeException(nameof(cooperation), "文化参数须在 0 到 1 之间。");
        Society = Society with
        {
            Cultures = Society.Cultures.SetItem(Society.Cultures.IndexOf(culture),
                culture with
                {
                    Cooperation = cooperation, Innovation = innovation, NatureAffinity = natureAffinity,
                }),
        };
    }

    /// <summary>指定国家的文化归属。</summary>
    /// <param name="nationId">国家 ID。</param>
    /// <param name="cultureId">文化定义的稳定 ID。</param>
    public void SetNationCulture(int nationId, int cultureId)
    {
        _ = RequireCulture(cultureId);
        RequireNation(nationId).Replace(RequireNation(nationId).Value with { CultureId = cultureId });
    }

    /// <summary>指定居民的文化归属。</summary>
    /// <param name="residentId">居民 ID。</param>
    /// <param name="cultureId">文化定义的稳定 ID。</param>
    public void SetResidentCulture(int residentId, int cultureId)
    {
        _ = RequireCulture(cultureId);
        var resident = Residents.FirstOrDefault(r => r.Value.Id == residentId) ??
                       throw new ArgumentException("居民不存在。");
        resident.Replace(resident.Value with { CultureId = cultureId });
    }

    /// <summary>记录两位居民实际接触对彼此文化认知的影响。</summary>
    /// <param name="first">实际参与交流的第一位居民。</param>
    /// <param name="second">实际参与交流的第二位居民。</param>
    public void ExchangeCulture(Resident first, Resident second)

    {
        ExchangeCulture(RequireResident(first.Id), RequireResident(second.Id));
    }

    private void ExchangeCulture(StateReference<Resident> first, StateReference<Resident> second)
    {
        if (Distance(first.Value.X, first.Value.Y, second.Value.X, second.Value.Y) > 2 || first.Value.CultureId == second.Value.CultureId)
            return;
        var firstCulture = first.Value.CultureId;
        ObserveCulture(first, second.Value.CultureId);
        ObserveCulture(second, firstCulture);
    }

    private void ObserveCulture(StateReference<Resident> resident, int cultureId)
    {
        if (resident.Value.CultureId == cultureId || !Society.Cultures.Any(c => c.Id == cultureId))
            return;
        var index = FindCultureContactIndex(resident.Value.Id, cultureId);
        var contact = index < 0 ? null : Society.CulturalContacts[index];
        if (contact is null)
        {
            contact = new CulturalContact { ResidentId = resident.Value.Id, CultureId = cultureId, LastContactTick = -12 };
            index = Society.CulturalContacts.Count;
            Society = Society with { CulturalContacts = Society.CulturalContacts.Add(contact) };
            if (_knowledgeQueriesActive)
            {
                _cultureContactIndices.TryAdd((resident.Value.Id, cultureId), index);
                _indexedCultureContactCount = index + 1;
            }
        }

        if (SimulationTick - contact.LastContactTick < 12)
            return;
        contact = PublishContact(contact.Observe(SimulationTick, resident.Value.Agent.Personality.Sociability));
        if (contact.Exposure < 10)
            return;
        var previous = GetCulture(resident.Value.CultureId).Name;
        resident.Replace(resident.Value with { CultureId = cultureId });
        // 文化归属变化后须重新积累持续接触，避免连续快速转化。
        var tick = SimulationTick;
        var residentId = resident.Value.Id;
        Society = Society with
        {
            CulturalContacts = Society.CulturalContacts.Map(exposure => exposure.ResidentId == residentId
                ? exposure with { Exposure = 0, LastContactTick = tick }
                : exposure),
        };

        RecordLife(resident, $"长期当面交流后，由{previous}转向{GetCulture(cultureId).Name}文化；种族与国籍未改变。");
        if (resident.Value.History.Count > 24)
            resident.Replace(resident.Value with { History = resident.Value.History.RemoveAt(0) });
        var cultureEvent = AddEvent(WorldEventKind.Culture, $"{resident.Value.Name}经长期交流转向{GetCulture(cultureId).Name}文化。",
            resident.Value.X, resident.Value.Y);
        cultureEvent = PublishEvent(cultureEvent with { ResidentId = resident.Value.Id, NationId = resident.Value.NationId });
    }

    /// <summary>尝试按正式施法命令执行法术；条件不满足时返回失败。</summary>
    /// <param name="casterId">施法居民的稳定 ID。</param>
    /// <param name="spell">法术类别。</param>
    /// <param name="x">横向地格坐标。</param>
    /// <param name="y">纵向地格坐标。</param>
    public bool TryCastSpell(int casterId, SpellKind spell, int x, int y)
    {
        // 自主施法频繁探测；缺魔力或尚未解锁属于普通不可用条件，不用异常完成判断。
        var caster = FindLiveResident(casterId);
        if (!Enum.IsDefined(spell) || caster is null || !InBounds(x, y)
            || Distance(caster.Value.X, caster.Value.Y, x, y) > 4 || caster.Value.Health <= 0 || !ResidentNeedsRules.CanWork(caster.Value)
            || caster.Value.MagicTalent < 25 || caster.Value.MagicTraining < 8 ||
            caster.Value.Mana < PersonalSpellCost(caster.Value.Race, spell))
            return false;
        if (ResearchRules.Unlocking(spell) is { } research
            && (!HasResearch(caster.Value.SettlementId, research)
                || !HasResearchPrerequisites(caster.Value.SettlementId, research.Prerequisites)))
            return false;
        try
        {
            CastSpell(casterId, spell, x, y);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>消耗居民魔力，在指定地点施放法术。</summary>
    /// <param name="casterId">施法居民的稳定 ID。</param>
    /// <param name="spell">法术类别。</param>
    /// <param name="x">横向地格坐标。</param>
    /// <param name="y">纵向地格坐标。</param>
    public void CastSpell(int casterId, SpellKind spell, int x, int y)
    {
        if (!Enum.IsDefined(spell))
            throw new ArgumentOutOfRangeException(nameof(spell));
        var caster = FindLiveResident(casterId) ?? throw new ArgumentException("施法居民不存在。");
        if (!InBounds(x, y) || Distance(caster.Value.X, caster.Value.Y, x, y) > 4)
            throw new InvalidOperationException("目标须位于施法者 4 格以内。");
        if (caster.Value.Health <= 0 || !ResidentNeedsRules.CanWork(caster.Value) || caster.Value.MagicTalent < 25 || caster.Value.MagicTraining < 8)
            throw new InvalidOperationException("需要达到劳动年龄且能够行动、魔法天赋至少 25 且完成至少 8 点奥术训练。");
        if (SpellUnlockError(casterId, spell) is { } unlockError)
            throw new InvalidOperationException(unlockError);
        var cost = PersonalSpellCost(caster.Value.Race, spell);
        if (caster.Value.Mana < cost)
            throw new InvalidOperationException($"法力不足：需要 {cost:0.#}，当前 {caster.Value.Mana:0.#}。");
        StateReference<Resident>? recipient = null;
        StateReference<Settlement>? town = null;
        switch (spell)
        {
            case SpellKind.Heal:
                recipient = NearbyResidents(x, y, 1)
                    .Where(r => r.Value.NationId == caster.Value.NationId && r.Value.Health > 0 &&
                                (r.Value.Health < 100 || r.Value.SicknessTicks > 0)).OrderBy(r => r.Value.Health).ThenBy(r => r.Value.Id)
                    .FirstOrDefault();
                if (recipient is null)
                    throw new InvalidOperationException("目标附近没有需要治疗的本国居民。");
                break;
            case SpellKind.HarvestBlessing:
            case SpellKind.Shield:
                town = Settlements.Where(s => s.Value.NationId == caster.Value.NationId && Distance(s.Value.X, s.Value.Y, x, y) <= 3)
                    .OrderBy(s => Distance(s.Value.X, s.Value.Y, x, y)).FirstOrDefault();
                if (town is null)
                    throw new InvalidOperationException("目标附近没有可施加结界或丰饶祝福的本国聚落。");
                break;
            case SpellKind.Ember:
            case SpellKind.FrostBolt:
            case SpellKind.ChainLightning:
                recipient = NearbyResidents(x, y, 1)
                    .Where(r => r.Value.NationId != caster.Value.NationId && r.Value.Health > 0 &&
                                IsKnownHostile(caster, r.Value.NationId)).OrderBy(r => r.Value.Id).FirstOrDefault();
                if (recipient is null)
                    throw new InvalidOperationException("目标附近没有正在交战的敌方居民。");
                if (!ClearSignalLine(caster.Value.X, caster.Value.Y, recipient.Value.X, recipient.Value.Y))
                    throw new InvalidOperationException("山体遮挡了施法视线");
                break;
            case SpellKind.RainCall:
                if (!Circle(x, y, 2).Any(i => Tiles[i].Value.DroughtTicks > 0 || Tiles[i].Value.FireTicks > 0))
                    throw new InvalidOperationException("目标附近没有干旱或火灾");
                break;
            case SpellKind.RuneWard:
                recipient = NearbyResidents(x, y, 1).Where(r =>
                        r.Value.NationId == caster.Value.NationId && r.Value.Health > 0 && r.Value.PersonalWard < 30)
                    .OrderBy(r => r.Value.PersonalWard).ThenBy(r => r.Value.Id).FirstOrDefault();
                if (recipient is null)
                    throw new InvalidOperationException("目标附近没有需要个人结界的本国居民");
                break;
        }

        caster.Replace(caster.Value.WithMana(caster.Value.Mana - (cost)));
        var efficiency = ResidentNeedsRules.WorkEfficiency(caster.Value, true);
        var power = (0.7 + caster.Value.MagicTalent / 150 + caster.Value.MagicTraining / 250) * efficiency;
        caster.Replace(caster.Value.WithAction(caster.Value.Agent with
        {
            Fatigue = ResidentNeedsRules.ExertionFatigue(caster.Value, ResidentNeedsRules.PhysicalWorkCostPerTick * ResidentNeedsRules.MentalWorkCostRatio),
        }, ResidentActivity.Casting));
        if (spell == SpellKind.Heal)
        {
            recipient!.Replace(recipient!.Value.WithHealth(Math.Min(100,
                recipient.Value.Health + 22 * power * (HasResearch(caster.Value.SettlementId, Advancement.Restoration) ? 1.5 : 1))));
            recipient.Replace(recipient.Value.WithSicknessTicks(Math.Max(0,
                recipient.Value.SicknessTicks - (int)(SimulationTime.TicksPerDay * efficiency))));
        }

        if (spell == SpellKind.HarvestBlessing)
            town!.Replace(town!.Value with { FertilityBoostTicks = Math.Max(town.Value.FertilityBoostTicks, (int)(2 * SimulationTime.TicksPerMonth * power)) });
        if (spell == SpellKind.Shield)
            town!.Replace(town!.Value with { ShieldTicks = Math.Max(town.Value.ShieldTicks, (int)(2 * SimulationTime.TicksPerDay * power)) });
        if (spell == SpellKind.Ember)
            DamageResident(recipient!, TryAbsorbShieldDamage(recipient!, 18 * power), DeathCause.Magic);
        if (spell == SpellKind.FrostBolt)
        {
            DamageResident(recipient!, TryAbsorbShieldDamage(recipient!, 10 * power * Rules.CombatDamageRate),
                DeathCause.Magic);
            recipient!.Replace(recipient!.Value with
            {
                FrozenUntilTick = Math.Max(recipient.Value.FrozenUntilTick, SimulationTick + (int)(6 * efficiency)),
            });
        }

        if (spell == SpellKind.ChainLightning)
        {
            foreach (var enemy in NearbyResidents(recipient!.Value.X, recipient.Value.Y, 2).Where(r => r.Value.Health > 0
                             && r.Value.NationId != caster.Value.NationId && Distance(caster.Value.X, caster.Value.Y, r.Value.X, r.Value.Y) <= 4
                             && IsKnownHostile(caster, r.Value.NationId) && ClearSignalLine(caster.Value.X, caster.Value.Y, r.Value.X, r.Value.Y))
                         .OrderBy(r => r.Value.Id).Take(3))
                DamageResident(enemy, TryAbsorbShieldDamage(enemy, 14 * power * Rules.CombatDamageRate),
                    DeathCause.Magic);
        }

        if (spell == SpellKind.RuneWard)
            recipient!.Replace(recipient!.Value with { PersonalWard = Math.Max(recipient.Value.PersonalWard, Math.Min(60, 30 * power)) });
        if (spell == SpellKind.RainCall)
        {
            foreach (var index in Circle(x, y, 2))
            {
                var tile = Tiles[index];
                tile.Replace(tile.Value.WithDroughtTicks(Math.Max(0,
                    tile.Value.DroughtTicks - (int)(2 * SimulationTime.TicksPerMonth * efficiency))));
                if (tile.Value.DroughtTicks == 0)
                    _dryTiles.Remove(index);
                if (tile.Value.FireTicks > 0)
                {
                    tile.Replace(tile.Value.WithFireTicks(Math.Max(0, tile.Value.FireTicks - (int)(8 * efficiency))));
                    if (tile.Value.FireTicks == 0)
                        EndFire(index, false);
                }
            }
        }

        EmitVisual(spell switch
        {
            SpellKind.Heal => WorldVisualKind.Heal,
            SpellKind.HarvestBlessing => WorldVisualKind.Harvest,
            SpellKind.Shield or SpellKind.RuneWard => WorldVisualKind.Shield,
            SpellKind.FrostBolt => WorldVisualKind.Frost,
            SpellKind.ChainLightning => WorldVisualKind.Lightning,
            SpellKind.RainCall => WorldVisualKind.Rain,
            _ => WorldVisualKind.Ember,
        }, x, y, 2, caster.Value.X, caster.Value.Y);
        var detail = SpellName(spell);
        caster.Replace(caster.Value.WithAgent(caster.Value.Agent.RecordDecision(new AgentDecision
        {
            Tick = SimulationTick,
            Goal = spell == SpellKind.Ember ? AgentGoalKind.Flee : AgentGoalKind.Work,
            Reason = $"在 {x},{y} 施放{detail}，消耗 {cost:0.#} 法力；天赋和训练决定效果",
        })));
        var magicEvent = AddEvent(WorldEventKind.Magic, $"{caster.Value.Name}施放{detail}，消耗 {cost:0.#} 法力。", x, y);
        magicEvent = PublishEvent(magicEvent with { ResidentId = caster.Value.Id, NationId = caster.Value.NationId });
    }

    /// <summary>按附近防御政策及护盾减伤，再消耗个人符文护甲和护甲，返回剩余伤害。</summary>
    /// <param name="resident">实际承受伤害并消耗个人防护的居民。</param>
    /// <param name="damage">减伤前的伤害量。</param>
    public double TryAbsorbShieldDamage(Resident resident, double damage)

    {
        return TryAbsorbShieldDamage(RequireResident(resident.Id), damage);
    }

    private double TryAbsorbShieldDamage(StateReference<Resident> resident, double damage)
    {
        var multiplier = 1d;
        foreach (var town in Settlements)
        {
            if (town.Value.NationId != resident.Value.NationId || Distance(town.Value.X, town.Value.Y, resident.Value.X, resident.Value.Y) > 5)
                continue;
            var protection = (GetLocalPolicy(town.Value.Id) == PolicyKind.Defense ? 0.88 : 1) *
                             (town.Value.ShieldTicks > 0 ? 0.6 : 1);
            multiplier = Math.Min(multiplier, protection);
        }

        var remaining = Math.Max(0, damage * multiplier);
        var ward = Math.Min(resident.Value.PersonalWard, remaining);
        remaining -= ward;
        var armor = Math.Min(resident.Value.Armor, remaining * .35);
        if (ward > 0 || armor > 0)
            resident.Replace(resident.Value with
            {
                PersonalWard = resident.Value.PersonalWard - ward,
                Armor = resident.Value.Armor - armor,
            });
        return remaining - armor;
    }

    /// <summary>在实体或地形编辑后修复城镇中心和连通占领，清理失效的社会归属记录。</summary>
    public void ReconcileSocietyTopology()
    {
        EnsureTownCenters();
        ReconcileConnectedClaims();
        var townIds = Settlements.Select(s => s.Value.Id).ToHashSet();
        var nationIds = Nations.Select(n => n.Value.Id).ToHashSet();
        Buildings.RemoveAll(b =>
            !townIds.Contains(b.Value.SettlementId) || !BuildingTerrainValid(b.Value.Kind, Tiles[Index(b.Value.X, b.Value.Y)].Value) ||
            (b.Value.Health <= 0 && b.Value.Kind != BuildingKind.TownCenter));
        Society = Society with
        {
            Research = Society.Research.RemoveAll(r => !townIds.Contains(r.SettlementId)),
        };
        Society = Society with
        {
            Policies = Society.Policies.RemoveAll(p => !townIds.Contains(p.SettlementId)),
        };
        Society = Society with
        {
            Institutions = Society.Institutions.RemoveAll(i => !nationIds.Contains(i.NationId)),
        };
        Society = Society with
        {
            Reports = Society.Reports.RemoveAll(r => !townIds.Contains(r.RecipientSettlementId)),
        };
        var people = Residents.Select(r => r.Value.Id).ToHashSet();
        Society = Society with
        {
            CulturalContacts = Society.CulturalContacts.RemoveAll(c => !people.Contains(c.ResidentId)),
        };
        foreach (var building in Buildings)
            building.Replace(building.Value with { Workers = building.Value.Workers.RemoveAll(id => !people.Contains(id)) });
        AssignResidentHomes();
        SynchronizeResidentRescues();
    }

    /// <summary>推进当前模拟 tick的社会发展。</summary>
    public void TickSociety()
    {
        if (SimulationTick % SimulationTime.TicksPerMonth == SimulationTime.WakeTick)
        {
            RefreshLocalRepresentatives();
            BalanceLocalWorkforce();
        }

        var townIds = Settlements.Select(s => s.Value.Id).ToHashSet();
        var nationIds = Nations.Select(n => n.Value.Id).ToHashSet();
        Society = Society with
        {
            Research = Society.Research.RemoveAll(r => !townIds.Contains(r.SettlementId)),
        };
        Society = Society with
        {
            Policies = Society.Policies.RemoveAll(p => !townIds.Contains(p.SettlementId)),
        };
        Society = Society with
        {
            Institutions = Society.Institutions.RemoveAll(i => !nationIds.Contains(i.NationId)),
        };
        Society = Society with
        {
            Reports = Society.Reports.RemoveAll(r =>
                !townIds.Contains(r.RecipientSettlementId) ||
                SimulationTick - r.ReceivedTick > 12 * SimulationTime.TicksPerYear),
        };
        var liveResidents = _societyResidentIds;
        liveResidents.Clear();
        foreach (var resident in Residents)
            liveResidents.Add(resident.Value.Id);
        Society = Society with
        {
            CulturalContacts =
            Society.CulturalContacts.RemoveAll(c => !liveResidents.Contains(c.ResidentId)),
        };
        foreach (var building in Buildings)
        {
            if (!InBounds(building.Value.X, building.Value.Y) || !townIds.Contains(building.Value.SettlementId))
            {
                building.Replace(building.Value with { Health = 0 });
                continue;
            }

            var tile = Tiles[Index(building.Value.X, building.Value.Y)];
            if (!BuildingTerrainValid(building.Value.Kind, tile.Value))
                building.Replace(building.Value with { Health = 0 });
            else if (tile.Value.FireTicks > 0)
                building.Replace(building.Value with { Health = Math.Max(0, building.Value.Health - 1.5 * BuildingFlammability(building.Value)) });
            if (IsHusbandry(building.Value.Kind) && SimulationTick - building.Value.LastServiceTick > SimulationTime.TicksPerMonth &&
                (SimulationTick + building.Value.Id) % 6 == 0)
            {
                building.Replace(building.Value with { LivestockPopulation = building.Value.LivestockPopulation * (Math.Pow(.98, 6d / SimulationTime.TicksPerDay)) });
                if (building.Value.LivestockPopulation < .01)
                    building.Replace(building.Value with
                    {
                        LivestockPopulation = 0, LivestockKind = WildlifeKind.None,
                    });
            }

            building.Replace(building.Value with { Workers = building.Value.Workers.RemoveAll(id => !liveResidents.Contains(id)) });
        }

        var crossingCollapsed = RemoveFailedCrossings();
        Buildings.RemoveAll(b => b.Value.Health <= 0 && b.Value.Kind != BuildingKind.TownCenter);
        if (crossingCollapsed)
        {
            RelocateInvalidEntities();
            _nearbyResidentTick = -1;
        }

        EnsureTownCenters();
        AssignResidentHomes();
        foreach (var town in Settlements)
        {
            if (town.Value.FertilityBoostTicks > 0)
                town.Replace(town.Value with { FertilityBoostTicks = town.Value.FertilityBoostTicks - 1 });
            if (town.Value.ShieldTicks > 0)
                town.Replace(town.Value with { ShieldTicks = town.Value.ShieldTicks - 1 });
            if (SimulationTick % SimulationTime.TicksPerMonth == 0)
                DecideLocalPolicy(town);
            if ((SimulationTick + town.Value.Id) % (2 * SimulationTime.TicksPerMonth) == 0)
                PlanLocalDevelopment(town);
            if (GetLocalPolicy(town.Value.Id) == PolicyKind.PublicHealth && town.Value.Resources.Food >= 0.02)
            {
                var patient = _citizens.GetValueOrDefault(town.Value.Id)
                    ?.Where(r => Distance(r.Value.X, r.Value.Y, town.Value.X, town.Value.Y) <= 2 && r.Value.Health is > 0 and < 99)
                    .OrderBy(r => r.Value.Health)
                    .FirstOrDefault();
                if (patient is not null)
                {
                    town.Replace(town.Value.WithResources(town.Value.Resources with
                    {
                        Food = town.Value.Resources.Food - 0.02 / SimulationTime.TicksPerDay,
                    }));
                    patient.Replace(patient.Value.WithHealth(Math.Min(100, patient.Value.Health + 0.15 / SimulationTime.TicksPerDay)));
                }
            }
        }

        _nearbyResidentsActive = true;
        try
        {
            foreach (var person in Residents)
            {
                if (!InBounds(person.Value.X, person.Value.Y) || person.Value.Health <= 0 || person.Value.Activity is ResidentActivity.Sleeping or ResidentActivity.Unconscious)
                    continue;
                if (person.Value.MagicTalent >= 25 && person.Value.MagicTraining >= 8 && (SimulationTick + person.Value.Id) % 12 == 0)
                    TryAutomaticMagic(person);
            }
        }
        finally
        {
            _nearbyResidentsActive = false;
        }

        RegenerateNaturalResources();
        liveResidents.Clear();
    }

    private void TryAutomaticMagic(StateReference<Resident> person)
    {
        if (person.Value.Agent.Goal.Kind is AgentGoalKind.DeliverMessage or AgentGoalKind.Trade ||
            person.Value.Agent.Goal.PlayerDirected || person.Value.Mana < SpellManaCost(SpellKind.Heal) * .85)
            return;
        StateReference<Resident>? patient = null;
        foreach (var candidate in NearbyResidents(person.Value.X, person.Value.Y, 3))
            if (candidate.Value.NationId == person.Value.NationId && candidate.Value.Health is > 0 and < 60
                                                      && (patient is null || candidate.Value.Health < patient.Value.Health
                                                                          || (candidate.Value.Health == patient.Value.Health &&
                                                                              candidate.Value.Id < patient.Value.Id)))
                patient = candidate;
        if (patient is not null && (person.Value.Agent.Personality.Sociability >= 0.3 || patient.Value.Id == person.Value.Id) &&
            TryCastSpell(person.Value.Id, SpellKind.Heal, patient.Value.X, patient.Value.Y))
            return;
        if (person.Value.ArmyId != 0 && person.Value.Agent.Personality.Courage >= 0.35)
        {
            StateReference<Resident>? enemy = null;
            foreach (var candidate in NearbyResidents(person.Value.X, person.Value.Y, 3))
                if (candidate.Value.NationId != person.Value.NationId && candidate.Value.Health > 0
                                                          && (enemy is null || candidate.Value.Id < enemy.Value.Id) &&
                                                          IsKnownHostile(person, candidate.Value.NationId))
                    enemy = candidate;
            if (enemy is not null)
            {
                if (person.Value.Profession == Profession.Battlemage &&
                    TryCastSpell(person.Value.Id, SpellKind.ChainLightning, enemy.Value.X, enemy.Value.Y))
                    return;
                if (HasResearch(person.Value.SettlementId, Advancement.Elementalism) && enemy.Value.FrozenUntilTick <= SimulationTick
                                                                               && TryCastSpell(person.Value.Id,
                                                                                   SpellKind.FrostBolt, enemy.Value.X,
                                                                                   enemy.Value.Y))
                    return;
                if (TryCastSpell(person.Value.Id, SpellKind.Ember, enemy.Value.X, enemy.Value.Y))
                    return;
            }
        }

        if (!_settlements.TryGetValue(person.Value.SettlementId, out var town) ||
            Distance(person.Value.X, person.Value.Y, town.Value.X, town.Value.Y) > 4)
            return;
        if (GetLocalPolicy(town.Value.Id) == PolicyKind.Defense && town.Value.ShieldTicks < 6 &&
            TryCastSpell(person.Value.Id, SpellKind.Shield, town.Value.X, town.Value.Y))
            return;
        if ((person.Value.Profession is Profession.Farmer or Profession.Mage ||
             GetCulture(person.Value.CultureId).NatureAffinity >= 0.6)
            && town.Value.FertilityBoostTicks < 6 &&
            (person.Value.Hunger > 30 || GetLocalPolicy(town.Value.Id) == PolicyKind.FoodSecurity))
            TryCastSpell(person.Value.Id, SpellKind.HarvestBlessing, town.Value.X, town.Value.Y);
    }

    private void RefreshLocalRepresentatives()
    {
        foreach (var town in Settlements)
        {
            if (Residents.Any(r =>
                    r.Value.Id == town.Value.RepresentativeId && r.Value.SettlementId == town.Value.Id && r.Value.Health > 0 &&
                    r.Value.Profession == Profession.Representative))
                continue;
            var representative = Residents.Where(r =>
                    r.Value.SettlementId == town.Value.Id && r.Value.Age >= 16 && r.Value.Health > 0 && r.Value.ArmyId == 0
                    && r.Value.Agent.DestinationSettlementId == 0 && Distance(r.Value.X, r.Value.Y, town.Value.X, town.Value.Y) <= 3)
                .OrderByDescending(r => r.Value.Profession == Profession.Representative)
                .ThenByDescending(r => r.Value.Agent.Personality.Sociability)
                .ThenBy(r => r.Value.Id).FirstOrDefault();
            var representativeId = representative?.Value.Id ?? 0;
            if (town.Value.RepresentativeId != representativeId)
                town.Replace(town.Value with { RepresentativeId = representativeId });
            if (representative is not null)
            {
                representative.Replace(representative.Value with
                {
                    Profession = Profession.Representative,
                    Agent = representative.Value.Agent with { JobChangedTick = SimulationTick, DailyPlan = null, DaytimeGoal = null },
                });
            }
        }

        foreach (var nation in Nations)
            nation.Replace(nation.Value with { RepresentativeId = Settlements.FirstOrDefault(s => s.Value.Id == nation.Value.CapitalId)?.Value.RepresentativeId ?? 0 });
    }

    private void PlanLocalDevelopment(StateReference<Settlement> town)
    {
        if (town.Value.FoundationPending)
            return;
        town.Replace(town.Value with { LastDevelopmentTick = SimulationTick });
        var local = Residents.Where(r => r.Value.SettlementId == town.Value.Id && r.Value.Age >= 16 && r.Value.Health > 50 &&
                                                 r.Value.ArmyId == 0
                                                 && Distance(r.Value.X, r.Value.Y, town.Value.X, town.Value.Y) <= 6 &&
                                                 r.Value.Agent.DestinationSettlementId == 0).ToArray();
        var buildings = Buildings.Where(b => b.Value.SettlementId == town.Value.Id).ToArray();
        var project = Society.Research.First(r => r.SettlementId == town.Value.Id);
        if (local.Length < 4)
        {
            town.Replace(town.Value with { DevelopmentGoal = "恢复当地劳动力", DevelopmentBlocker = "附近可工作的成年人少于 4 人" });
            return;
        }

        void Recruit(Profession job)
        {
            var needed = job switch
            {
                Profession.Lumberjack => (int)Math.Ceiling(Math.Max(0, 60 - town.Value.Resources.Wood) / 12),
                Profession.Miner => (int)Math.Ceiling(LocalMineralDeficit(town) / 12),
                Profession.Farmer => (int)Math.Ceiling(Math.Max(0, town.Value.Population * .4 - town.Value.Resources.Food) / 3),
                Profession.Builder => Math.Max(SettlementNeedsClaimArea(town) ? 1 : 0,
                    buildings.Where(b => !b.Value.IsCompleted || b.Value.IsUpgrading || b.Value.Health < 50).Sum(b => b.Value.WorkSlots)),
                _ => buildings
                    .Where(b => WorkplaceProfession(b.Value.Kind) == job && b.Value.Enabled && b.Value.IsCompleted && b.Value.Health >= 50)
                    .Sum(b => b.Value.WorkSlots),
            };
            needed = Math.Clamp(needed, 0, local.Length);
            if (local.Count(r => r.Value.Profession == job) >= needed)
                return;
            var recruit = local.Where(r =>
                    r.Value.Profession is Profession.Farmer or Profession.Lumberjack or Profession.Miner or Profession.Builder
                        or Profession.Scholar or Profession.Laborer
                    && AvailableForLocalAssignment(r, town)
                    && r.Value.Profession != job
                    && (r.Value.Profession == Profession.Laborer || (r.Value.Profession == Profession.Farmer
                        ? local.Count(p => p.Value.Profession == Profession.Farmer) >= 4
                        : local.Count(p => p.Value.Profession == r.Value.Profession) >= 2))
                    && SuitableForProfession(r, job))
                .OrderByDescending(r => ProfessionSuitability(r, job)).ThenBy(r => r.Value.Id).FirstOrDefault();
            if (recruit is null)
                return;
            ChangeLocalProfession(recruit, job);
            RecordLife(recruit, $"因家园发展需要，接受新的{ProfessionName(job)}岗位。");
            if (recruit.Value.History.Count > 24)
                recruit.Replace(recruit.Value with { History = recruit.Value.History.RemoveAt(0) });
        }

        if (buildings.Any(b => b.Value.Health > 0 && !b.Value.IsCompleted))
            Recruit(Profession.Builder);
        if (project.ActiveProject is not null && buildings.Any(b => b.Value.Kind == BuildingKind.Academy && b.Value.IsCompleted))
            Recruit(Profession.Scholar);
        if (Society.MagicEnabled && buildings.Any(b => b.Value.Kind == BuildingKind.ArcaneSanctum && b.Value.IsCompleted))
            Recruit(Profession.Mage);
        var demand = InspectLocalDemand(town, buildings);
        var lowFood = town.Value.Resources.Food < Math.Max(25, town.Value.Population * .4);
        if (lowFood)
            Recruit(Profession.Farmer);
        string? blockedGoal = null, blockedReason = null;

        void RememberBlocker(string reason)
        {
            town.Replace(town.Value with { DevelopmentBlocker = reason });
            if (blockedGoal is not null)
                return;
            blockedGoal = town.Value.DevelopmentGoal;
            blockedReason = reason;
        }

        bool PlanBuilding(BuildingKind kind)
        {
            if (kind == BuildingKind.Housing)
            {
                var plannedCapacity = buildings.Where(b => b.Value.Kind == BuildingKind.Housing
                    && b.Value.Enabled && b.Value.Health > 0 && !IsFacilityOperating(b.Value))
                    .Sum(b => b.Value.Level * HousingCapacityPerLevel);
                if (town.Value.Population <= GetHousingCapacity(town.Value.Id) + plannedCapacity)
                    return false;
            }
            var desired = kind == BuildingKind.Farm ? Math.Clamp((town.Value.Population + 39) / 40, 1, 32)
                : kind == BuildingKind.Housing ? 30
                : 1;
            if (buildings.Count(b => b.Value.Kind == kind && (b.Value.Health > 0 || !b.Value.Enabled)) >= desired)
                return false;
            if (!FacilityNeeded(demand, kind))
                return false;
            var buildingProjects =
                buildings.Count(b => b.Value.Health > 0 && !b.Value.IsCompleted && !IsPublicInfrastructure(b.Value.Kind));
            if (buildingProjects >=
                (kind is BuildingKind.Farm or BuildingKind.Well or BuildingKind.Housing or BuildingKind.Reservoir
                    ? 3
                    : 2))
                return false;
            town.Replace(town.Value with { DevelopmentGoal = "修建" + BuildingName(kind) });
            if (!Rules.Construction)
            {
                RememberBlocker("世界规则关闭了自主建设");
                return false;
            }

            var cost = GetBuildingCost(kind);
            // 住宅不能反复抢走下一研究或产业项目的木石；生存设施仍可直接使用现有材料。
            var reserve = kind == BuildingKind.Housing ? LocalDevelopmentReserve(town) : new ResourceStock();
            var required = cost;
            foreach (var resource in ResourceStock.Kinds)
                if (cost.Get(resource) > 0 && reserve.Get(resource) > 0)
                    required = required.WithAmount(resource, cost.Get(resource) + reserve.Get(resource));
            var missing = MissingResources(town.Value.Resources, required);
            if (missing is not null)
            {
                RememberBlocker(missing + "；安排采集与实物运输");
                if (town.Value.Resources.Wood < required.Wood)
                    Recruit(Profession.Lumberjack);
                if (town.Value.Resources.Stone < required.Stone ||
                    town.Value.Resources.Ore < required.Ore)
                    Recruit(Profession.Miner);
                return false;
            }

            var ruined = buildings.FirstOrDefault(b => b.Value.Kind == kind && b.Value.Enabled && b.Value.Health <= 0);
            if (ruined is not null && VisibleWorkSiteReachable(local[0], ruined.Value.X, ruined.Value.Y, true))
            {
                town.Replace(town.Value.WithResources(Spend(town.Value.Resources, GetBuildingCost(kind))));
                ruined.Replace(ruined.Value with
                {
                    Health = 100,
                    ConstructionProgress = 0,
                    UpgradeProgress = 0,
                    UpgradeRequired = 0,
                    PendingDirection = null,
                    Workers = ruined.Value.Workers.Clear(),
                    LastWorkedTick = -100,
                });
                var entry = AddEvent(WorldEventKind.Construction, town.Value.Name + "投入材料重建" + BuildingName(kind), ruined.Value.X,
                    ruined.Value.Y, EventAction.Started, town.Value.Id);
                ruined.Replace(ruined.Value with { Observation = new ProjectObservation { StartEventId = entry.Id } });
                Recruit(Profession.Builder);
                town.Replace(town.Value with { DevelopmentBlocker = "等待工人到场重建" });
                return true;
            }

            var position = BestBuildingSite(town, kind);
            if (position < 0)
            {
                RememberBlocker("附近没有用途合适且施工可达的占领地，需先勘察或登记地盘");
                Recruit(Profession.Builder);
                return false;
            }

            var reason = BuildingPurpose(town, kind);
            BuildPlannedFacility(town, kind, position % Width, position / Width, reason);
            Recruit(Profession.Builder);
            town.Replace(town.Value with { DevelopmentBlocker = "材料已备齐，等待工人到场" });
            return true;
        }

        if (project.ActiveProject is not null && !buildings.Any(b => b.Value.Kind == BuildingKind.Academy && b.Value.Health > 0))
        {
            PlanBuilding(BuildingKind.Academy);
            return;
        }

        // 基本生计也须参与项目优先级竞争，避免长期被研究和建设占用资源后无法补足。
        if (demand.Water)
        {
            foreach (var kind in HasResearch(town.Value.Id, Advancement.CivilEngineering)
                         ? new[] { BuildingKind.Reservoir, BuildingKind.Well }
                         : new[] { BuildingKind.Well })
                if (PlanBuilding(kind))
                    return;
        }

        if (demand.Patients && PlanBuilding(HasResearch(town.Value.Id, Advancement.Sanitation)
                ? BuildingKind.Hospital
                : BuildingKind.Infirmary))
            return;
        if (lowFood)
        {
            if (PlanBuilding(BuildingKind.Farm))
                return;
            Recruit(Profession.Farmer);
        }

        if (!buildings.Any(b => b.Value.Kind == BuildingKind.Academy && b.Value.Health > 0) && Rules.Research)
        {
            PlanBuilding(BuildingKind.Academy);
            return;
        }

        if (PlanBuilding(BuildingKind.Housing))
            return;

        foreach (var plan in PendingLocalDevelopment(town, buildings, project))
        {
            if (plan.Facility is { } facility)
            {
                if (PlanBuilding(facility))
                    return;
                continue;
            }

            var kind = plan.Research!;
            town.Replace(town.Value with { DevelopmentGoal = "研究" + kind.Name });
            if (!Rules.Research)
            {
                RememberBlocker("世界规则关闭了自主研究");
                continue;
            }

            var missing = MissingResources(town.Value.Resources, GetResearchCost(kind));
            if (missing is not null)
            {
                RememberBlocker(missing);
                if (town.Value.Resources.Wood < GetResearchCost(kind).Wood)
                    Recruit(Profession.Lumberjack);
                if (town.Value.Resources.Stone < GetResearchCost(kind).Stone ||
                    town.Value.Resources.Ore < GetResearchCost(kind).Ore)
                    Recruit(Profession.Miner);
                continue;
            }

            if (!buildings.Any(b => b.Value.Kind == BuildingKind.Academy && b.Value.IsCompleted))
            {
                RememberBlocker("学舍施工中，等待工人完工后研究");
                Recruit(Profession.Builder);
                continue;
            }

            StartResearch(town.Value.Id, kind);
            Recruit(Profession.Scholar);
            town.Replace(town.Value with { DevelopmentBlocker = "等待学者到学舍工作" });
            return;
        }

        if (blockedGoal is not null)
        {
            town.Replace(town.Value with { DevelopmentGoal = blockedGoal, DevelopmentBlocker = blockedReason! });
            return;
        }

        if (PlanBuildingUpgrade(town, buildings))
            return;
        town.Replace(town.Value with { DevelopmentGoal = Rules.Expansion ? "积累物资，建立新聚落" : "维持繁荣与对外交流" });
        town.Replace(town.Value with { DevelopmentBlocker = Rules.Expansion
            ? $"拓荒条件：人口 {town.Value.Population}/80；需要送达的建村勘察报告、相距至少 {MinimumSettlementDistance} 格的用地与携带补给的拓荒者；费用 {ResourceStock.Format(VillageFoundingCost)}"
            : "已有研究完成；扩张已关闭" });
    }

    private IEnumerable<LocalDevelopmentPlan> PendingLocalDevelopment(StateReference<Settlement> town,
        IReadOnlyList<StateReference<Building>> buildings, SettlementResearch project)
    {
        if (Rules.Research && !buildings.Any(b => b.Value.Kind == BuildingKind.Academy))
        {
            yield return new LocalDevelopmentPlan(BuildingKind.Academy, null, GetBuildingCost(BuildingKind.Academy));
            yield break;
        }

        var focus = GetDevelopmentFocus(town.Value.Id);
        var technology = focus is DevelopmentFocus.Technology or DevelopmentFocus.Integrated;
        var magic = Society.MagicEnabled && focus is DevelopmentFocus.MagicPractice
            or DevelopmentFocus.ArcaneIndustry or DevelopmentFocus.Integrated;
        var magicalIndustry = magic && focus is DevelopmentFocus.ArcaneIndustry or DevelopmentFocus.Integrated;
        var demand = InspectLocalDemand(town, buildings);
        if (magic && HasResearch(town.Value.Id, Advancement.ArcaneArts) &&
            !buildings.Any(b => b.Value.Kind == BuildingKind.ArcaneSanctum)
            && FacilityNeeded(demand, BuildingKind.ArcaneSanctum))
        {
            yield return new LocalDevelopmentPlan(BuildingKind.ArcaneSanctum, null,
                GetBuildingCost(BuildingKind.ArcaneSanctum));
        }

        var wanted = ResearchRules.All.Where(definition => definition.Shared || (definition.Magic
            ? magicalIndustry || (magic && (definition == Advancement.ArcaneArts ||
                                            definition == Advancement.ManaAttunement
                                            || definition == Advancement.Restoration ||
                                            definition == Advancement.Elementalism ||
                                            definition == Advancement.NatureBinding))
            : technology)).ToArray();
        foreach (var definition in wanted.Where(r => HasResearch(town.Value.Id, r)))
        foreach (var facility in definition.UnlockedBuildings)
            if (!buildings.Any(b => b.Value.Kind == facility && (b.Value.Health > 0 || !b.Value.Enabled)) &&
                FacilityNeeded(demand, facility))
                yield return new LocalDevelopmentPlan(facility, null, GetBuildingCost(facility));
        if (project.ActiveProject is null)
        {
            foreach (var definition in wanted.Where(r =>
                             !HasResearch(town.Value.Id, r) && ResearchPrerequisiteError(town.Value.Id, r) is null)
                         .Select(r => (Research: r, Utility: ResearchUtility(demand, r))).Where(p => p.Utility > 0)
                         .OrderByDescending(p => p.Utility).ThenBy(p => p.Research.Id))
                yield return new LocalDevelopmentPlan(null, definition.Research,
                    GetResearchCost(definition.Research));
        }

        (BuildingKind Kind, bool Needed)[] facilities =
        [
            (BuildingKind.Infirmary, GetLocalPolicy(town.Value.Id) == PolicyKind.PublicHealth),
            (BuildingKind.ArcaneSanctum, magic && HasResearch(town.Value.Id, Advancement.ArcaneArts)),
            (BuildingKind.Waystation, HasResearch(town.Value.Id, Advancement.Logistics)), (BuildingKind.SignalTower,
                technology && HasResearch(town.Value.Id, Advancement.SignalNetwork) &&
                HasResearch(town.Value.Id, Advancement.Electrification)),
            (BuildingKind.Dock, HasResearch(town.Value.Id, Advancement.Logistics)),
            (BuildingKind.Shipyard, HasResearch(town.Value.Id, Advancement.Logistics)),
            (BuildingKind.LumberCamp, town.Value.Population >= 24), (BuildingKind.Quarry, town.Value.Population >= 24),
            (BuildingKind.Well, town.Value.Resources.Water < town.Value.Population * 2),
            (BuildingKind.Granary, town.Value.Population >= 40), (BuildingKind.Housing, town.Value.Population >= 30),
            (BuildingKind.Market, town.Value.Tier >= SettlementTier.Town),
            (BuildingKind.Watchtower, town.Value.Tier >= SettlementTier.Town),
        ];
        foreach (var (kind, needed) in facilities)
            if (needed && FacilityNeeded(demand, kind) &&
                !buildings.Any(b => b.Value.Kind == kind && (b.Value.Health > 0 || !b.Value.Enabled)))
                yield return new LocalDevelopmentPlan(kind, null, GetBuildingCost(kind));
        foreach (var kind in Enum.GetValues<BuildingKind>())
            if (BuildingRace(kind) is not null && !buildings.Any(b => b.Value.Kind == kind)
                                               && CanBuildRacialFacility(town.Value.Id, kind) && FacilityNeeded(demand, kind)
                                               && (kind != BuildingKind.DwarvenForge ||
                                                   HasResearch(town.Value.Id, Advancement.Industry))
                                               && (kind != BuildingKind.SacredGrove ||
                                                   (magic && HasResearch(town.Value.Id, Advancement.ArcaneArts))))
                yield return new LocalDevelopmentPlan(kind, null, GetBuildingCost(kind));
    }

    private ResourceStock LocalDevelopmentReserve(StateReference<Settlement> town)
    {
        if (_localWorkQueriesActive && _productionReserves.TryGetValue(town.Value.Id, out var cached))
            return cached;
        // 当本阶段第一次实际查询需求时评估；库存仍在各次取料时读取，阶段结束即清除计划费用。
        var reserve = CalculateLocalDevelopmentReserve(town);
        if (_localWorkQueriesActive)
            _productionReserves[town.Value.Id] = reserve;
        return reserve;
    }

    private ResourceStock CalculateLocalDevelopmentReserve(StateReference<Settlement> town)
    {
        IReadOnlyList<StateReference<Building>> buildings = _localWorkQueriesActive
            ? _localWorkBuildings.GetValueOrDefault(town.Value.Id) ?? []
            : Buildings.Where(b => b.Value.SettlementId == town.Value.Id).ToArray();
        var project = FindSettlementResearch(town.Value.Id)!;
        // 为下一项可执行的本地计划预留木石；缺矿可推迟魔法项目，但不能阻止基础运输发展。
        // 只评估首个可选计划，并保留实时库存引用，使矿工在自己行动时仍能看到实际需求。
        foreach (var plan in PendingLocalDevelopment(town, buildings, project))
            if (plan.Facility.HasValue ? Rules.Construction : Rules.Research)
                return plan.Cost;
        return new ResourceStock();
    }

    private static void ValidateSocietyState(WorldState state)
    {
        static void Need(bool condition, string message)
        {
            if (!condition)
                throw new ArgumentException("无效存档：" + message);
        }

        static bool Range(double value, double max)
        {
            return double.IsFinite(value) && value >= 0 && value <= max;
        }

        static bool Text(string? value, int max = 1000)
        {
            return value is not null && value.Length <= max && !value.Any(c => char.IsControl(c) && c != '\n');
        }

        var society = state.Society;
        Need(society is not null, "缺少社会状态。");
        Need(society!.Cultures is { Count: > 0 and <= 64 } && society.Buildings is not null &&
             society.Buildings.Count <= MaxBuildings
             && society.Research is not null && society.Research.Count <= 256 && society.Policies is not null &&
             society.Policies.Count <= 256
             && society.Institutions is not null && society.Institutions.Count <= 64 && society.Reports is not null &&
             society.Reports.Count <= 2_048
             && society.CulturalContacts is not null && society.CulturalContacts.Count <= MaxPopulation * 64,
            "社会记录数量或集合无效。");
        var cultureIds = new HashSet<int>();
        foreach (var culture in society.Cultures!)
            Need(culture is not null && culture.Id is > 0 and <= 100_000 && cultureIds.Add(culture.Id)
                 && Text(culture.Name, 40) && culture.Name.Length > 0 && Range(culture.Cooperation, 1) &&
                 Range(culture.Innovation, 1) && Range(culture.NatureAffinity, 1), "文化定义无效。");
        var towns = state.Settlements.ToDictionary(s => s.Id);
        var nations = state.Nations.Select(n => n.Id).ToHashSet();
        var residents = state.Residents.Select(r => r.Id).ToHashSet();
        foreach (var person in state.Residents)
            Need(cultureIds.Contains(person.CultureId), "居民文化引用无效。");
        foreach (var town in state.Settlements)
            Need(cultureIds.Contains(town.CultureId), "聚落文化引用无效。");
        foreach (var nation in state.Nations)
            Need(cultureIds.Contains(nation.CultureId), "国家文化引用无效。");
        var buildingIds = new HashSet<int>();
        var occupied = new HashSet<(int, int)>();
        var entityIds = state.Residents.Select(r => r.Id).Concat(state.Nations.Select(n => n.Id))
            .Concat(state.Settlements.Select(s => s.Id)).Concat(state.Armies.Select(a => a.Id)).ToHashSet();
        foreach (var building in society.Buildings!)
            Need(building is not null && building.Id > 0 && building.Id < state.NextId &&
                 buildingIds.Add(building.Id) && !entityIds.Contains(building.Id)
                 && towns.ContainsKey(building.SettlementId) && Enum.IsDefined(building.Kind) && building.X >= 0 &&
                 building.Y >= 0 && building.X < state.Width && building.Y < state.Height
                 && occupied.Add((building.X, building.Y)) && Range(building.Health, 100) &&
                 Range(building.ConstructionProgress, 10_000)
                 && building.ConstructionRequired > 0 && building.ConstructionRequired <= 10_000 &&
                 building.ConstructionProgress <= building.ConstructionRequired
                 && building.Level is >= 1 and <= 3 && Enum.IsDefined(building.Direction)
                 && (building.PendingDirection is null || (building.Kind == BuildingKind.Bridge &&
                                                           Enum.IsDefined(building.PendingDirection.Value)))
                 && Range(building.UpgradeProgress, 10_000) && Range(building.UpgradeRequired, 10_000) &&
                 building.UpgradeProgress <= building.UpgradeRequired
                 && (building.UpgradeRequired == 0
                     ? building.PendingDirection is null
                     : building.ConstructionProgress >= building.ConstructionRequired &&
                       (building.PendingDirection.HasValue || building.Level < 3))
                 && Enum.IsDefined(building.LivestockKind) &&
                 Range(building.LivestockPopulation, LivestockCapacity(building))
                 && (IsHusbandry(building.Kind)
                     ? building.LivestockPopulation == 0 || CanDomesticate(building.LivestockKind,
                         building.Kind == BuildingKind.Aquaculture)
                     : building.LivestockKind == WildlifeKind.None && building.LivestockPopulation == 0)
                 && building.ProductionBatches is >= 0 and <= 1_000_000_000
                 && Range(building.ProductionProgress, 1) && building.ProductionProgress < 1
                 && building.ServiceActions is >= 0 and <= 1_000_000_000 && building.LastServiceTick >= -100 &&
                 building.LastServiceTick <= state.Tick
                 && building.WorkSlots is > 0 and <= 20 && building.Workers is not null &&
                 building.Workers.Count <= building.WorkSlots
                 && building.Workers.Distinct().Count() == building.Workers.Count &&
                 building.Workers.All(residents.Contains)
                 && building.LastWorkedTick >= -100 && building.LastWorkedTick <= state.Tick
                 && Text(building.PlanningReason, 512) && Text(building.SiteReason, 512), "设施状态或引用无效。");
        var researchTowns = new HashSet<int>();
        foreach (var research in society.Research!)
            Need(research is not null && towns.ContainsKey(research.SettlementId) &&
                 researchTowns.Add(research.SettlementId)
                 && Range(research.Progress, 10_000) && Range(research.RequiredProgress, 10_000)
                 && research.Completed is not null &&
                 research.Completed.Count <= Advancement.All.Count &&
                 research.Completed.All(r => r is not null)
                 && research.Completed.Distinct().Count() == research.Completed.Count &&
                 (research.ActiveProject is null || !research.Completed.Contains(research.ActiveProject))
                 && (research.ActiveProject is null ||
                     (research.RequiredProgress > 0 && research.Progress < research.RequiredProgress)), "研究状态无效。");
        Need(researchTowns.Count == towns.Count, "聚落研究状态缺失。");
        var policyTowns = new HashSet<int>();
        foreach (var policy in society.Policies!)
            Need(policy is not null && towns.ContainsKey(policy.SettlementId) && policyTowns.Add(policy.SettlementId)
                 && Enum.IsDefined(policy.Kind) && policy.DecidedTick >= 0 && policy.DecidedTick <= state.Tick &&
                 policy.EvidenceObservedTick >= 0 && policy.EvidenceObservedTick <= state.Tick
                 && Text(policy.Reason), "政策状态无效。");
        Need(policyTowns.Count == towns.Count, "聚落政策状态缺失。");
        var institutionNations = new HashSet<int>();
        foreach (var institution in society.Institutions!)
            Need(institution is not null && nations.Contains(institution.NationId) &&
                 institutionNations.Add(institution.NationId)
                 && Enum.IsDefined(institution.Kind) && (!institution.PlayerPolicy.HasValue ||
                                                         Enum.IsDefined(institution.PlayerPolicy.Value))
                 && institution.LastDecisionTick >= 0 && institution.LastDecisionTick <= state.Tick &&
                 Text(institution.LastDecision), "制度状态无效。");
        Need(institutionNations.Count == nations.Count, "国家制度状态缺失。");
        var reports = new HashSet<(int, int)>();
        foreach (var report in society.Reports!)
            Need(report is not null && towns.ContainsKey(report.RecipientSettlementId) && report.FactId > 0 &&
                 report.FactId < state.NextId
                 && reports.Add((report.RecipientSettlementId, report.FactId)) && Enum.IsDefined(report.Topic) &&
                 Enum.IsDefined(report.ReportedProfession)
                 && double.IsFinite(report.Value) && Math.Abs(report.Value) <= 1_000_000_000 &&
                 Range(report.Confidence, 1)
                 && report.ObservedTick >= 0 && report.ObservedTick <= report.ReceivedTick &&
                 report.ReceivedTick <= state.Tick, "机构报告无效。");
        var contacts = new HashSet<(int, int)>();
        foreach (var contact in society.CulturalContacts!)
            Need(contact is not null && residents.Contains(contact.ResidentId) && cultureIds.Contains(contact.CultureId)
                 && contacts.Add((contact.ResidentId, contact.CultureId)) && Range(contact.Exposure, 20) &&
                 contact.LastContactTick >= -12 && contact.LastContactTick <= state.Tick, "文化交流状态无效。");
    }

    private void AddPublicFact(StateReference<Settlement> town, AgentFact fact)
    {
        if (town.Value.PublicKnowledge.Any(f => f.Id == fact.Id))
            return;
        var prior = town.Value.PublicKnowledge.FirstOrDefault(f =>
            fact.HasSameSubject(f));
        if (prior is not null)
        {
            if (prior.ObservedTick >= fact.ObservedTick)
                return;
            town.Replace(town.Value with { PublicKnowledge = town.Value.PublicKnowledge.Remove(prior) });
        }

        town.Replace(town.Value with { PublicKnowledge = town.Value.PublicKnowledge.Add(fact) });
        while (town.Value.PublicKnowledge.Count > 24)
            town.Replace(town.Value with { PublicKnowledge = town.Value.PublicKnowledge.RemoveAt(0) });
    }

    private static void AddResidentFact(StateReference<Resident> resident, AgentFact fact)
    {
        if (resident.Value.Agent.Memory.Any(f => f.Id == fact.Id))
            return;
        var memory = resident.Value.Agent.Memory.Add(fact);
        if (memory.Length > 16)
            memory = memory.RemoveAt(0);
        resident.Replace(resident.Value.WithAgent(resident.Value.Agent with { Memory = memory }));
    }

    private static ResourceStock Spend(in ResourceStock stock, in ResourceStock cost)
    {
        if (MissingResources(stock, cost) is not null)
            throw new InvalidOperationException("当地材料不足；" + MissingResources(stock, cost));
        return stock with
        {
            Food = Math.Max(0, stock.Food - cost.Food),
            Wood = Math.Max(0, stock.Wood - cost.Wood),
            Stone = Math.Max(0, stock.Stone - cost.Stone),
            Ore = Math.Max(0, stock.Ore - cost.Ore),
            Alloy = Math.Max(0, stock.Alloy - cost.Alloy),
            EnergyCells = Math.Max(0, stock.EnergyCells - cost.EnergyCells),
            Crystals = Math.Max(0, stock.Crystals - cost.Crystals),
            Coal = Math.Max(0, stock.Coal - cost.Coal),
            Oil = Math.Max(0, stock.Oil - cost.Oil),
            RareEarth = Math.Max(0, stock.RareEarth - cost.RareEarth),
            Boats = Math.Max(0, stock.Boats - cost.Boats),
            Aircraft = Math.Max(0, stock.Aircraft - cost.Aircraft),
            Water = Math.Max(0, stock.Water - cost.Water),
        };
    }

    private StateReference<Settlement> RequireTown(int id)
    {
        return _settlements.TryGetValue(id, out var town) ? town : throw new ArgumentException("聚落不存在。");
    }

    private StateReference<Nation> RequireNation(int id)
    {
        return _nations.TryGetValue(id, out var nation) ? nation : throw new ArgumentException("国家不存在。");
    }

    private CultureDefinition RequireCulture(int id)
    {
        return Society.Cultures.FirstOrDefault(c => c.Id == id) ?? throw new ArgumentException("文化不存在。");
    }

    private CultureDefinition GetCulture(int id)
    {
        return Society.Cultures.FirstOrDefault(c => c.Id == id) ?? Society.Cultures[0];
    }

    /// <summary>返回建筑类型的中文名称。</summary>
    /// <param name="kind">设施类别。</param>
    public static string BuildingName(BuildingKind kind)
    {
        return kind switch
        {
            BuildingKind.Farm => "农田",
            BuildingKind.Workshop => "工坊",
            BuildingKind.Academy => "学舍",
            BuildingKind.Waystation => "驿站",
            BuildingKind.SignalTower => "无线信号塔",
            BuildingKind.ArcaneSanctum => "奥术研习所",
            BuildingKind.Infirmary => "医馆",
            BuildingKind.MountainPass => "山路",
            BuildingKind.Bridge => "桥梁",
            BuildingKind.Dock => "码头",
            BuildingKind.TownCenter => "城镇中心",
            BuildingKind.Shipyard => "船坞",
            BuildingKind.LumberCamp => "林场",
            BuildingKind.Quarry => "采石场",
            BuildingKind.Well => "水井",
            BuildingKind.Granary => "粮仓",
            BuildingKind.Housing => "住宅",
            BuildingKind.Market => "集市",
            BuildingKind.Watchtower => "瞭望塔",
            BuildingKind.AssemblyHall => "议事厅",
            BuildingKind.TradeGuild => "商贸公会",
            BuildingKind.SacredGrove => "精灵圣林",
            BuildingKind.HerbGarden => "草药园",
            BuildingKind.MiningHall => "矮人矿业工坊",
            BuildingKind.HuntingCamp => "兽人狩猎营",
            BuildingKind.WarDrum => "战鼓营",
            BuildingKind.Reservoir => "蓄水站",
            BuildingKind.Hospital => "医院",
            BuildingKind.FireStation => "消防站",
            BuildingKind.Library => "图书馆",
            BuildingKind.SurveyOffice => "勘测所",
            BuildingKind.Armory => "装备工坊",
            BuildingKind.WardTower => "结界塔",
            BuildingKind.StormSpire => "风暴尖塔",
            BuildingKind.GroveSanctuary => "共生林苑",
            BuildingKind.Waygate => "折跃门",
            BuildingKind.Pasture => "牧场",
            BuildingKind.Aquaculture => "水产养殖厂",
            _ => ProductionRules.For(kind)?.FacilityName ?? kind.ToString(),
        };
    }

    /// <summary>返回政策的中文名称。</summary>
    /// <param name="kind">政策类别。</param>
    public static string PolicyName(PolicyKind kind)
    {
        return kind switch
        {
            PolicyKind.FoodSecurity => "粮食保障",
            PolicyKind.Defense => "防务优先",
            PolicyKind.Scholarship => "求知兴学",
            PolicyKind.PublicHealth => "公共医疗",
            _ => "均衡发展",
        };
    }

    private sealed record LocalDevelopmentPlan(BuildingKind? Facility, Advancement? Research, ResourceStock Cost);
}
