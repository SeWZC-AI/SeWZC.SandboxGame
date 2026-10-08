using SeWZC.WorldBox.Core.Runtime;
namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    /// <summary>世界设施记录数量的上限。</summary>
    public const int MaxBuildings = 1_536;

    /// <summary>为当前世界及新聚落补齐社会默认状态，已存在的记录保留。</summary>
    public void InitializeSociety()
    {
        if (Current.Society.Cultures.Count == 0)
        {
            Current.Society.Cultures.AddRange([
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
            ]);
        }

        foreach (var nation in Current.Nations)
        {
            var capital = Current.Settlements.FirstOrDefault(s => s.Id == nation.CapitalId);
            if (!Current.Society.Cultures.Any(c => c.Id == nation.CultureId))
            {
                nation.CultureId =
                    capital is null ? Current.Society.Cultures[0].Id : InitialCulture(capital.X, capital.Y);
            }

            if (!Current.Society.Institutions.Any(i => i.NationId == nation.Id))
                Current.Society.Institutions.Add(new NationInstitutionCursor { NationId = nation.Id });
        }

        EnsureTownCenters();
        foreach (var town in Current.Settlements)
        {
            if (!Current.Society.Cultures.Any(c => c.Id == town.CultureId))
            {
                town.CultureId = Current.Nations.FirstOrDefault(n => n.Id == town.NationId)?.CultureId ??
                                 Current.Society.Cultures[0].Id;
            }

            if (!Current.Society.Policies.Any(p => p.SettlementId == town.Id))
            {
                var manual = Current.Society.Institutions.FirstOrDefault(i => i.NationId == town.NationId)?.PlayerPolicy;
                Current.Society.Policies.Add(new LocalPolicyCursor
                {
                    SettlementId = town.Id,
                    Kind = manual ?? PolicyKind.Balanced,
                    PlayerOverride = manual.HasValue,
                });
            }

            if (Current.Society.Research.Any(r => r.SettlementId == town.Id))
                continue;
            Current.Society.Research.Add(new SettlementResearchCursor { SettlementId = town.Id });
            // 定居家庭携带初始农场和工坊，后续设施仍须实际建设。
            if (!town.FoundationPending)
            {
                AddFoundingFacility(town, BuildingKind.Farm);
                AddFoundingFacility(town, BuildingKind.Workshop);
            }
        }

        foreach (var resident in Current.Residents)
        {
            if (Current.Society.Cultures.Any(c => c.Id == resident.CultureId))
                continue;
            resident.CultureId = Current.Settlements.FirstOrDefault(s => s.Id == resident.SettlementId)?.CultureId ??
                                 Current.Society.Cultures[0].Id;
            var baseTalent =
                resident.Race switch
                {
                    RaceKind.Elf => 45,
                    RaceKind.Dwarf => 23,
                    RaceKind.Orc => 28,
                    _ => 32,
                };
            resident.MagicTalent = baseTalent + unchecked(((uint)resident.Id * 2654435761u) ^ (uint)Current.Seed) % 36;
        }

        ReconcileSocietyTopology();
        RefreshLocalRepresentatives();
        BalanceLocalWorkforce();
    }

    private int InitialCulture(int x, int y)
    {
        var preferred = Current.Tiles[Index(x, y)].Terrain switch
        {
            TerrainType.Sand or TerrainType.Desert => 4,
            TerrainType.Forest => 3,
            TerrainType.Snow or TerrainType.Hills or TerrainType.Tundra => 2,
            _ => 1,
        };
        return Current.Society.Cultures.Any(c => c.Id == preferred) ? preferred : Current.Society.Cultures[0].Id;
    }

    private void AddFoundingFacility(SettlementCursor town, BuildingKind kind)
    {
        if (Current.Society.Buildings.Count >= MaxBuildings - 256)
            return;
        var position = BestBuildingSite(town, kind, true);
        if (position >= 0)
        {
            var building = new BuildingCursor
            {
                Id = NewId(),
                SettlementId = town.Id,
                Kind = kind,
                X = position % Current.Width,
                Y = position / Current.Width,
                ConstructionProgress = 30,
                ConstructionRequired = 30,
            };
            Current.Society.Buildings.Add(building);
            CompleteLandImprovement(building);
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
                Food = 15,
                Wood = 30,
                Stone = 20,
            },
            BuildingKind.SacredGrove or BuildingKind.HerbGarden => new ResourceStock
            {
                Food = 20,
                Wood = 25,
                Stone = 15,
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
            _ => ProductionRules.For(kind)?.BuildingCost.ToStock() ?? throw new ArgumentOutOfRangeException(nameof(kind)),
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
    public int BuildFacility(int settlementId, BuildingKind kind, int x, int y, BridgeDirection? direction = null, int bridgeLevel = 1)
    {
        return PlaceFacility(settlementId, kind, x, y, false, direction, bridgeLevel);
    }

    private int PlaceFacility(int settlementId, BuildingKind kind, int x, int y, bool gift,
        BridgeDirection? direction = null, int bridgeLevel = 1)
    {
        if (FacilityPlacementError(settlementId, kind, x, y, gift, direction, bridgeLevel) is { } error)
            throw new InvalidOperationException(error);
        var town = RequireTown(settlementId);
        var tile = Current.Tiles[Index(x, y)];
        if (!gift)
            town.Resources = Spend(town.Resources, FacilityCost(kind, bridgeLevel));
        var building = new BuildingCursor
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
        };
        Current.Society.Buildings.Add(building);
        if (_localWorkQueriesActive)
        {
            _workBuildingsById[building.Id] = building;
            LocalWorkGroup(_localWorkBuildings, _localWorkBuildingBuffers, settlementId).Add(building);
        }

        if (gift)
        {
            building.ConstructionProgress = building.ConstructionRequired;
            if (IsForestTerrain(tile.Terrain) && !PreserveBuildingForest(kind))
            {
                tile.Terrain = TerrainType.Grass;
                tile.ResourceAmount = 0;
            }

            CompleteLandImprovement(building);
            EmitVisual(WorldVisualKind.Construction, x, y);
        }

        // 水上项目起点尚未登记，须在本城镇陆岸旁先登记施工地块，否则归属检查会阻止首次施工。
        if (gift || IsWaterfrontBuilding(kind))
            RegisterBuildingGround(building);
        var projectEvent = AddEvent(WorldEventKind.Construction,
            gift
                ? $"玩家向{town.Name}赐予{BuildingName(kind)}；效果受建筑健康、启用与当地条件限制。"
                : $"{town.Name}备好材料，开始修建{BuildingName(kind)}；居民必须到场施工。", x, y,
            gift ? EventAction.Gifted : EventAction.Started, town.Id);
        building.Observation = building.Observation with { StartEventId = projectEvent.Id };
        building.Observation = building.Observation.Observe(Current.Tick, Current.Rules.DevelopmentRate, building.ConstructionProgress);
        RefreshTotals();
        return building.Id;
    }

    /// <summary>校验并扣除本地材料，在笔刷范围内没有道路的可通行地格修建道路。</summary>
    /// <param name="settlementId">聚落 ID。</param>
    /// <param name="x">横向地格坐标。</param>
    /// <param name="y">纵向地格坐标。</param>
    /// <param name="radius">道路笔刷的作用半径，以地格为单位。</param>
    public void BuildRoad(int settlementId, int x, int y, int radius = 1)
    {
        var town = RequireTown(settlementId);
        if (!InBounds(x, y) || Distance(x, y, town.X, town.Y) > 24)
            throw new ArgumentException("道路须位于聚落周边 24 格内。");
        if (radius is < 0 or > 4)
            throw new ArgumentOutOfRangeException(nameof(radius), "道路笔刷半径须在 0 到 4 之间。");
        var tiles = Circle(x, y, radius).Where(i => Current.Tiles[i].IsWalkable && Current.Tiles[i].RoadLevel == 0)
            .ToArray();
        if (tiles.Length == 0)
            throw new InvalidOperationException("笔刷内没有可修建道路的土地。");
        town.Resources = Spend(town.Resources, new ResourceStock { Wood = tiles.Length * 0.5, Stone = tiles.Length });
        foreach (var index in tiles)
            Current.Tiles[index].RoadLevel = 1;
        RefreshTotals();
    }

    /// <summary>启动聚落研究项目。</summary>
    /// <param name="settlementId">聚落 ID。</param>
    /// <param name="project">研究项目。</param>
    public void StartResearch(int settlementId, Advancement project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var town = RequireTown(settlementId);
        var research = Current.Society.Research.First(r => r.SettlementId == settlementId);
        if (research.Completed.Contains(project))
            throw new InvalidOperationException("当地已经掌握这项知识。");
        if (research.ActiveProject is not null)
            throw new InvalidOperationException("当地已有正在进行的研究。");
        if (!Current.Society.Buildings.Any(b =>
                b.SettlementId == settlementId && b.Kind == BuildingKind.Academy && b.IsCompleted))
            throw new InvalidOperationException("研究需要已建成的学舍与实际到场的研究人员。");
        if (ResearchPrerequisiteError(settlementId, project) is { } prerequisite)
            throw new InvalidOperationException(prerequisite);
        town.Resources = Spend(town.Resources, GetResearchCost(project));
        research.Replace(research.Value with { ActiveProject = project, Progress = 0, RequiredProgress = project.Work });
        var start = AddEvent(WorldEventKind.Research, $"{town.Name}投入材料，开始研究{project.Name}。", town.X, town.Y,
            EventAction.Started, town.Id);
        research.Observation = new ProjectObservation
        {
            StartEventId = start.Id,
            DevelopmentRate = Current.Rules.DevelopmentRate,
        };
        research.Observation = research.Observation.Observe(Current.Tick, Current.Rules.DevelopmentRate, 0);
        RefreshTotals();
    }

    /// <summary>判断指定聚落是否已经掌握某项研究。</summary>
    /// <param name="settlementId">聚落 ID。</param>
    /// <param name="project">研究项目。</param>
    public bool HasResearch(int settlementId, Advancement project)
    {
        return _knowledgeQueriesActive
            ? (uint)project.Id < 64 && (_knowledgeByTown.GetValueOrDefault(settlementId) & (1UL << project.Id)) != 0
            : (_localWorkQueriesActive
                ? _localResearch.GetValueOrDefault(settlementId)
                : FindSettlementResearch(settlementId))?.Completed
            .Contains(project) == true;
    }

    private SettlementResearchCursor? FindSettlementResearch(int settlementId)
    {
        foreach (var research in Current.Society.Research)
            if (research.SettlementId == settlementId) return research;
        return null;
    }

    /// <summary>查询聚落所属国家的古代工具等级。</summary>
    /// <param name="settlementId">聚落 ID。</param>
    public int GetLocalTechnologyLevel(int settlementId)
    {
        return 1 + (Current.Society.Research.FirstOrDefault(r => r.SettlementId == settlementId)?.Completed
            .Count(k => k.ImprovesBasicTechnology) ?? 0);
    }

    /// <summary>将实际收到的研究知识记入聚落，关联其递送依据和前因。</summary>
    /// <param name="settlementId">聚落 ID。</param>
    /// <param name="project">研究项目。</param>
    /// <param name="causeEventId">关联的前因事件 ID，0 表示未指定前因。</param>
    /// <param name="evidenceFactId">关联的信息依据 ID，0 表示未指定依据。</param>
    public void GrantReceivedResearch(int settlementId, Advancement project, int causeEventId = 0, int evidenceFactId = 0)
    {
        ArgumentNullException.ThrowIfNull(project);
        var town = RequireTown(settlementId);
        var research = Current.Society.Research.First(r => r.SettlementId == settlementId);
        if (research.Completed.Contains(project))
            return;
        research.Completed.Add(project);
        research.Completed.Sort((left, right) => left.Id.CompareTo(right.Id));
        if (_knowledgeQueriesActive)
            _knowledgeByTown[settlementId] = _knowledgeByTown.GetValueOrDefault(settlementId) | (1UL << project.Id);
        if (_nations.TryGetValue(town.NationId, out var nation))
            nation.Technology = Math.Max(nation.Technology, GetLocalTechnologyLevel(town.Id));
        var entry = AddEvent(WorldEventKind.Research, $"{town.Name}掌握了{project.Name}；知识可由当地居民和信使继续传授。", town.X,
            town.Y,
            EventAction.Completed, town.Id, causeEventId: causeEventId, evidenceFactId: evidenceFactId);
        research.LastCompletionEventId = entry.Id;
        if (research.ActiveProject == project)
        {
            if (research.Observation.StartEventId > 0 && research.Observation.StartEventId != causeEventId)
                entry.AdditionalCauseEventIds.Add(research.Observation.StartEventId);
            foreach (var person in Current.Residents.Where(r => research.Observation.Contributors.Contains(r.Id)))
                RecordLife(person, $"参与{town.Name}的{project.Name}研究，现已掌握成果。", entry,
                    PersonalExperienceKind.Learning);
            research.Replace(research.Value with { ActiveProject = null, Progress = 0, RequiredProgress = 0, Observation = new ProjectObservation() });
        }
    }

    /// <summary>查找符合居民职业及本地条件的劳动地点，返回是否找到目标。</summary>
    /// <param name="resident">劳动的居民。</param>
    /// <param name="x">找到的劳动地点横向地格坐标；失败时为居民当前位置。</param>
    /// <param name="y">找到的劳动地点纵向地格坐标；失败时为居民当前位置。</param>
    public bool TryGetLocalWorkTarget(Resident resident, out int x, out int y)
    {
        var building = FindLocalWorkBuilding(resident, 8, true);
        x = building?.X ?? resident.X;
        y = building?.Y ?? resident.Y;
        return building is not null;
    }

    private BuildingCursor? FindLocalWorkTarget(ResidentCursor resident)
    {
        return FindLocalWorkBuilding(resident, 8, true);
    }

    private int WorkPriority(BuildingCursor building, ResidentCursor resident, bool preferSpecialty)
    {
        if (preferSpecialty && PreferredExpansionJob(building.Kind) == resident.Profession)
            return 0;
        if (preferSpecialty && (!building.IsCompleted || building.IsUpgrading))
            return 3;
        if (!building.IsCompleted || building.IsUpgrading || (building.Kind == BuildingKind.TownCenter &&
                                                              RequireTown(building.SettlementId).IsExpanding))
            return 0;
        if (PreferredExpansionJob(building.Kind) == resident.Profession)
            return 1;
        if (building.Kind == BuildingKind.Reservoir && RequireTown(building.SettlementId).Resources.Water <
            RequireTown(building.SettlementId).Population)
            return 1;
        if (resident.Profession is Profession.Physician or Profession.Firefighter or Profession.Archivist
                or Profession.Surveyor or Profession.Gardener
            && preferSpecialty)
            return 6;
        if (building.Kind == BuildingKind.Armory &&
            resident.Profession is Profession.Soldier or Profession.Ranger)
            return 1;
        if (ProductionRules.For(building.Kind) is { } production)
        {
            if (building.ProductionBatches == 0)
                return 1;
            if (resident.Profession == Profession.Scholar && Current.Society.Research.Any(r =>
                    r.SettlementId == building.SettlementId && r.ActiveProject is not null))
                return 3;
            var stock = RequireTown(building.SettlementId).Resources.Get(production.Output);
            return stock < (production.Output == ResourceKind.Food ? 100 : 80) ? 1 : 4;
        }

        if (building.Kind is BuildingKind.Waystation or BuildingKind.SignalTower)
            return building.LastWorkedTick < Current.Tick - 6 ? 1 : 5;
        if ((resident.Profession is Profession.Mage or Profession.Battlemage ||
             resident.Agent.Goal.Kind == AgentGoalKind.TrainMagic) &&
            building.Kind == BuildingKind.ArcaneSanctum)
            return resident.MagicTraining < 8 ? 1 : 2;
        if ((resident.Profession == Profession.Scholar || resident.Agent.Goal.Kind == AgentGoalKind.Study) &&
            building.Kind == BuildingKind.Academy)
            return 1;
        if (building.Kind == BuildingKind.Academy && resident.Agent.Personality.Ambition > 0.55)
            return 2;
        if (building.Kind == BuildingKind.ArcaneSanctum && resident.MagicTalent >= 45)
            return 2;
        if (resident.Profession == Profession.Farmer && building.Kind == BuildingKind.Farm)
            return 2;
        if (resident.Profession is Profession.Lumberjack or Profession.Miner &&
            building.Kind == BuildingKind.Workshop)
            return 2;
        return 3;
    }

    private bool BuildingHasWork(BuildingCursor building, ResidentCursor resident)
    {
        if (!BuildingGroundOwned(building) || !building.Enabled || resident.Age < 14 || resident.ArmyId != 0 ||
            resident.Health <= 0)
            return false;
        if (Current.Tiles[Index(building.X, building.Y)].FireTicks > 0
            || !BuildingTerrainValid(building.Kind, Current.Tiles[Index(building.X, building.Y)]))
            return false;
        if (building.LastWorkedTick == Current.Tick && building.Workers.Count >= building.WorkSlots &&
            !building.Workers.Contains(resident.Id))
            return false;
        if (building.Health < 50)
        {
            return building.Health > 0 && resident.Profession is Profession.Builder or Profession.Engineer
                                           or Profession.Firefighter
                                       && (resident.Inventory.Stone >= .5 ||
                                           RequireTown(building.SettlementId).Resources.Stone >= .5);
        }

        if (!building.IsCompleted || building.IsUpgrading)
            return true;
        if (BuildingRace(building.Kind) is { } race && resident.Race != race)
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
            BuildingKind.Farm => resident.Agent.Goal.PlayerDirected ||
                                 RequireTown(building.SettlementId).Resources.Food <
                                 ProductionStockTarget(RequireTown(building.SettlementId), ResourceKind.Food) ||
                                 resident.Inventory.Food < TravelReserve(resident),
            BuildingKind.Workshop => (resident.Agent.Goal.PlayerDirected ||
                                      LocalMaterialsNeeded(resident, RequireTown(building.SettlementId))) &&
                                     FindWorkshopResource(building, resident.Profession) >= 0,
            BuildingKind.Academy => Current.Society.Research.Any(r =>
                r.SettlementId == building.SettlementId && r.ActiveProject is not null),
            BuildingKind.ArcaneSanctum => Current.Society.MagicEnabled && resident.MagicTalent >= 25 &&
                                          resident.MagicTraining < 100,
            BuildingKind.Infirmary => FindLocalWorkPatient(building, true) is not null,
            BuildingKind.MountainPass or BuildingKind.Bridge or BuildingKind.Granary or BuildingKind.Housing
                or BuildingKind.Watchtower => false,
            BuildingKind.TownCenter => RequireTown(building.SettlementId).IsExpanding,
            BuildingKind.LumberCamp => resident.Profession == Profession.Lumberjack &&
                                       (resident.Agent.Goal.PlayerDirected ||
                                        LocalMaterialsNeeded(resident, RequireTown(building.SettlementId))) &&
                                       FindWorkshopResource(building, Profession.Lumberjack) >= 0,
            BuildingKind.Quarry => resident.Profession == Profession.Miner &&
                                   (resident.Agent.Goal.PlayerDirected ||
                                    LocalMaterialsNeeded(resident, RequireTown(building.SettlementId))) &&
                                   FindWorkshopResource(building, Profession.Miner) >= 0,
            BuildingKind.Well => resident.Inventory.Water < WaterReserve(resident) + 3
                                 && WellWaterYield(Current.Tiles[Index(building.X, building.Y)]) > 0 &&
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
    private bool TryWorkAtBuilding(ResidentCursor resident)
    {
        var building = FindLocalWorkBuilding(resident, 1, false, true);
        if (building is null || !_settlements.TryGetValue(building.SettlementId, out var town))
            return false;
        if (!IsWorkDay(resident)) return true;
        if (building.IsCompleted && !building.IsUpgrading &&
            building.Kind is BuildingKind.Waystation or BuildingKind.SignalTower or BuildingKind.Dock
                or BuildingKind.Market && town.Resources.Food < 0.01)
            return false;
        if (building.IsCompleted && !building.IsUpgrading && building.Kind == BuildingKind.ArcaneSanctum &&
            (!Current.Society.MagicEnabled || town.Resources.Food < 0.03))
            return false;
        var production = ProductionRules.For(building.Kind);
        if (building.IsCompleted && !building.IsUpgrading && production is not null &&
            !HasProductionInputs(resident.Inventory, production))
            return false;
        if (building.LastWorkedTick != Current.Tick)
        {
            building.Workers.Clear();
            building.LastWorkedTick = Current.Tick;
        }

        if (building.Workers.Contains(resident.Id))
            return false;
        building.Workers.Add(resident.Id);
        if (building.Health < 50)
        {
            RepairBuilding(resident.Id, building.Id);
            return true;
        }

        var effort = WorkInterval(resident) * Math.Clamp((0.6 + resident.Agent.Personality.Diligence * 0.6) * LaborCondition(resident), 0.1,
            1.2);
        if ((!building.IsCompleted || building.IsUpgrading) && resident.Profession == Profession.Engineer
                                                            && HasResearch(town.Id,
                                                                Advancement.MechanicalEngineering) &&
                                                            resident.Inventory.Tools >= .05)
        {
            resident.Inventory = resident.Inventory with { Tools = resident.Inventory.Tools - .05 };
            effort *= 1.75;
        }

        if (building.IsUpgrading)
        {
            building.UpgradeProgress = Math.Min(building.UpgradeRequired,
                building.UpgradeProgress + effort * Current.Rules.DevelopmentRate);
            if (building.UpgradeProgress >= building.UpgradeRequired)
                FinishBuildingUpgrade(building);
            return true;
        }

        if (!building.IsCompleted)
        {
            building.Observation = building.Observation.AddContributor(resident.Id);
            building.ConstructionProgress = Math.Min(building.ConstructionRequired,
                building.ConstructionProgress + effort * Current.Rules.DevelopmentRate);
            if (building.IsCompleted)
            {
                var ground = Current.Tiles[Index(building.X, building.Y)];
                if (IsForestTerrain(ground.Terrain) && !PreserveBuildingForest(building.Kind))
                {
                    ground.Terrain = TerrainType.Grass;
                    ground.ResourceAmount = 0;
                }

                CompleteLandImprovement(building);
                EmitVisual(WorldVisualKind.Construction, building.X, building.Y);
                var complete = AddEvent(WorldEventKind.Construction, $"{town.Name}的{BuildingName(building.Kind)}竣工。",
                    building.X, building.Y,
                    EventAction.Completed, town.Id, causeEventId: building.Observation.StartEventId);
                foreach (var person in Current.Residents.Where(r => building.Observation.Contributors.Contains(r.Id)))
                    RecordLife(person, $"参与施工的{BuildingName(building.Kind)}竣工。", complete,
                        PersonalExperienceKind.Achievement);
            }

            return true;
        }

        if (production is not null)
            return Produce(building, resident, production);
        if (IsHusbandry(building.Kind))
            return WorkHusbandry(building, resident, effort * building.Efficiency);
        if (building.Kind >= BuildingKind.Reservoir)
            return WorkExpansionFacility(building, resident, town, effort * building.Efficiency);
        effort *= building.Efficiency * RaceTerrainRules
            .For(resident.Race, Current.Tiles[Index(building.X, building.Y)].Terrain).Productivity;
        if (BuildingRace(building.Kind) is not null)
            return WorkRacialBuilding(building, resident, effort);
        var culture = GetCulture(resident.CultureId);
        switch (building.Kind)
        {
            case BuildingKind.Farm:
                var tile = Current.Tiles[Index(building.X, building.Y)];
                var fertility = tile.Fertility / 100d * (tile.DroughtTicks > 0 ? 0.18 : 1) *
                                (tile.FireTicks > 0 ? 0 : 1);
                var harvest = Math.Min(1.25 * effort * fertility, 0.5 * effort * fertility *
                                                                  (1 + culture.NatureAffinity * 0.2) *
                                                                  (HasResearch(town.Id, Advancement.Agriculture)
                                                                      ? 1.35
                                                                      : 1)
                                                                  * (town.FertilityBoostTicks > 0 ? 1.35 : 1) *
                                                                  GetPolicyProductionMultiplier(town.Id) *
                                                                  (HasResearch(town.Id, Advancement.Irrigation)
                                                                      ? 1.25
                                                                      : 1));
                RecordHarvest(tile, harvest);
                resident.Inventory = resident.Inventory with { Food = Math.Min(1_000_000, resident.Inventory.Food + harvest) };
                return harvest > 0;
            case BuildingKind.Workshop:
            case BuildingKind.LumberCamp:
            case BuildingKind.Quarry:
                var source = FindWorkshopResource(building, resident.Profession);
                if (source < 0)
                    return false;
                var sourceTile = Current.Tiles[source];
                var yields = TerrainRules.For(sourceTile.Terrain);
                var desired = effort * 0.2 * Current.Rules.GatheringRate *
                              (HasResearch(town.Id, Advancement.Forestry) ? 1.25 : 1) *
                              GatheringTerritoryMultiplier(resident, sourceTile);
                var amount = resident.Profession == Profession.Miner
                    ? Math.Min(sourceTile.ResourceAmount, desired)
                    : HarvestPlants(sourceTile, desired * NaturalPlantHarvestEfficiency(sourceTile, true), true);
                if (resident.Profession == Profession.Miner)
                {
                    sourceTile.ResourceAmount -= amount;
                    resident.Inventory = resident.Inventory with
                    {
                        Stone = resident.Inventory.Stone + amount * yields.StoneYield,
                        Ore = resident.Inventory.Ore + amount * yields.OreYield,
                    };
                }
                else
                {
                    resident.Inventory = resident.Inventory with { Wood = resident.Inventory.Wood + amount * yields.WoodYield };
                    FinishLogging(sourceTile, source % Current.Width, source / Current.Width);
                }

                RecordHarvest(sourceTile,
                    amount * (resident.Profession == Profession.Miner
                        ? yields.StoneYield + yields.OreYield
                        : yields.WoodYield));
                resident.Inventory = resident.Inventory.Clamp(1_000_000);
                return true;
            case BuildingKind.Academy:
                var research = Current.Society.Research.First(r => r.SettlementId == town.Id);
                if (research.ActiveProject is null)
                    return false;
                research.Observation = research.Observation.AddContributor(resident.Id);
                research.Progress += effort * Current.Rules.DevelopmentRate * (1 + EffectiveSettlementRank(town) * .1) *
                                     (0.75 + culture.Innovation * 0.5) *
                                     (GetLocalPolicy(town.Id) == PolicyKind.Scholarship ? 1.35 : 1)
                                     * (HasResearch(town.Id, Advancement.ScientificMethod) ? 1.25 : 1) *
                                     (HasResearch(town.Id, Advancement.ArcaneScholarship) ? 1.25 : 1);
                if (research.Progress >= 8 && resident.Profession == Profession.Builder &&
                    !HasTwoLocalWorkers(town.Id, Profession.Scholar))
                    resident.Profession = Profession.Scholar;
                if (research.Progress >= research.RequiredProgress)
                {
                    var completed = research.ActiveProject;
                    GrantReceivedResearch(town.Id, completed, research.Observation.StartEventId);
                    var fact = new AgentFact
                    {
                        Id = NewId(),
                        EventId = research.LastCompletionEventId,
                        Kind = AgentFactKind.Research,
                        SubjectId = town.Id,
                        X = town.X,
                        Y = town.Y,
                        Value = completed.Id,
                        ObservedTick = Current.Tick,
                        LearnedTick = Current.Tick,
                        OriginResidentId = resident.Id,
                        OriginProfession = resident.Profession,
                        SourceResidentId = resident.Id,
                        Text = $"{town.Name}已完成{completed.Name}研究",
                    };
                    AddPublicFact(town, fact);
                    AddResidentFact(resident, fact);
                }

                return true;
            case BuildingKind.Waystation:
            case BuildingKind.SignalTower:
            case BuildingKind.Dock:
            case BuildingKind.Market:
                if (town.Resources.Food < 0.01)
                    return false;
                town.Resources = town.Resources with { Food = town.Resources.Food - 0.01 };
                return true;
            case BuildingKind.ArcaneSanctum:
                if (!Current.Society.MagicEnabled || town.Resources.Food < 0.03)
                    return false;
                town.Resources = town.Resources with { Food = town.Resources.Food - 0.03 };
                resident.MagicTraining = Math.Min(100,
                    resident.MagicTraining + effort * Current.Rules.MagicRate * (0.05 + resident.MagicTalent / 500) *
                    TerrainRules.For(Current.Tiles[Index(resident.X, resident.Y)].Terrain).ManaRate *
                    (HasResearch(town.Id, Advancement.ArcaneScholarship) ? 1.5 : 1));
                if (resident.MagicTraining >= 8 && resident.Profession is Profession.Builder or Profession.Scholar &&
                    !HasTwoLocalWorkers(town.Id, Profession.Mage))
                    resident.Profession = Profession.Mage;
                resident.Mana = Math.Min(100,
                    resident.Mana + 0.15 * effort * (HasResearch(town.Id, Advancement.ManaAttunement) ? 1.5 : 1));
                return true;
            case BuildingKind.Infirmary:
                if (town.Resources.Food < 0.05)
                    return false;
                var patient = FindLocalWorkPatient(building);
                if (patient is null)
                    return false;
                town.Resources = town.Resources with { Food = town.Resources.Food - 0.05 };
                patient.Health = Math.Min(100,
                    patient.Health + 0.45 * effort * (HasResearch(town.Id, Advancement.Medicine) ? 1.5 : 1));
                patient.SicknessTicks = Math.Max(0, patient.SicknessTicks - 1);
                return true;
            case BuildingKind.TownCenter:
                return WorkOnTownExpansion(town, effort / building.Efficiency);
            case BuildingKind.Well:
                return resident.X == building.X && resident.Y == building.Y && DrawWater(resident,
                    Index(building.X, building.Y),
                    Math.Min(1, effort) *
                    GatheringTerritoryMultiplier(resident, Current.Tiles[Index(building.X, building.Y)])) > 0;
            default:
                return false;
        }
    }

    private int FindWorkshopResource(BuildingCursor building, Profession profession)
    {
        var best = -1;
        var bestYield = 0d;
        foreach (var index in Circle(building.X, building.Y, 1))
        {
            var tile = Current.Tiles[index];
            if (tile.ResourceAmount < .5 || tile.FireTicks > 0)
                continue;
            if (profession != Profession.Miner && NaturalPlantHarvestEfficiency(tile, true) < .25)
                continue;
            var yield = TerrainRules.For(tile.Terrain);
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
        var tile = Current.Tiles[Index(x, y)];
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
        foreach (var town in Current.Settlements)
            if (town.NationId == nationId && Distance(x, y, town.X, town.Y) <= 3)
                bonus = Math.Max(bonus, 1 + EffectiveSettlementRank(town) * .15);
        foreach (var building in Current.Society.Buildings)
            if (building.Kind == BuildingKind.Waystation && IsFacilityOperating(building)
                                                         && _settlements.TryGetValue(building.SettlementId,
                                                             out var town) && town.NationId == nationId &&
                                                         Distance(x, y, building.X, building.Y) <= 3)
                bonus = Math.Max(bonus, 1.25 + (building.Level - 1) * .15);
        speed *= bonus;
        return speed;
    }

    /// <summary>检查两处同国聚落之间是否有可用信号塔路径，并给出递送日数。</summary>
    /// <param name="fromSettlementId">信息递送出发聚落的 ID。</param>
    /// <param name="toSettlementId">信息递送目标聚落的 ID。</param>
    /// <param name="travelTicks">可用路径所需的模拟日数；没有路径时为零。</param>
    public bool CanRelayInformation(int fromSettlementId, int toSettlementId, out int travelTicks)
    {
        travelTicks = 0;
        if (!_settlements.TryGetValue(fromSettlementId, out var from) ||
            !_settlements.TryGetValue(toSettlementId, out var to) || from.NationId != to.NationId)
            return false;
        if (!Current.Society.Buildings.Any(b => b.Kind == BuildingKind.SignalTower && b.IsCompleted))
            return false;
        var towers = Current.Society.Buildings.Where(b => b.Kind == BuildingKind.SignalTower && IsFacilityOperating(b)
                && HasResearch(b.SettlementId, Advancement.SignalNetwork) &&
                HasResearch(b.SettlementId, Advancement.Electrification) &&
                _settlements.TryGetValue(b.SettlementId, out var town) && town.NationId == from.NationId)
            .OrderBy(b => b.Id)
            .ToArray();
        var queue = new Queue<(BuildingCursor Tower, int Hops)>();
        var visited = new HashSet<int>();
        foreach (var tower in towers.Where(t =>
                     Distance(t.X, t.Y, from.X, from.Y) <= 12 + (t.Level - 1) * 4 &&
                     ClearSignalLine(t.X, t.Y, from.X, from.Y)))
        {
            queue.Enqueue((tower, 1));
            visited.Add(tower.Id);
        }

        while (queue.TryDequeue(out var node))
        {
            if (Distance(node.Tower.X, node.Tower.Y, to.X, to.Y) <= 12 + (node.Tower.Level - 1) * 4 &&
                ClearSignalLine(node.Tower.X, node.Tower.Y, to.X, to.Y))
            {
                travelTicks = node.Hops * 2;
                return true;
            }

            foreach (var tower in towers)
                if (!visited.Contains(tower.Id) &&
                    Distance(tower.X, tower.Y, node.Tower.X, node.Tower.Y) <=
                    24 + (Math.Min(tower.Level, node.Tower.Level) - 1) * 8 &&
                    ClearSignalLine(tower.X, tower.Y, node.Tower.X, node.Tower.Y))
                {
                    visited.Add(tower.Id);
                    queue.Enqueue((tower, node.Hops + 1));
                }
        }

        return false;
    }

    private bool IsFacilityOperating(BuildingCursor building)
    {
        return BuildingGroundOwned(building) && building.Enabled && building.IsCompleted && !building.IsUpgrading &&
               building.Health >= 50 && BuildingTerrainValid(building.Kind, Current.Tiles[Index(building.X, building.Y)])
               && Current.Tiles[Index(building.X, building.Y)].FireTicks == 0 && (PassiveFacility(building) ||
                   building.Kind is BuildingKind.MountainPass or BuildingKind.Bridge or BuildingKind.TownCenter
                       or BuildingKind.Well or BuildingKind.Waygate || (building.LastWorkedTick >= Current.Tick - 12
                                                                        && Current.Residents.Any(r =>
                                                                            building.Workers.Contains(r.Id) &&
                                                                            r.SettlementId == building.SettlementId &&
                                                                            r.Health > 0 &&
                                                                            (BuildingRace(building.Kind) is not
                                                                            { } race ||
                                                                             (r.Race == race && r.Age >= 14)) &&
                                                                            Distance(r.X, r.Y, building.X,
                                                                                building.Y) <= 1)));
    }

    private bool ClearSignalLine(int x0, int y0, int x1, int y1)
    {
        var steps = Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0));
        for (var i = 1; i < steps; i++)
        {
            var x = x0 + (int)Math.Round((x1 - x0) * i / (double)steps);
            var y = y0 + (int)Math.Round((y1 - y0) * i / (double)steps);
            if (Current.Tiles[Index(x, y)].Terrain == TerrainType.Mountain)
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
        Current.Society.Institutions.First(i => i.NationId == nationId).Kind = kind;
    }

    /// <summary>为国家指定政策，并安排其在相关聚落生效。</summary>
    /// <param name="nationId">国家 ID。</param>
    /// <param name="policy">待应用的政策。</param>
    public void SetPolicy(int nationId, PolicyKind policy)
    {
        if (!Enum.IsDefined(policy))
            throw new ArgumentOutOfRangeException(nameof(policy));
        var nation = RequireNation(nationId);
        Current.Society.Institutions.First(i => i.NationId == nationId).PlayerPolicy = policy;
        foreach (var town in Current.Settlements.Where(s => s.NationId == nationId))
        {
            var local = Current.Society.Policies.First(p => p.SettlementId == town.Id);
            local.Replace(local.Value with { Kind = policy, PlayerOverride = true, DecidedTick = Current.Tick, Reason = "玩家直接设定；恢复自治前保持此政策" });
        }

        AddEvent(WorldEventKind.Editor, $"{nation.Name}的政策由玩家调整为{PolicyName(policy)}。");
    }

    /// <summary>清除国家和本地的玩家政策覆盖，恢复机构自主选择。</summary>
    /// <param name="nationId">国家 ID。</param>
    public void SetPolicyAutonomy(int nationId)
    {
        _ = RequireNation(nationId);
        Current.Society.Institutions.First(i => i.NationId == nationId).PlayerPolicy = null;
        foreach (var town in Current.Settlements.Where(s => s.NationId == nationId))
            Current.Society.Policies.First(p => p.SettlementId == town.Id).PlayerOverride = false;
    }

    /// <summary>查询聚落当前采用的政策，没有记录时返回均衡政策。</summary>
    /// <param name="settlementId">聚落 ID。</param>
    public PolicyKind GetLocalPolicy(int settlementId)
    {
        return Current.Society.Policies.FirstOrDefault(p => p.SettlementId == settlementId)?.Kind ?? PolicyKind.Balanced;
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
        var local = Current.Society.Policies.First(p => p.SettlementId == settlementId);
        if (local.PlayerOverride)
            return;
        local.Replace(local.Value with { Kind = policy, DecidedTick = Current.Tick, Reason = "代表已收到递送的政策指令" });
    }

    private void ReceiveSocietyReport(SettlementCursor target, ResidentCursor carrier, AgentFact fact)
    {
        if (Distance(carrier.X, carrier.Y, target.X, target.Y) > 2 || fact.ObservedTick > Current.Tick ||
            fact.Confidence is < 0 or > 1 || !double.IsFinite(fact.Value))
            return;
        fact.Topic.Receive(this, target, carrier, fact);
        if (!fact.Topic.CreatesInstitutionReport)
            return;
        if (_knowledgeQueriesActive ? !_institutionReports.Add((target.Id, fact.Id))
            : Current.Society.Reports.Any(r => r.RecipientSettlementId == target.Id && r.FactId == fact.Id))
            return;
        Current.Society.Reports.Add(new InstitutionReport
        {
            EventId = fact.EventId,
            RecipientSettlementId = target.Id,
            FactId = fact.Id,
            OriginResidentId = fact.OriginResidentId,
            RepresentativeId = carrier.Id,
            ReportedProfession = fact.OriginProfession,
            Topic = fact.Kind,
            SubjectId = fact.SubjectId,
            Value = fact.Value,
            Confidence = fact.Confidence,
            ObservedTick = fact.ObservedTick,
            ReceivedTick = Current.Tick,
        });
        if (Current.Society.Reports.Count > 2_048)
        {
            var remove = Current.Society.Reports.Count - 2_048;
            if (_knowledgeQueriesActive)
                for (var index = 0; index < remove; index++)
                {
                    var old = Current.Society.Reports[index];
                    _institutionReports.Remove((old.RecipientSettlementId, old.FactId));
                }
            Current.Society.Reports.RemoveRange(0, Current.Society.Reports.Count - 2_048);
        }
    }

    internal void ReceiveResearchFact(SettlementCursor target, AgentFact fact)
    {
        if (fact.Confidence >= 0.5 && fact.Value == Math.Truncate(fact.Value) &&
            Advancement.Find((int)fact.Value) is { } receivedResearch)
            GrantReceivedResearch(target.Id, receivedResearch, fact.EventId, fact.Id);
    }

    internal void ReceivePolicyFact(SettlementCursor target, AgentFact fact)
    {
        if (fact.Confidence < 0.5 || Current.Tick - fact.ObservedTick > 240 ||
            fact.Value is < 0 or > 4 || fact.Value != Math.Truncate(fact.Value))
            return;
        var policy = Current.Society.Policies.First(p => p.SettlementId == target.Id);
        if (fact.ObservedTick < policy.EvidenceObservedTick)
            return;
        ApplyReceivedPolicy(target.Id, (PolicyKind)(int)fact.Value);
        if (!policy.PlayerOverride)
        {
            policy.Replace(policy.Value with { EvidenceFactId = fact.Id, EvidenceObservedTick = fact.ObservedTick });
        }
    }

    internal void ReceiveCultureFact(ResidentCursor carrier, AgentFact fact)
    {
        if (fact.Confidence >= 0.5 && fact.Value is > 0 and <= 100_000 &&
            fact.Value == Math.Truncate(fact.Value))
            ObserveCulture(carrier, (int)fact.Value);
    }

    private void DecideLocalPolicy(SettlementCursor town)
    {
        var institution = Current.Society.Institutions.First(i => i.NationId == town.NationId);
        var local = Current.Society.Policies.First(p => p.SettlementId == town.Id);
        if (institution.PlayerPolicy.HasValue)
        {
            local.Replace(local.Value with { Kind = institution.PlayerPolicy.Value, PlayerOverride = true });
            return;
        }

        if (local.PlayerOverride)
            return;
        var reports = Current.Society.Reports
            .Where(r => r.RecipientSettlementId == town.Id && r.ObservedTick <= Current.Tick)
            .GroupBy(r => (r.OriginResidentId, r.Topic)).Select(g =>
                g.OrderByDescending(r => r.ObservedTick).ThenByDescending(r => r.ReceivedTick).First()).ToArray();
        if (reports.Length == 0)
            return;
        var scores = new double[5];
        var evidence = new InstitutionReport?[5];
        var largest = new double[5];
        foreach (var report in reports)
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
                                            Current.Nations.First(n => n.Id == town.NationId).RepresentativeId ? 5 :
                    report.ReportedProfession == Profession.Soldier ? 2.5 : 0.6,
                InstitutionKind.GuildCouncil => report.ReportedProfession is Profession.Lumberjack or Profession.Miner
                    or Profession.Builder
                    ? 2.8
                    : 0.8,
                _ => 1 + GetCulture(town.CultureId).Cooperation * 0.35,
            };
            var score = urgency * report.Confidence * relevance * authority /
                        (1 + (Current.Tick - report.ObservedTick) / 180d);
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
            return;
        var changed = local.Kind != (PolicyKind)choice;
        local.Replace(local.Value with { Kind = (PolicyKind)choice, DecidedTick = Current.Tick, EvidenceFactId = strongest.FactId, EvidenceObservedTick = strongest.ObservedTick });
        local.Reason =
            $"依据代表 {strongest.RepresentativeId} 送达的议题（观察于 {strongest.ObservedTick}，接收于 {strongest.ReceivedTick}），制度与职业相关权重合计 {scores[choice]:0.0}，采用{PolicyName(local.Kind)}";
        institution.Replace(institution.Value with { LastDecision = local.Reason, LastDecisionTick = Current.Tick });
        if (town.Id == Current.Nations.First(n => n.Id == town.NationId).CapitalId)
            Current.Nations.First(n => n.Id == town.NationId).Decision = local.Reason;
        if (changed)
        {
            var policyEvent = AddEvent(WorldEventKind.Policy, $"{town.Name}议事决定采用{PolicyName(local.Kind)}。", town.X,
                town.Y, EventAction.Policy, town.Id, causeEventId: strongest.EventId, evidenceFactId: strongest.FactId);
            AddPublicFact(town, new AgentFact
            {
                Id = NewId(),
                EventId = policyEvent.Id,
                Kind = AgentFactKind.Policy,
                SubjectId = town.Id,
                X = town.X,
                Y = town.Y,
                Value = (int)local.Kind,
                ObservedTick = Current.Tick,
                LearnedTick = Current.Tick,
                OriginResidentId = town.RepresentativeId,
                OriginProfession = Profession.Representative,
                SourceResidentId = town.RepresentativeId,
                Text = local.Reason,
            });
        }
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
        Current.Society.Cultures[Current.Society.Cultures.IndexOf(culture)] = culture with { Name = name };
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
        Current.Society.Cultures[Current.Society.Cultures.IndexOf(culture)] = culture with
        {
            Cooperation = cooperation,
            Innovation = innovation,
            NatureAffinity = natureAffinity,
        };
    }

    /// <summary>指定国家的文化归属。</summary>
    /// <param name="nationId">国家 ID。</param>
    /// <param name="cultureId">文化定义的稳定 ID。</param>
    public void SetNationCulture(int nationId, int cultureId)
    {
        _ = RequireCulture(cultureId);
        RequireNation(nationId).CultureId = cultureId;
    }

    /// <summary>指定居民的文化归属。</summary>
    /// <param name="residentId">居民 ID。</param>
    /// <param name="cultureId">文化定义的稳定 ID。</param>
    public void SetResidentCulture(int residentId, int cultureId)
    {
        _ = RequireCulture(cultureId);
        var resident = Current.Residents.FirstOrDefault(r => r.Id == residentId) ?? throw new ArgumentException("居民不存在。");
        resident.CultureId = cultureId;
    }

    /// <summary>记录两位居民实际接触对彼此文化认知的影响。</summary>
    /// <param name="first">实际参与交流的第一位居民。</param>
    /// <param name="second">实际参与交流的第二位居民。</param>
    public void ExchangeCulture(Resident first, Resident second)

    {
        ExchangeCulture(RequireResident(first.Id), RequireResident(second.Id));
    }
    private void ExchangeCulture(ResidentCursor first, ResidentCursor second)
    {
        if (Distance(first.X, first.Y, second.X, second.Y) > 2 || first.CultureId == second.CultureId)
            return;
        var firstCulture = first.CultureId;
        ObserveCulture(first, second.CultureId);
        ObserveCulture(second, firstCulture);
    }

    private void ObserveCulture(ResidentCursor resident, int cultureId)
    {
        if (resident.CultureId == cultureId || !Current.Society.Cultures.Any(c => c.Id == cultureId))
            return;
        var contact =
            Current.Society.CulturalContacts.FirstOrDefault(c => c.ResidentId == resident.Id && c.CultureId == cultureId);
        if (contact is null)
        {
            contact = new CulturalContactCursor { ResidentId = resident.Id, CultureId = cultureId, LastContactTick = -12 };
            Current.Society.CulturalContacts.Add(contact);
        }

        if (Current.Tick - contact.LastContactTick < 12)
            return;
        contact.LastContactTick = Current.Tick;
        contact.Exposure += 0.5 + resident.Agent.Personality.Sociability;
        if (contact.Exposure < 10)
            return;
        var previous = GetCulture(resident.CultureId).Name;
        resident.CultureId = cultureId;
        // 文化归属变化后须重新积累持续接触，避免连续快速转化。
        foreach (var exposure in Current.Society.CulturalContacts.Where(c => c.ResidentId == resident.Id))
        {
            exposure.Replace(exposure.Value with { Exposure = 0, LastContactTick = Current.Tick });
        }

        RecordLife(resident, $"长期当面交流后，由{previous}转向{GetCulture(cultureId).Name}文化；种族与国籍未改变。");
        if (resident.History.Count > 24)
            resident.History.RemoveAt(0);
        var cultureEvent = AddEvent(WorldEventKind.Culture, $"{resident.Name}经长期交流转向{GetCulture(cultureId).Name}文化。",
            resident.X, resident.Y);
        cultureEvent.Replace(cultureEvent.Value with { ResidentId = resident.Id, NationId = resident.NationId });
    }

    /// <summary>尝试按正式施法命令执行法术；条件不满足时返回失败。</summary>
    /// <param name="casterId">施法居民的稳定 ID。</param>
    /// <param name="spell">法术类别。</param>
    /// <param name="x">横向地格坐标。</param>
    /// <param name="y">纵向地格坐标。</param>
    public bool TryCastSpell(int casterId, SpellKind spell, int x, int y)
    {
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
        var caster = Current.Residents.FirstOrDefault(r => r.Id == casterId) ?? throw new ArgumentException("施法居民不存在。");
        if (!InBounds(x, y) || Distance(caster.X, caster.Y, x, y) > 4)
            throw new InvalidOperationException("目标须位于施法者 4 格以内。");
        if (caster.Health <= 0 || caster.Age < 14 || caster.MagicTalent < 25 || caster.MagicTraining < 8)
            throw new InvalidOperationException("需要成年、魔法天赋至少 25 且完成至少 8 点奥术训练。");
        if (SpellUnlockError(casterId, spell) is { } unlockError)
            throw new InvalidOperationException(unlockError);
        var cost = SpellManaCost(spell);
        cost *= (caster.Race == RaceKind.Elf && spell == SpellKind.Heal) ||
                (caster.Race == RaceKind.Dwarf && spell == SpellKind.Shield) ||
                (caster.Race == RaceKind.Orc && spell == SpellKind.Ember)
            ? 0.85
            : 1;
        if (caster.Mana < cost)
            throw new InvalidOperationException($"法力不足：需要 {cost:0.#}，当前 {caster.Mana:0.#}。");
        ResidentCursor? recipient = null;
        SettlementCursor? town = null;
        switch (spell)
        {
            case SpellKind.Heal:
                recipient = Current.Residents
                    .Where(r => r.NationId == caster.NationId && Distance(r.X, r.Y, x, y) <= 1 &&
                                (r.Health < 100 || r.SicknessTicks > 0)).OrderBy(r => r.Health).ThenBy(r => r.Id)
                    .FirstOrDefault();
                if (recipient is null)
                    throw new InvalidOperationException("目标附近没有需要治疗的本国居民。");
                break;
            case SpellKind.HarvestBlessing:
            case SpellKind.Shield:
                town = Current.Settlements.Where(s => s.NationId == caster.NationId && Distance(s.X, s.Y, x, y) <= 3)
                    .OrderBy(s => Distance(s.X, s.Y, x, y)).FirstOrDefault();
                if (town is null)
                    throw new InvalidOperationException("目标附近没有可施加结界或丰饶祝福的本国聚落。");
                break;
            case SpellKind.Ember:
            case SpellKind.FrostBolt:
            case SpellKind.ChainLightning:
                recipient = Current.Residents
                    .Where(r => r.NationId != caster.NationId && Distance(r.X, r.Y, x, y) <= 1 &&
                                IsKnownHostile(caster, r.NationId)).OrderBy(r => r.Id).FirstOrDefault();
                if (recipient is null)
                    throw new InvalidOperationException("目标附近没有正在交战的敌方居民。");
                if (!ClearSignalLine(caster.X, caster.Y, recipient.X, recipient.Y))
                    throw new InvalidOperationException("山体遮挡了施法视线");
                break;
            case SpellKind.RainCall:
                if (!Circle(x, y, 2).Any(i => Current.Tiles[i].DroughtTicks > 0 || Current.Tiles[i].FireTicks > 0))
                    throw new InvalidOperationException("目标附近没有干旱或火灾");
                break;
            case SpellKind.RuneWard:
                recipient = Current.Residents.Where(r =>
                    r.NationId == caster.NationId && r.Health > 0 && r.PersonalWard < 30
                    && Distance(r.X, r.Y, x, y) <= 1).OrderBy(r => r.PersonalWard).ThenBy(r => r.Id).FirstOrDefault();
                if (recipient is null)
                    throw new InvalidOperationException("目标附近没有需要个人结界的本国居民");
                break;
        }

        caster.Mana -= cost;
        var power = 0.7 + caster.MagicTalent / 150 + caster.MagicTraining / 250;
        if (spell == SpellKind.Heal)
        {
            recipient!.Health = Math.Min(100,
                recipient.Health + 22 * power * (HasResearch(caster.SettlementId, Advancement.Restoration) ? 1.5 : 1));
            recipient.SicknessTicks = Math.Max(0, recipient.SicknessTicks - 15);
        }

        if (spell == SpellKind.HarvestBlessing)
            town!.FertilityBoostTicks = Math.Max(town.FertilityBoostTicks, (int)(50 * power));
        if (spell == SpellKind.Shield)
            town!.ShieldTicks = Math.Max(town.ShieldTicks, (int)(40 * power));
        if (spell == SpellKind.Ember)
            DamageResident(recipient!, TryAbsorbShieldDamage(recipient!, 18 * power), DeathCause.Magic);
        if (spell == SpellKind.FrostBolt)
        {
            DamageResident(recipient!, TryAbsorbShieldDamage(recipient!, 10 * power * Current.Rules.CombatDamageRate),
                DeathCause.Magic);
            recipient!.FrozenUntilTick = Math.Max(recipient.FrozenUntilTick, Current.Tick + 6);
        }

        if (spell == SpellKind.ChainLightning)
        {
            foreach (var enemy in Current.Residents.Where(r => r.Health > 0 && r.NationId != caster.NationId
                                                                          && Distance(r.X, r.Y, recipient!.X,
                                                                              recipient.Y) <= 2 &&
                                                                          Distance(caster.X, caster.Y, r.X, r.Y) <= 4
                                                                          && IsKnownHostile(caster, r.NationId) &&
                                                                          ClearSignalLine(caster.X, caster.Y, r.X, r.Y))
                         .OrderBy(r => r.Id).Take(3))
                DamageResident(enemy, TryAbsorbShieldDamage(enemy, 14 * power * Current.Rules.CombatDamageRate),
                    DeathCause.Magic);
        }

        if (spell == SpellKind.RuneWard)
            recipient!.PersonalWard = Math.Max(recipient.PersonalWard, Math.Min(60, 30 * power));
        if (spell == SpellKind.RainCall)
        {
            foreach (var index in Circle(x, y, 2))
            {
                var tile = Current.Tiles[index];
                tile.DroughtTicks = Math.Max(0, tile.DroughtTicks - 60);
                if (tile.DroughtTicks == 0)
                    _dryTiles.Remove(index);
                if (tile.FireTicks > 0)
                {
                    tile.FireTicks = Math.Max(0, tile.FireTicks - 8);
                    if (tile.FireTicks == 0)
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
        }, x, y, 2, caster.X, caster.Y);
        var detail = SpellName(spell);
        caster.Agent.Decisions.Add(new AgentDecision
        {
            Tick = Current.Tick,
            Goal = spell == SpellKind.Ember ? AgentGoalKind.Flee : AgentGoalKind.Work,
            Reason = $"在 {x},{y} 施放{detail}，消耗 {cost:0.#} 法力；天赋和训练决定效果",
        });
        if (caster.Agent.Decisions.Count > 6)
            caster.Agent.Decisions.RemoveAt(0);
        var magicEvent = AddEvent(WorldEventKind.Magic, $"{caster.Name}施放{detail}，消耗 {cost:0.#} 法力。", x, y);
        magicEvent.Replace(magicEvent.Value with { ResidentId = caster.Id, NationId = caster.NationId });
    }

    /// <summary>按附近防御政策及护盾减伤，再消耗个人符文护甲和护甲，返回剩余伤害。</summary>
    /// <param name="resident">实际承受伤害并消耗个人防护的居民。</param>
    /// <param name="damage">减伤前的伤害量。</param>
    public double TryAbsorbShieldDamage(Resident resident, double damage)

    {
        return TryAbsorbShieldDamage(RequireResident(resident.Id), damage);
    }
    private double TryAbsorbShieldDamage(ResidentCursor resident, double damage)
    {
        var multiplier = 1d;
        foreach (var town in Current.Settlements)
        {
            if (town.NationId != resident.NationId || Distance(town.X, town.Y, resident.X, resident.Y) > 5)
                continue;
            var protection = (GetLocalPolicy(town.Id) == PolicyKind.Defense ? 0.88 : 1) *
                             (town.ShieldTicks > 0 ? 0.6 : 1);
            multiplier = Math.Min(multiplier, protection);
        }

        var remaining = Math.Max(0, damage * multiplier);
        var ward = Math.Min(resident.PersonalWard, remaining);
        resident.PersonalWard -= ward;
        remaining -= ward;
        var armor = Math.Min(resident.Armor, remaining * .35);
        resident.Armor -= armor;
        return remaining - armor;
    }

    /// <summary>在实体或地形编辑后修复城镇中心和连通占领，清理失效的社会归属记录。</summary>
    public void ReconcileSocietyTopology()
    {
        EnsureTownCenters();
        ReconcileConnectedClaims();
        var townIds = Current.Settlements.Select(s => s.Id).ToHashSet();
        var nationIds = Current.Nations.Select(n => n.Id).ToHashSet();
        Current.Society.Buildings.RemoveAll(b =>
            !townIds.Contains(b.SettlementId) || !BuildingTerrainValid(b.Kind, Current.Tiles[Index(b.X, b.Y)]) ||
            (b.Health <= 0 && b.Kind != BuildingKind.TownCenter));
        Current.Society.Research.RemoveAll(r => !townIds.Contains(r.SettlementId));
        Current.Society.Policies.RemoveAll(p => !townIds.Contains(p.SettlementId));
        Current.Society.Institutions.RemoveAll(i => !nationIds.Contains(i.NationId));
        Current.Society.Reports.RemoveAll(r => !townIds.Contains(r.RecipientSettlementId));
        var people = Current.Residents.Select(r => r.Id).ToHashSet();
        Current.Society.CulturalContacts.RemoveAll(c => !people.Contains(c.ResidentId));
        foreach (var building in Current.Society.Buildings)
            building.Workers.RemoveAll(id => !people.Contains(id));
    }

    /// <summary>推进当前模拟日的社会发展。</summary>
    public void TickSociety()
    {
        if (Current.Tick % 30 == 0)
        {
            RefreshLocalRepresentatives();
            BalanceLocalWorkforce();
        }

        var townIds = Current.Settlements.Select(s => s.Id).ToHashSet();
        var nationIds = Current.Nations.Select(n => n.Id).ToHashSet();
        Current.Society.Research.RemoveAll(r => !townIds.Contains(r.SettlementId));
        Current.Society.Policies.RemoveAll(p => !townIds.Contains(p.SettlementId));
        Current.Society.Institutions.RemoveAll(i => !nationIds.Contains(i.NationId));
        Current.Society.Reports.RemoveAll(r =>
            !townIds.Contains(r.RecipientSettlementId) || Current.Tick - r.ReceivedTick > 1_440);
        var liveResidents = Current.Residents.Select(r => r.Id).ToHashSet();
        Current.Society.CulturalContacts.RemoveAll(c => !liveResidents.Contains(c.ResidentId));
        foreach (var building in Current.Society.Buildings)
        {
            if (!InBounds(building.X, building.Y) || !townIds.Contains(building.SettlementId))
            {
                building.Health = 0;
                continue;
            }

            var tile = Current.Tiles[Index(building.X, building.Y)];
            if (!BuildingTerrainValid(building.Kind, tile))
                building.Health = 0;
            else if (tile.FireTicks > 0)
                building.Health = Math.Max(0, building.Health - 1.5 * BuildingFlammability(building));
            if (IsHusbandry(building.Kind) && Current.Tick - building.LastServiceTick > 30 &&
                (Current.Tick + building.Id) % 6 == 0)
            {
                building.LivestockPopulation *= .98;
                if (building.LivestockPopulation < .01)
                {
                    building.Replace(building.Value with { LivestockPopulation = 0, LivestockKind = WildlifeKind.None });
                }
            }

            building.Workers.RemoveAll(id => !liveResidents.Contains(id));
        }

        var crossingCollapsed = RemoveFailedCrossings();
        Current.Society.Buildings.RemoveAll(b => b.Health <= 0 && b.Kind != BuildingKind.TownCenter);
        if (crossingCollapsed)
            RelocateInvalidEntities();
        EnsureTownCenters();
        foreach (var town in Current.Settlements)
        {
            if (town.FertilityBoostTicks > 0)
                town.FertilityBoostTicks--;
            if (town.ShieldTicks > 0)
                town.ShieldTicks--;
            if (Current.Tick % 30 == 0)
                DecideLocalPolicy(town);
            if ((Current.Tick + town.Id) % 60 == 0)
                PlanLocalDevelopment(town);
            if (GetLocalPolicy(town.Id) == PolicyKind.PublicHealth && town.Resources.Food >= 0.02)
            {
                var patient = _citizens.GetValueOrDefault(town.Id)
                    ?.Where(r => Distance(r.X, r.Y, town.X, town.Y) <= 2 && r.Health < 99).OrderBy(r => r.Health)
                    .FirstOrDefault();
                if (patient is not null)
                {
                    town.Resources = town.Resources with { Food = town.Resources.Food - 0.02 };
                    patient.Health = Math.Min(100, patient.Health + 0.15);
                }
            }
        }

        foreach (var person in Current.Residents)
        {
            if (!InBounds(person.X, person.Y) || person.Health <= 0)
                continue;
            if (person.MagicTalent >= 25 && person.MagicTraining >= 8 && (Current.Tick + person.Id) % 12 == 0)
                TryAutomaticMagic(person);
        }

        // 按模拟日序分摊资源恢复。
        const int batch = 128;
        for (var offset = 0; offset < Math.Min(batch, Current.Tiles.Count); offset++)
        {
            var tile = Current.Tiles[(int)((Current.Tick * batch + offset) % Current.Tiles.Count)];
            if (!Current.Rules.ResourceRegeneration || !tile.IsWalkable || tile.FireTicks > 0)
                continue;
            var yields = TerrainRules.For(tile.Terrain);
            var renewal = (yields.FoodYield + yields.WoodYield) * (tile.DroughtTicks > 0 ? 0.2 : 1);
            var capacity = NaturalResourceCapacity(tile);
            if (tile.ResourceAmount < capacity)
                tile.ResourceAmount = Math.Min(capacity, tile.ResourceAmount + renewal * 2);
        }
    }

    private void TryAutomaticMagic(ResidentCursor person)
    {
        if (person.Agent.Goal.Kind is AgentGoalKind.DeliverMessage or AgentGoalKind.Trade ||
            person.Agent.Goal.PlayerDirected)
            return;
        var patient = Current.Residents
            .Where(r => r.NationId == person.NationId && r.Health < 60 && Distance(person.X, person.Y, r.X, r.Y) <= 3)
            .OrderBy(r => r.Health).ThenBy(r => r.Id).FirstOrDefault();
        if (patient is not null && (person.Agent.Personality.Sociability >= 0.3 || patient.Id == person.Id) &&
            TryCastSpell(person.Id, SpellKind.Heal, patient.X, patient.Y))
            return;
        if (person.ArmyId != 0 && person.Agent.Personality.Courage >= 0.35)
        {
            var enemy = Current.Residents.FirstOrDefault(r =>
                r.NationId != person.NationId && r.Health > 0 && Distance(person.X, person.Y, r.X, r.Y) <= 3 &&
                IsKnownHostile(person, r.NationId));
            if (enemy is not null)
            {
                if (person.Profession == Profession.Battlemage &&
                    TryCastSpell(person.Id, SpellKind.ChainLightning, enemy.X, enemy.Y))
                    return;
                if (HasResearch(person.SettlementId, Advancement.Elementalism) && enemy.FrozenUntilTick <= Current.Tick
                                                                               && TryCastSpell(person.Id,
                                                                                   SpellKind.FrostBolt, enemy.X,
                                                                                   enemy.Y))
                    return;
                if (TryCastSpell(person.Id, SpellKind.Ember, enemy.X, enemy.Y))
                    return;
            }
        }

        if (!_settlements.TryGetValue(person.SettlementId, out var town) ||
            Distance(person.X, person.Y, town.X, town.Y) > 4)
            return;
        if (GetLocalPolicy(town.Id) == PolicyKind.Defense && town.ShieldTicks < 6 &&
            TryCastSpell(person.Id, SpellKind.Shield, town.X, town.Y))
            return;
        if ((person.Profession is Profession.Farmer or Profession.Mage ||
             GetCulture(person.CultureId).NatureAffinity >= 0.6)
            && town.FertilityBoostTicks < 6 &&
            (person.Hunger > 30 || GetLocalPolicy(town.Id) == PolicyKind.FoodSecurity))
            TryCastSpell(person.Id, SpellKind.HarvestBlessing, town.X, town.Y);
    }

    private void RefreshLocalRepresentatives()
    {
        foreach (var town in Current.Settlements)
        {
            if (Current.Residents.Any(r =>
                    r.Id == town.RepresentativeId && r.SettlementId == town.Id && r.Health > 0 &&
                    r.Profession == Profession.Representative))
                continue;
            var representative = Current.Residents.Where(r =>
                    r.SettlementId == town.Id && r.Age >= 16 && r.Health > 0 && r.ArmyId == 0
                    && r.Agent.DestinationSettlementId == 0 && Distance(r.X, r.Y, town.X, town.Y) <= 3)
                .OrderByDescending(r => r.Profession == Profession.Representative)
                .ThenByDescending(r => r.Agent.Personality.Sociability)
                .ThenBy(r => r.Id).FirstOrDefault();
            town.RepresentativeId = representative?.Id ?? 0;
            if (representative is not null)
            {
                representative.Replace(representative.Value with { Profession = Profession.Representative, Agent = representative.Agent.Value with { JobChangedTick = Current.Tick } });
            }
        }

        foreach (var nation in Current.Nations)
            nation.RepresentativeId =
                Current.Settlements.FirstOrDefault(s => s.Id == nation.CapitalId)?.RepresentativeId ?? 0;
    }

    private void PlanLocalDevelopment(SettlementCursor town)
    {
        if (town.FoundationPending)
            return;
        town.LastDevelopmentTick = Current.Tick;
        var local = Current.Residents.Where(r => r.SettlementId == town.Id && r.Age >= 16 && r.Health > 50 &&
                                               r.ArmyId == 0
                                               && Distance(r.X, r.Y, town.X, town.Y) <= 6 &&
                                               r.Agent.DestinationSettlementId == 0).ToArray();
        var buildings = Current.Society.Buildings.Where(b => b.SettlementId == town.Id).ToArray();
        var project = Current.Society.Research.First(r => r.SettlementId == town.Id);
        if (local.Length < 4)
        {
            town.Replace(town.Value with { DevelopmentGoal = "恢复当地劳动力", DevelopmentBlocker = "附近可工作的成年人少于 4 人" });
            return;
        }

        void Recruit(Profession job)
        {
            var needed = job == Profession.Miner ? Math.Clamp(local.Length / 12, 2, 6) : job == Profession.Mage ? 2 : 1;
            if (local.Count(r => r.Profession == job) >= needed)
                return;
            var recruit = local.Where(r =>
                    r.Profession is Profession.Farmer or Profession.Lumberjack or Profession.Miner or Profession.Builder
                        or Profession.Scholar
                    && !r.Agent.Goal.PlayerDirected && Current.Tick - r.Agent.JobChangedTick >= 120
                    && r.Profession != job
                    && (r.Profession == Profession.Farmer
                        ? local.Count(p => p.Profession == Profession.Farmer) >= 4
                        : local.Count(p => p.Profession == r.Profession) >= 2)
                    && (job != Profession.Mage || r.MagicTalent >= 35))
                .OrderByDescending(r => r.Agent.Personality.Diligence).ThenBy(r => r.Id).FirstOrDefault();
            if (recruit is null)
                return;
            recruit.Replace(recruit.Value with { Profession = job, Agent = recruit.Agent.Value with { JobChangedTick = Current.Tick, NextThinkTick = Current.Tick } });
            recruit.Agent.Goal = new AgentGoal { Kind = AgentGoalKind.Idle, TargetX = recruit.X, TargetY = recruit.Y };
            RecordLife(recruit, $"因家园发展需要，接受新的{ProfessionName(job)}岗位。");
            if (recruit.History.Count > 24)
                recruit.History.RemoveAt(0);
        }

        if (buildings.Any(b => b.Health > 0 && !b.IsCompleted))
            Recruit(Profession.Builder);
        if (project.ActiveProject is not null && buildings.Any(b => b.Kind == BuildingKind.Academy && b.IsCompleted))
            Recruit(Profession.Scholar);
        if (Current.Society.MagicEnabled && buildings.Any(b => b.Kind == BuildingKind.ArcaneSanctum && b.IsCompleted))
            Recruit(Profession.Mage);
        var demand = InspectLocalDemand(town, buildings);
        var lowFood = town.Resources.Food < Math.Max(25, town.Population * .4);
        if (lowFood)
            Recruit(Profession.Farmer);
        string? blockedGoal = null, blockedReason = null;

        void RememberBlocker(string reason)
        {
            town.DevelopmentBlocker = reason;
            if (blockedGoal is not null)
                return;
            blockedGoal = town.DevelopmentGoal;
            blockedReason = reason;
        }

        bool PlanBuilding(BuildingKind kind)
        {
            var desired = kind == BuildingKind.Farm ? Math.Clamp((town.Population + 39) / 40, 1, 8)
                : kind == BuildingKind.Housing ? Math.Clamp((town.Population - town.Housing + 19) / 20, 1, 30) : 1;
            if (buildings.Count(b => b.Kind == kind && (b.Health > 0 || !b.Enabled)) >= desired)
                return false;
            if (!FacilityNeeded(demand, kind))
                return false;
            var buildingProjects =
                buildings.Count(b => b.Health > 0 && !b.IsCompleted && !IsPublicInfrastructure(b.Kind));
            if (buildingProjects >=
                (kind is BuildingKind.Farm or BuildingKind.Well or BuildingKind.Housing or BuildingKind.Reservoir
                    ? 3
                    : 2))
                return false;
            town.DevelopmentGoal = "修建" + BuildingName(kind);
            if (!Current.Rules.Construction)
            {
                RememberBlocker("世界规则关闭了自主建设");
                return false;
            }

            var missing = MissingResources(town.Resources, GetBuildingCost(kind));
            if (missing is not null)
            {
                RememberBlocker(missing + "；安排采集与实物运输");
                if (town.Resources.Wood < GetBuildingCost(kind).Wood)
                    Recruit(Profession.Lumberjack);
                if (town.Resources.Stone < GetBuildingCost(kind).Stone ||
                    town.Resources.Ore < GetBuildingCost(kind).Ore)
                    Recruit(Profession.Miner);
                return false;
            }

            var ruined = buildings.FirstOrDefault(b => b.Kind == kind && b.Enabled && b.Health <= 0);
            if (ruined is not null && VisibleWorkSiteReachable(local[0], ruined.X, ruined.Y, true))
            {
                town.Resources = Spend(town.Resources, GetBuildingCost(kind));
                ruined.Replace(ruined.Value with { Health = 100, ConstructionProgress = 0 });
                ruined.UpgradeProgress = ruined.UpgradeRequired = 0;
                ruined.PendingDirection = null;
                ruined.Workers.Clear();
                ruined.LastWorkedTick = -100;
                var entry = AddEvent(WorldEventKind.Construction, town.Name + "投入材料重建" + BuildingName(kind), ruined.X,
                    ruined.Y, EventAction.Started, town.Id);
                ruined.Observation = new ProjectObservation { StartEventId = entry.Id };
                Recruit(Profession.Builder);
                town.DevelopmentBlocker = "等待工人到场重建";
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
            BuildPlannedFacility(town, kind, position % Current.Width, position / Current.Width, reason);
            Recruit(Profession.Builder);
            town.DevelopmentBlocker = "材料已备齐，等待工人到场";
            return true;
        }

        if (project.ActiveProject is not null && !buildings.Any(b => b.Kind == BuildingKind.Academy && b.Health > 0))
        {
            PlanBuilding(BuildingKind.Academy);
            return;
        }

        // 基本生计也须参与项目优先级竞争，避免长期被研究和建设占用资源后无法补足。
        if (demand.Water)
        {
            foreach (var kind in HasResearch(town.Id, Advancement.CivilEngineering)
                         ? new[] { BuildingKind.Reservoir, BuildingKind.Well }
                         : new[] { BuildingKind.Well })
                if (PlanBuilding(kind))
                    return;
        }

        if (demand.Patients && PlanBuilding(HasResearch(town.Id, Advancement.Sanitation)
                ? BuildingKind.Hospital
                : BuildingKind.Infirmary))
            return;
        if (PlanBuilding(BuildingKind.Housing))
            return;
        if (lowFood)
        {
            if (PlanBuilding(BuildingKind.Farm))
                return;
            Recruit(Profession.Farmer);
        }

        if (!buildings.Any(b => b.Kind == BuildingKind.Academy && b.Health > 0) && Current.Rules.Research)
        {
            PlanBuilding(BuildingKind.Academy);
            return;
        }

        foreach (var plan in PendingLocalDevelopment(town, buildings, project))
        {
            if (plan.Facility is { } facility)
            {
                if (PlanBuilding(facility))
                    return;
                continue;
            }

            var kind = plan.Research!;
            town.DevelopmentGoal = "研究" + kind.Name;
            if (!Current.Rules.Research)
            {
                RememberBlocker("世界规则关闭了自主研究");
                continue;
            }

            var missing = MissingResources(town.Resources, GetResearchCost(kind));
            if (missing is not null)
            {
                RememberBlocker(missing);
                if (town.Resources.Wood < GetResearchCost(kind).Wood)
                    Recruit(Profession.Lumberjack);
                if (town.Resources.Stone < GetResearchCost(kind).Stone ||
                    town.Resources.Ore < GetResearchCost(kind).Ore)
                    Recruit(Profession.Miner);
                continue;
            }

            if (!buildings.Any(b => b.Kind == BuildingKind.Academy && b.IsCompleted))
            {
                RememberBlocker("学舍施工中，等待工人完工后研究");
                Recruit(Profession.Builder);
                continue;
            }

            StartResearch(town.Id, kind);
            Recruit(Profession.Scholar);
            town.DevelopmentBlocker = "等待学者到学舍工作";
            return;
        }

        if (blockedGoal is not null)
        {
            town.Replace(town.Value with { DevelopmentGoal = blockedGoal, DevelopmentBlocker = blockedReason! });
            return;
        }

        if (PlanBuildingUpgrade(town, buildings))
            return;
        town.DevelopmentGoal = Current.Rules.Expansion ? "积累物资，建立新聚落" : "维持繁荣与对外交流";
        town.DevelopmentBlocker = Current.Rules.Expansion
            ? $"拓荒条件：人口 {town.Population}/80；需要送达的建村勘察报告、相距至少 {MinimumSettlementDistance} 格的用地与携带补给的拓荒者；费用 {ResourceStock.Format(VillageFoundingCost)}"
            : "已有研究完成；扩张已关闭";
    }

    private IEnumerable<LocalDevelopmentPlan> PendingLocalDevelopment(SettlementCursor town,
        IReadOnlyList<BuildingCursor> buildings, SettlementResearchCursor project)
    {
        if (Current.Rules.Research && !buildings.Any(b => b.Kind == BuildingKind.Academy))
        {
            yield return new LocalDevelopmentPlan(BuildingKind.Academy, null, GetBuildingCost(BuildingKind.Academy));
            yield break;
        }

        var focus = GetDevelopmentFocus(town.Id);
        var technology = focus is DevelopmentFocus.Technology or DevelopmentFocus.Integrated;
        var magic = Current.Society.MagicEnabled && focus is DevelopmentFocus.MagicPractice
            or DevelopmentFocus.ArcaneIndustry or DevelopmentFocus.Integrated;
        var magicalIndustry = magic && focus is DevelopmentFocus.ArcaneIndustry or DevelopmentFocus.Integrated;
        var demand = InspectLocalDemand(town, buildings);
        if (magic && HasResearch(town.Id, Advancement.ArcaneArts) &&
            !buildings.Any(b => b.Kind == BuildingKind.ArcaneSanctum)
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
        foreach (var definition in wanted.Where(r => HasResearch(town.Id, r)))
            foreach (var facility in definition.UnlockedBuildings)
                if (!buildings.Any(b => b.Kind == facility && (b.Health > 0 || !b.Enabled)) &&
                    FacilityNeeded(demand, facility))
                    yield return new LocalDevelopmentPlan(facility, null, GetBuildingCost(facility));
        if (project.ActiveProject is null)
        {
            foreach (var definition in wanted.Where(r =>
                             !HasResearch(town.Id, r) && ResearchPrerequisiteError(town.Id, r) is null)
                         .Select(r => (Research: r, Utility: ResearchUtility(demand, r))).Where(p => p.Utility > 0)
                         .OrderByDescending(p => p.Utility).ThenBy(p => p.Research.Id))
                yield return new LocalDevelopmentPlan(null, definition.Research,
                    GetResearchCost(definition.Research));
        }

        var facilities = new[]
        {
            (BuildingKind.Infirmary, GetLocalPolicy(town.Id) == PolicyKind.PublicHealth),
            (BuildingKind.ArcaneSanctum, magic && HasResearch(town.Id, Advancement.ArcaneArts)),
            (BuildingKind.Waystation, HasResearch(town.Id, Advancement.Logistics)), (BuildingKind.SignalTower,
                technology && HasResearch(town.Id, Advancement.SignalNetwork) &&
                HasResearch(town.Id, Advancement.Electrification)),
            (BuildingKind.Dock, HasResearch(town.Id, Advancement.Logistics)),
            (BuildingKind.Shipyard, HasResearch(town.Id, Advancement.Logistics)),
            (BuildingKind.LumberCamp, town.Population >= 24), (BuildingKind.Quarry, town.Population >= 24),
            (BuildingKind.Well, town.Resources.Water < town.Population * 2),
            (BuildingKind.Granary, town.Population >= 40), (BuildingKind.Housing, town.Population >= 30),
            (BuildingKind.Market, town.Tier >= SettlementTier.Town),
            (BuildingKind.Watchtower, town.Tier >= SettlementTier.Town),
        };
        foreach (var (kind, needed) in facilities)
            if (needed && FacilityNeeded(demand, kind) &&
                !buildings.Any(b => b.Kind == kind && (b.Health > 0 || !b.Enabled)))
                yield return new LocalDevelopmentPlan(kind, null, GetBuildingCost(kind));
        foreach (var kind in Enum.GetValues<BuildingKind>())
            if (FacilityNeeded(demand, kind) && BuildingRace(kind) is not null &&
                CanBuildRacialFacility(town.Id, kind) && !buildings.Any(b => b.Kind == kind)
                && (kind != BuildingKind.DwarvenForge || HasResearch(town.Id, Advancement.Industry))
                && (kind != BuildingKind.SacredGrove || (magic && HasResearch(town.Id, Advancement.ArcaneArts))))
                yield return new LocalDevelopmentPlan(kind, null, GetBuildingCost(kind));
    }

    private ResourceStock LocalDevelopmentReserve(SettlementCursor town)
    {
        if (town.Resources.Food < Math.Max(25, town.Population * .4))
            return new ResourceStock();
        IReadOnlyList<BuildingCursor> buildings = _localWorkQueriesActive
            ? _localWorkBuildings.GetValueOrDefault(town.Id) ?? []
            : Current.Society.Buildings.Where(b => b.SettlementId == town.Id).ToArray();
        var project = _localWorkQueriesActive
            ? _localResearch[town.Id]
            : Current.Society.Research.First(r => r.SettlementId == town.Id);
        // 为下一项可执行的本地计划预留木石；缺矿可推迟魔法项目，但不能阻止基础运输发展。
        // 只评估首个可选计划，并保留实时库存引用，使矿工在自己行动时仍能看到实际需求。
        foreach (var plan in PendingLocalDevelopment(town, buildings, project))
            if (plan.Facility.HasValue ? Current.Rules.Construction : Current.Rules.Research)
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

    private void AddPublicFact(SettlementCursor town, AgentFact fact)
    {
        if (town.PublicKnowledge.Any(f => f.Id == fact.Id))
            return;
        var prior = town.PublicKnowledge.FirstOrDefault(f =>
            fact.HasSameSubject(f));
        if (prior is not null)
        {
            if (prior.ObservedTick >= fact.ObservedTick)
                return;
            town.PublicKnowledge.Remove(prior);
        }

        town.PublicKnowledge.Add(fact);
        while (town.PublicKnowledge.Count > 24)
            town.PublicKnowledge.RemoveAt(0);
    }

    private static void AddResidentFact(ResidentCursor resident, AgentFact fact)
    {
        if (resident.Agent.Memory.Any(f => f.Id == fact.Id))
            return;
        resident.Agent.Memory.Add(fact);
        if (resident.Agent.Memory.Count > 16)
            resident.Agent.Memory.RemoveAt(0);
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

    private static ResourceStock Spend(in ResourceStock stock, ResourceAmounts cost) => Spend(stock, cost.ToStock());

    private SettlementCursor RequireTown(int id)
    {
        return _settlements.TryGetValue(id, out var town) ? town : throw new ArgumentException("聚落不存在。");
    }

    private NationCursor RequireNation(int id)
    {
        return _nations.TryGetValue(id, out var nation) ? nation : throw new ArgumentException("国家不存在。");
    }

    private CultureDefinition RequireCulture(int id)
    {
        return Current.Society.Cultures.FirstOrDefault(c => c.Id == id) ?? throw new ArgumentException("文化不存在。");
    }

    private CultureDefinition GetCulture(int id)
    {
        return Current.Society.Cultures.FirstOrDefault(c => c.Id == id) ?? Current.Society.Cultures[0];
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
