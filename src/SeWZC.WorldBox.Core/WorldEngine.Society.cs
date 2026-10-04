namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    public const int MaxBuildings = 1_536;
    private sealed record LocalDevelopmentPlan(BuildingKind? Facility, ResearchKind? Research, ResourceStock Cost);

    /// <summary>Idempotent defaults for current worlds and new towns.</summary>
    public void InitializeSociety()
    {
        if (State.Society.Cultures.Count == 0)
            State.Society.Cultures.AddRange([
                new() { Id = 1, Name = "河谷互助", Cooperation = 0.85, Innovation = 0.45, NatureAffinity = 0.65 },
                new() { Id = 2, Name = "山地工艺", Cooperation = 0.55, Innovation = 0.85, NatureAffinity = 0.3 },
                new() { Id = 3, Name = "林地共生", Cooperation = 0.65, Innovation = 0.5, NatureAffinity = 0.95 },
                new() { Id = 4, Name = "远行求知", Cooperation = 0.4, Innovation = 0.95, NatureAffinity = 0.45 }
            ]);
        foreach (var nation in State.Nations)
        {
            var capital = State.Settlements.FirstOrDefault(s => s.Id == nation.CapitalId);
            if (!State.Society.Cultures.Any(c => c.Id == nation.CultureId))
                nation.CultureId = capital is null ? State.Society.Cultures[0].Id : InitialCulture(capital.X, capital.Y);
            if (!State.Society.Institutions.Any(i => i.NationId == nation.Id))
                State.Society.Institutions.Add(new NationInstitution { NationId = nation.Id });
        }
        EnsureTownCenters();
        foreach (var town in State.Settlements)
        {
            if (!State.Society.Cultures.Any(c => c.Id == town.CultureId))
                town.CultureId = State.Nations.FirstOrDefault(n => n.Id == town.NationId)?.CultureId ?? State.Society.Cultures[0].Id;
            if (!State.Society.Policies.Any(p => p.SettlementId == town.Id))
            {
                var manual = State.Society.Institutions.FirstOrDefault(i => i.NationId == town.NationId)?.PlayerPolicy;
                State.Society.Policies.Add(new LocalPolicy { SettlementId = town.Id, Kind = manual ?? PolicyKind.Balanced, PlayerOverride = manual.HasValue });
            }
            if (State.Society.Research.Any(r => r.SettlementId == town.Id)) continue;
            State.Society.Research.Add(new SettlementResearch { SettlementId = town.Id });
            // Founding households bring a field and a workshop; later facilities must be constructed.
            if (!town.FoundationPending)
            { AddFoundingFacility(town, BuildingKind.Farm); AddFoundingFacility(town, BuildingKind.Workshop); }
        }
        foreach (var resident in State.Residents)
        {
            if (State.Society.Cultures.Any(c => c.Id == resident.CultureId)) continue;
            resident.CultureId = State.Settlements.FirstOrDefault(s => s.Id == resident.SettlementId)?.CultureId ?? State.Society.Cultures[0].Id;
            var baseTalent = resident.Race switch { RaceKind.Elf => 45, RaceKind.Dwarf => 23, RaceKind.Orc => 28, _ => 32 };
            resident.MagicTalent = baseTalent + (unchecked((uint)resident.Id * 2654435761u ^ (uint)State.Seed) % 36);
        }
        ReconcileSocietyTopology();
        RefreshLocalRepresentatives();
    }

    private int InitialCulture(int x, int y)
    {
        var preferred = State.Tiles[Index(x, y)].Terrain switch
        { TerrainType.Forest => 3, TerrainType.Hills or TerrainType.Snow or TerrainType.Tundra => 2, TerrainType.Desert or TerrainType.Sand => 4, _ => 1 };
        return State.Society.Cultures.Any(c => c.Id == preferred) ? preferred : State.Society.Cultures[0].Id;
    }

    private void AddFoundingFacility(Settlement town, BuildingKind kind)
    {
        if (State.Society.Buildings.Count >= MaxBuildings - 256) return;
        var position = BestBuildingSite(town, kind, founding: true);
        if (position >= 0)
        {
            var building = new Building { Id = NewId(), SettlementId = town.Id, Kind = kind,
                X = position % State.Width, Y = position / State.Width, ConstructionProgress = 30, ConstructionRequired = 30 };
            State.Society.Buildings.Add(building); CompleteLandImprovement(building);
        }
    }

    public static ResourceStock GetBuildingCost(BuildingKind kind) => kind switch
    {
        BuildingKind.TownCenter => new() { Wood = 12, Stone = 3 },
        BuildingKind.AssemblyHall or BuildingKind.TradeGuild => new() { Food = 15, Wood = 30, Stone = 20 },
        BuildingKind.SacredGrove or BuildingKind.HerbGarden => new() { Food = 20, Wood = 25, Stone = 15 },
        BuildingKind.MiningHall => new() { Wood = 20, Stone = 35, Ore = 8 },
        BuildingKind.HuntingCamp or BuildingKind.WarDrum => new() { Food = 15, Wood = 25, Stone = 10 },
        BuildingKind.Farm => new() { Wood = 12, Stone = 3 },
        BuildingKind.Workshop => new() { Wood = 18, Stone = 10 },
        BuildingKind.Academy => new() { Food = 20, Wood = 30, Stone = 15 },
        BuildingKind.Waystation => new() { Wood = 20, Stone = 15 },
        BuildingKind.SignalTower => new() { Stone = 30, Alloy = 8, EnergyCells = 5 },
        BuildingKind.Bridge => new() { Wood = 8, Stone = 4 },
        BuildingKind.MountainPass => new() { Wood = 6, Stone = 10 },
        BuildingKind.ArcaneSanctum => new() { Food = 15, Wood = 20, Stone = 25, Ore = 12 },
        BuildingKind.Infirmary => new() { Food = 10, Wood = 25, Stone = 10 },
        BuildingKind.Dock => new() { Wood = 20, Stone = 12 },
        BuildingKind.LumberCamp => new() { Wood = 16, Stone = 8 },
        BuildingKind.Quarry => new() { Wood = 20, Stone = 12 },
        BuildingKind.Well => new() { Wood = 12, Stone = 18 },
        BuildingKind.Granary => new() { Wood = 28, Stone = 18 },
        BuildingKind.Housing => new() { Wood = 25, Stone = 8 },
        BuildingKind.Market => new() { Food = 15, Wood = 30, Stone = 15 },
        BuildingKind.Watchtower => new() { Wood = 25, Stone = 20 },
        _ => AdvancementRules.For(kind)?.BuildingCost.Copy() ?? throw new ArgumentOutOfRangeException(nameof(kind))
    };

    public static ResourceStock GetResearchCost(ResearchKind kind) => kind switch
    {
        ResearchKind.Agriculture => new() { Food = 20, Wood = 15 },
        ResearchKind.Logistics => new() { Food = 20, Wood = 20, Stone = 10 },
        ResearchKind.SignalNetwork => new() { Food = 25, Alloy = 8, EnergyCells = 6 },
        ResearchKind.ArcaneArts => new() { Food = 25, Wood = 15, Ore = 8 },
        _ => AdvancementRules.For(kind)?.ResearchCost.Copy() ?? throw new ArgumentOutOfRangeException(nameof(kind))
    };

    public int BuildFacility(int settlementId, BuildingKind kind, int x, int y, BridgeDirection? direction = null, int bridgeLevel = 1) => PlaceFacility(settlementId, kind, x, y, false, direction, bridgeLevel);

    private int PlaceFacility(int settlementId, BuildingKind kind, int x, int y, bool gift, BridgeDirection? direction = null, int bridgeLevel = 1)
    {
        if (FacilityPlacementError(settlementId, kind, x, y, gift, direction, bridgeLevel) is { } error) throw new InvalidOperationException(error);
        var town = RequireTown(settlementId);
        var tile = State.Tiles[Index(x, y)];
        if (!gift) Spend(town.Resources, FacilityCost(kind, bridgeLevel));
        var building = new Building { Id = NewId(), SettlementId = settlementId, Kind = kind, X = x, Y = y, Level = kind == BuildingKind.Bridge ? bridgeLevel : 1,
            Direction = kind == BuildingKind.Bridge ? direction ?? InferBridgeDirection(x, y) : BridgeDirection.Horizontal,
            ConstructionRequired = kind is BuildingKind.SignalTower or BuildingKind.ArcaneSanctum ? 60 : 30, WorkSlots = (kind == BuildingKind.Farm ? 5 : 3) + (kind == BuildingKind.Bridge ? bridgeLevel - 1 : 0) };
        State.Society.Buildings.Add(building);
        if (_localWorkQueriesActive)
        { _workBuildingsById[building.Id] = building; LocalWorkGroup(_localWorkBuildings, _localWorkBuildingBuffers, settlementId).Add(building); }
        if (gift)
        {
            building.ConstructionProgress = building.ConstructionRequired;
            if (IsForestTerrain(tile.Terrain) && !PreserveBuildingForest(kind)) { tile.Terrain = TerrainType.Grass; tile.ResourceAmount = 0; }
            CompleteLandImprovement(building);
            EmitVisual(WorldVisualKind.Construction, x, y);
        }
        if (gift) RegisterBuildingGround(building);
        var projectEvent = AddEvent(WorldEventKind.Construction, gift ? $"玩家向{town.Name}赐予{BuildingName(kind)}；效果受建筑健康、启用与当地条件限制。" : $"{town.Name}备好材料，开始修建{BuildingName(kind)}；居民必须到场施工。", x, y, gift ? EventAction.Gifted : EventAction.Started, town.Id);
        building.Observation.StartEventId = projectEvent.Id;
        ObserveProject(building.Observation, building.ConstructionProgress);
        RefreshTotals();
        return building.Id;
    }

    public void BuildRoad(int settlementId, int x, int y, int radius = 1)
    {
        var town = RequireTown(settlementId);
        if (!InBounds(x, y) || Distance(x, y, town.X, town.Y) > 24) throw new ArgumentException("道路须位于聚落周边 24 格内。");
        if (radius is < 0 or > 4) throw new ArgumentOutOfRangeException(nameof(radius), "道路笔刷半径须在 0 到 4 之间。");
        var tiles = Circle(x, y, radius).Where(i => State.Tiles[i].IsWalkable && State.Tiles[i].RoadLevel == 0
            && (State.Tiles[i].NationId == 0 || State.Tiles[i].NationId == town.NationId)).ToArray();
        if (tiles.Length == 0) throw new InvalidOperationException("笔刷内没有可修建道路的土地。");
        Spend(town.Resources, new ResourceStock { Wood = tiles.Length * 0.5, Stone = tiles.Length });
        foreach (var index in tiles) State.Tiles[index].RoadLevel = 1;
        RefreshTotals();
    }

    public void StartResearch(int settlementId, ResearchKind kind)
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        var town = RequireTown(settlementId);
        var research = State.Society.Research.First(r => r.SettlementId == settlementId);
        if (research.Completed.Contains(kind)) throw new InvalidOperationException("当地已经掌握这项知识。");
        if (research.ActiveProject.HasValue) throw new InvalidOperationException("当地已有正在进行的研究。");
        if (!State.Society.Buildings.Any(b => b.SettlementId == settlementId && b.Kind == BuildingKind.Academy && b.IsCompleted)) throw new InvalidOperationException("研究需要已建成的学舍与实际到场的研究人员。");
        if (ResearchPrerequisiteError(settlementId, kind) is { } prerequisite) throw new InvalidOperationException(prerequisite);
        Spend(town.Resources, GetResearchCost(kind));
        research.ActiveProject = kind; research.Progress = 0;
        research.RequiredProgress = AdvancementRules.For(kind) is not null ? 180 : kind is ResearchKind.SignalNetwork or ResearchKind.ArcaneArts ? 100 : 60;
        var start = AddEvent(WorldEventKind.Research, $"{town.Name}投入材料，开始研究{ResearchName(kind)}。", town.X, town.Y, EventAction.Started, town.Id);
        research.Observation = new ProjectObservation { StartEventId = start.Id, DevelopmentRate = State.Rules.DevelopmentRate };
        ObserveProject(research.Observation, 0);
        RefreshTotals();
    }

    public bool HasResearch(int settlementId, ResearchKind kind) => (_localWorkQueriesActive ? _localResearch.GetValueOrDefault(settlementId) : State.Society.Research.FirstOrDefault(r => r.SettlementId == settlementId))?.Completed.Contains(kind) == true;
    public int GetLocalTechnologyLevel(int settlementId) => 1 + (State.Society.Research.FirstOrDefault(r => r.SettlementId == settlementId)?.Completed.Count(k => k <= ResearchKind.ArcaneArts) ?? 0);

    public void GrantReceivedResearch(int settlementId, ResearchKind kind, int causeEventId = 0, int evidenceFactId = 0)
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        var town = RequireTown(settlementId);
        var research = State.Society.Research.First(r => r.SettlementId == settlementId);
        if (research.Completed.Contains(kind)) return;
        research.Completed.Add(kind); research.Completed.Sort();
        if (_nations.TryGetValue(town.NationId, out var nation)) nation.Technology = Math.Max(nation.Technology, GetLocalTechnologyLevel(town.Id));
        var entry = AddEvent(WorldEventKind.Research, $"{town.Name}掌握了{ResearchName(kind)}；知识可由当地居民和信使继续传授。", town.X, town.Y,
            EventAction.Completed, town.Id, causeEventId: causeEventId, evidenceFactId: evidenceFactId);
        research.LastCompletionEventId = entry.Id;
        if (research.ActiveProject == kind)
        {
            if (research.Observation.StartEventId > 0 && research.Observation.StartEventId != causeEventId)
                entry.AdditionalCauseEventIds.Add(research.Observation.StartEventId);
            foreach (var person in State.Residents.Where(r => research.Observation.Contributors.Contains(r.Id)))
                RecordLife(person, $"参与{town.Name}的{ResearchName(kind)}研究，现已掌握成果。", entry, PersonalExperienceKind.Learning);
            research.ActiveProject = null; research.Progress = 0; research.RequiredProgress = 0; research.Observation = new();
        }
    }

    public bool TryGetLocalWorkTarget(Resident resident, out int x, out int y)
    {
        var building = FindLocalWorkBuilding(resident, 8, preferNearest: true);
        x = building?.X ?? resident.X; y = building?.Y ?? resident.Y;
        return building is not null;
    }

    private Building? FindLocalWorkTarget(Resident resident) => FindLocalWorkBuilding(resident, 8, preferNearest: true);

    private int WorkPriority(Building building, Resident resident)
    {
        if (!building.IsCompleted || building.IsUpgrading || building.Kind == BuildingKind.TownCenter && RequireTown(building.SettlementId).IsExpanding) return 0;
        if (AdvancementRules.For(building.Kind) is { } production)
        {
            if (resident.Profession == Profession.Scholar && State.Society.Research.Any(r => r.SettlementId == building.SettlementId && r.ActiveProject.HasValue)) return 3;
            var stock = RequireTown(building.SettlementId).Resources.Get(production.Output);
            return stock < (production.Output == ResourceKind.Food ? 100 : 80) ? 1 : 4;
        }
        if (building.Kind is BuildingKind.Waystation or BuildingKind.SignalTower) return building.LastWorkedTick < State.Tick - 6 ? 1 : 5;
        if ((resident.Profession == Profession.Mage || resident.Agent.Goal.Kind == AgentGoalKind.TrainMagic) && building.Kind == BuildingKind.ArcaneSanctum) return resident.MagicTraining < 8 ? 1 : 2;
        if ((resident.Profession == Profession.Scholar || resident.Agent.Goal.Kind == AgentGoalKind.Study) && building.Kind == BuildingKind.Academy) return 1;
        if (building.Kind == BuildingKind.Academy && resident.Agent.Personality.Ambition > 0.55) return 2;
        if (building.Kind == BuildingKind.ArcaneSanctum && resident.MagicTalent >= 45) return 2;
        if (resident.Profession == Profession.Farmer && building.Kind == BuildingKind.Farm) return 2;
        if (resident.Profession is Profession.Miner or Profession.Lumberjack && building.Kind == BuildingKind.Workshop) return 2;
        return 3;
    }

    private bool BuildingHasWork(Building building, Resident resident)
    {
        if (!building.Enabled || resident.Age < 14 || resident.ArmyId != 0 || resident.Health <= 0) return false;
        if (building.LastWorkedTick == State.Tick && building.Workers.Count >= building.WorkSlots && !building.Workers.Contains(resident.Id)) return false;
        if (!building.IsCompleted || building.IsUpgrading) return true;
        if (building.Health < 50 || State.Tiles[Index(building.X, building.Y)].FireTicks > 0) return false;
        if (BuildingRace(building.Kind) is { } race && resident.Race != race) return false;
        if (AdvancementRules.For(building.Kind) is { } production) return CanProduce(building, resident, production);
        if (BuildingRace(building.Kind) is not null) return RacialBuildingHasWork(building, resident);
        return building.Kind switch
        {
            BuildingKind.Workshop => FindWorkshopResource(building, resident.Profession) >= 0,
            BuildingKind.LumberCamp => resident.Profession == Profession.Lumberjack && FindWorkshopResource(building, Profession.Lumberjack) >= 0,
            BuildingKind.Quarry => resident.Profession == Profession.Miner && FindWorkshopResource(building, Profession.Miner) >= 0,
            BuildingKind.Well => resident.Inventory.Water < WaterReserve(resident) + 3 && AvailableWater(building.X, building.Y) > 0,
            BuildingKind.Academy => State.Society.Research.Any(r => r.SettlementId == building.SettlementId && r.ActiveProject.HasValue),
            BuildingKind.ArcaneSanctum => State.Society.MagicEnabled && resident.MagicTalent >= 25 && resident.MagicTraining < 100,
            BuildingKind.Infirmary => FindLocalWorkPatient(building, firstOnly: true) is not null,
            BuildingKind.TownCenter => RequireTown(building.SettlementId).IsExpanding,
            BuildingKind.Housing or BuildingKind.Granary or BuildingKind.Watchtower or BuildingKind.MountainPass or BuildingKind.Bridge => false,
            _ => true
        };
    }

    public bool TryWorkAtBuilding(Resident resident)
    {
        var building = FindLocalWorkBuilding(resident, 1, preferNearest: false, followTarget: true);
        if (building is null || !_settlements.TryGetValue(building.SettlementId, out var town)) return false;
        if (building.IsCompleted && !building.IsUpgrading && building.Kind is BuildingKind.Waystation or BuildingKind.SignalTower or BuildingKind.Dock or BuildingKind.Market && town.Resources.Food < 0.01) return false;
        if (building.IsCompleted && !building.IsUpgrading && building.Kind == BuildingKind.ArcaneSanctum && (!State.Society.MagicEnabled || town.Resources.Food < 0.03)) return false;
        var production = AdvancementRules.For(building.Kind);
        if (building.IsCompleted && !building.IsUpgrading && production is not null && MissingResources(resident.Inventory, production.Input) is not null) return false;
        if (building.LastWorkedTick != State.Tick) { building.Workers.Clear(); building.LastWorkedTick = State.Tick; }
        if (building.Workers.Contains(resident.Id)) return false;
        building.Workers.Add(resident.Id);
        var effort = Math.Clamp((0.6 + resident.Agent.Personality.Diligence * 0.6) * LaborCondition(resident), 0.1, 1.2);
        if (building.IsUpgrading)
        {
            building.UpgradeProgress = Math.Min(building.UpgradeRequired, building.UpgradeProgress + effort * State.Rules.DevelopmentRate);
            if (building.UpgradeProgress >= building.UpgradeRequired) FinishBuildingUpgrade(building);
            return true;
        }
        if (!building.IsCompleted)
        {
            if (building.Observation.Contributors.Count < 32 && !building.Observation.Contributors.Contains(resident.Id)) building.Observation.Contributors.Add(resident.Id);
            building.ConstructionProgress = Math.Min(building.ConstructionRequired, building.ConstructionProgress + effort * State.Rules.DevelopmentRate);
            if (building.IsCompleted)
            {
                var ground = State.Tiles[Index(building.X, building.Y)];
                if (IsForestTerrain(ground.Terrain) && !PreserveBuildingForest(building.Kind)) { ground.Terrain = TerrainType.Grass; ground.ResourceAmount = 0; }
                CompleteLandImprovement(building);
                EmitVisual(WorldVisualKind.Construction, building.X, building.Y);
                var complete = AddEvent(WorldEventKind.Construction, $"{town.Name}的{BuildingName(building.Kind)}竣工。", building.X, building.Y,
                    EventAction.Completed, town.Id, causeEventId: building.Observation.StartEventId);
                foreach (var person in State.Residents.Where(r => building.Observation.Contributors.Contains(r.Id)))
                    RecordLife(person, $"参与施工的{BuildingName(building.Kind)}竣工。", complete, PersonalExperienceKind.Achievement);
            }
            return true;
        }
        if (production is not null) return Produce(building, resident, production);
        effort *= building.Efficiency * RaceTerrainRules.For(resident.Race, State.Tiles[Index(building.X, building.Y)].Terrain).Productivity;
        if (BuildingRace(building.Kind) is not null) return WorkRacialBuilding(building, resident, effort);
        var culture = GetCulture(resident.CultureId);
        switch (building.Kind)
        {
            case BuildingKind.TownCenter: return WorkOnTownExpansion(town, effort / building.Efficiency);
            case BuildingKind.Well:
                return resident.X == building.X && resident.Y == building.Y && DrawWater(resident, Index(building.X, building.Y), Math.Min(1, effort)) > 0;
            case BuildingKind.Farm:
                var tile = State.Tiles[Index(building.X, building.Y)];
                var fertility = tile.Fertility / 100d * (tile.DroughtTicks > 0 ? 0.18 : 1) * (tile.FireTicks > 0 ? 0 : 1);
                var harvest = 0.5 * effort * fertility * (1 + culture.NatureAffinity * 0.2) * (HasResearch(town.Id, ResearchKind.Agriculture) ? 1.35 : 1)
                    * (town.FertilityBoostTicks > 0 ? 1.35 : 1) * GetPolicyProductionMultiplier(town.Id);
                RecordHarvest(tile, harvest);
                resident.Inventory.Food = Math.Min(1_000_000, resident.Inventory.Food + harvest);
                return harvest > 0;
            case BuildingKind.Workshop:
            case BuildingKind.LumberCamp:
            case BuildingKind.Quarry:
                var source = FindWorkshopResource(building, resident.Profession);
                if (source < 0) return false;
                var sourceTile = State.Tiles[source]; var yields = TerrainRules.For(sourceTile.Terrain);
                var amount = Math.Min(sourceTile.ResourceAmount, effort * 0.2 * State.Rules.GatheringRate);
                sourceTile.ResourceAmount -= amount;
                if (resident.Profession == Profession.Miner) { resident.Inventory.Stone += amount * yields.StoneYield; resident.Inventory.Ore += amount * yields.OreYield; }
                else
                {
                    resident.Inventory.Wood += amount * yields.WoodYield;
                    FinishLogging(sourceTile, source % State.Width, source / State.Width);
                }
                RecordHarvest(sourceTile, amount * (resident.Profession == Profession.Miner ? yields.StoneYield + yields.OreYield : yields.WoodYield));
                CapResources(resident.Inventory); return true;
            case BuildingKind.Academy:
                var research = State.Society.Research.First(r => r.SettlementId == town.Id);
                if (!research.ActiveProject.HasValue) return false;
                if (research.Observation.Contributors.Count < 32 && !research.Observation.Contributors.Contains(resident.Id)) research.Observation.Contributors.Add(resident.Id);
                research.Progress += effort * State.Rules.DevelopmentRate * (1 + (int)town.Tier * .1) * (0.75 + culture.Innovation * 0.5) * (GetLocalPolicy(town.Id) == PolicyKind.Scholarship ? 1.35 : 1);
                if (research.Progress >= 8 && resident.Profession == Profession.Builder && !HasTwoLocalWorkers(town.Id, Profession.Scholar))
                    resident.Profession = Profession.Scholar;
                if (research.Progress >= research.RequiredProgress)
                {
                    var completed = research.ActiveProject.Value;
                    GrantReceivedResearch(town.Id, completed, research.Observation.StartEventId);
                    var fact = new AgentFact { Id = NewId(), EventId = research.LastCompletionEventId, Kind = AgentFactKind.Research, SubjectId = town.Id, X = town.X, Y = town.Y, Value = (int)completed,
                        ObservedTick = State.Tick, LearnedTick = State.Tick, OriginResidentId = resident.Id, OriginProfession = resident.Profession, SourceResidentId = resident.Id,
                        Text = $"{town.Name}已完成{ResearchName(completed)}研究" };
                    AddPublicFact(town, fact);
                    AddResidentFact(resident, fact);
                }
                return true;
            case BuildingKind.ArcaneSanctum:
                if (!State.Society.MagicEnabled || town.Resources.Food < 0.03) return false;
                town.Resources.Food -= 0.03;
                resident.MagicTraining = Math.Min(100, resident.MagicTraining + effort * State.Rules.MagicRate * (0.05 + resident.MagicTalent / 500) * TerrainRules.For(State.Tiles[Index(resident.X, resident.Y)].Terrain).ManaRate);
                if (resident.MagicTraining >= 8 && resident.Profession is Profession.Builder or Profession.Scholar && !HasTwoLocalWorkers(town.Id, Profession.Mage))
                    resident.Profession = Profession.Mage;
                resident.Mana = Math.Min(100, resident.Mana + 0.15 * effort); return true;
            case BuildingKind.Infirmary:
                if (town.Resources.Food < 0.05) return false;
                var patient = FindLocalWorkPatient(building);
                if (patient is null) return false;
                town.Resources.Food -= 0.05; patient.Health = Math.Min(100, patient.Health + 0.45 * effort);
                patient.SicknessTicks = Math.Max(0, patient.SicknessTicks - 1); return true;
            case BuildingKind.Waystation:
            case BuildingKind.SignalTower:
            case BuildingKind.Dock:
            case BuildingKind.Market:
                if (town.Resources.Food < 0.01) return false;
                town.Resources.Food -= 0.01; return true;
            default: return false;
        }
    }

    private int FindWorkshopResource(Building building, Profession profession)
    {
        var best = -1; var bestYield = 0d;
        foreach (var index in Circle(building.X, building.Y, 1))
        {
            var tile = State.Tiles[index];
            if (tile.ResourceAmount < .5 || tile.FireTicks > 0) continue;
            var yield = TerrainRules.For(tile.Terrain);
            var value = profession == Profession.Miner ? yield.StoneYield + yield.OreYield : yield.WoodYield;
            if (value > bestYield || value == bestYield && value > 0 && index < best)
            { best = index; bestYield = value; }
        }
        return best;
    }

    public double GetTerrainMoveCost(int x, int y, RaceKind race = RaceKind.Human)
    {
        if (!InBounds(x, y)) return double.PositiveInfinity;
        var tile = State.Tiles[Index(x, y)];
        if (tile.Improvement == LandImprovement.MountainPass && tile.Terrain == TerrainType.Mountain) return race == RaceKind.Dwarf ? Math.Min(2, 3.5 / (1 + Math.Max(0, tile.RoadLevel - 1) * .25)) : 3.5 / (1 + Math.Max(0, tile.RoadLevel - 1) * .25);
        if (tile.Improvement == LandImprovement.Bridge && tile.Terrain is TerrainType.River or TerrainType.Stream or TerrainType.LargeRiver or TerrainType.Water or TerrainType.Lake) return 1.2 / (1 + Math.Max(0, tile.BridgeLevel - 1) * .25);
        var cost = race == RaceKind.Dwarf && tile.Terrain == TerrainType.Mountain ? 2.5 : TerrainRules.MovementCost(tile.Terrain);
        cost *= RaceTerrainRules.For(race, tile.Terrain).Movement;
        if (!double.IsFinite(cost)) return cost;
        return tile.RoadLevel > 0 ? Math.Max(0.65, cost * 0.55) : cost;
    }

    public double MessageTravelMultiplier(int x, int y, int nationId, RaceKind race = RaceKind.Human)
    {
        if (!InBounds(x, y)) return 0;
        var speed = 1 / GetTerrainMoveCost(x, y, race);
        var bonus = 1d;
        foreach (var town in State.Settlements)
            if (town.NationId == nationId && Distance(x, y, town.X, town.Y) <= 3)
                bonus = Math.Max(bonus, 1 + (int)town.Tier * .15);
        foreach (var building in State.Society.Buildings)
            if (building.Kind == BuildingKind.Waystation && IsFacilityOperating(building)
                && _settlements.TryGetValue(building.SettlementId, out var town) && town.NationId == nationId && Distance(x, y, building.X, building.Y) <= 3)
                bonus = Math.Max(bonus, 1.25 + (building.Level - 1) * .15);
        speed *= bonus;
        return speed;
    }

    public bool CanRelayInformation(int fromSettlementId, int toSettlementId, out int travelTicks)
    {
        travelTicks = 0;
        if (!_settlements.TryGetValue(fromSettlementId, out var from) || !_settlements.TryGetValue(toSettlementId, out var to) || from.NationId != to.NationId) return false;
        // Ancient worlds have no relay network to search or allocate traversal buffers for.
        if (!State.Society.Buildings.Any(b => b.Kind == BuildingKind.SignalTower && b.IsCompleted)) return false;
        var towers = State.Society.Buildings.Where(b => b.Kind == BuildingKind.SignalTower && IsFacilityOperating(b)
            && HasResearch(b.SettlementId, ResearchKind.SignalNetwork) && HasResearch(b.SettlementId, ResearchKind.Electrification) && _settlements.TryGetValue(b.SettlementId, out var town) && town.NationId == from.NationId).OrderBy(b => b.Id).ToArray();
        var queue = new Queue<(Building Tower, int Hops)>(); var visited = new HashSet<int>();
        foreach (var tower in towers.Where(t => Distance(t.X, t.Y, from.X, from.Y) <= 12 + (t.Level - 1) * 4 && ClearSignalLine(t.X, t.Y, from.X, from.Y)))
        { queue.Enqueue((tower, 1)); visited.Add(tower.Id); }
        while (queue.TryDequeue(out var node))
        {
            if (Distance(node.Tower.X, node.Tower.Y, to.X, to.Y) <= 12 + (node.Tower.Level - 1) * 4 && ClearSignalLine(node.Tower.X, node.Tower.Y, to.X, to.Y)) { travelTicks = node.Hops * 2; return true; }
            foreach (var tower in towers)
                if (!visited.Contains(tower.Id) && Distance(tower.X, tower.Y, node.Tower.X, node.Tower.Y) <= 24 + (Math.Min(tower.Level, node.Tower.Level) - 1) * 8 && ClearSignalLine(tower.X, tower.Y, node.Tower.X, node.Tower.Y))
                { visited.Add(tower.Id); queue.Enqueue((tower, node.Hops + 1)); }
        }
        return false;
    }

    private bool IsFacilityOperating(Building building) => building.Enabled && building.IsCompleted && !building.IsUpgrading && building.Health >= 50 && BuildingTerrainValid(building.Kind, State.Tiles[Index(building.X, building.Y)])
        && State.Tiles[Index(building.X, building.Y)].FireTicks == 0 && (PassiveFacility(building) || building.Kind is BuildingKind.TownCenter or BuildingKind.Bridge or BuildingKind.MountainPass || building.LastWorkedTick >= State.Tick - 12
        && State.Residents.Any(r => building.Workers.Contains(r.Id) && r.SettlementId == building.SettlementId && r.Health > 0 && (BuildingRace(building.Kind) is not { } race || r.Race == race && r.Age >= 14) && Distance(r.X, r.Y, building.X, building.Y) <= 1));
    private bool ClearSignalLine(int x0, int y0, int x1, int y1)
    {
        var steps = Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0));
        for (var i = 1; i < steps; i++)
        {
            var x = x0 + (int)Math.Round((x1 - x0) * i / (double)steps); var y = y0 + (int)Math.Round((y1 - y0) * i / (double)steps);
            if (State.Tiles[Index(x, y)].Terrain == TerrainType.Mountain) return false;
        }
        return true;
    }

    public void SetInstitution(int nationId, InstitutionKind kind)
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        _ = RequireNation(nationId);
        State.Society.Institutions.First(i => i.NationId == nationId).Kind = kind;
    }

    public void SetPolicy(int nationId, PolicyKind policy)
    {
        if (!Enum.IsDefined(policy)) throw new ArgumentOutOfRangeException(nameof(policy));
        var nation = RequireNation(nationId);
        State.Society.Institutions.First(i => i.NationId == nationId).PlayerPolicy = policy;
        foreach (var town in State.Settlements.Where(s => s.NationId == nationId))
        {
            var local = State.Society.Policies.First(p => p.SettlementId == town.Id);
            local.Kind = policy; local.PlayerOverride = true; local.DecidedTick = State.Tick; local.Reason = "玩家直接设定；恢复自治前保持此政策";
        }
        AddEvent(WorldEventKind.Editor, $"{nation.Name}的政策由玩家调整为{PolicyName(policy)}。");
    }

    public void SetPolicyAutonomy(int nationId)
    {
        _ = RequireNation(nationId);
        State.Society.Institutions.First(i => i.NationId == nationId).PlayerPolicy = null;
        foreach (var town in State.Settlements.Where(s => s.NationId == nationId)) State.Society.Policies.First(p => p.SettlementId == town.Id).PlayerOverride = false;
    }

    public PolicyKind GetLocalPolicy(int settlementId) => State.Society.Policies.FirstOrDefault(p => p.SettlementId == settlementId)?.Kind ?? PolicyKind.Balanced;
    public double GetPolicyProductionMultiplier(int settlementId) => GetLocalPolicy(settlementId) == PolicyKind.FoodSecurity ? 1.25 : 1;

    public void ApplyReceivedPolicy(int settlementId, PolicyKind policy)
    {
        if (!Enum.IsDefined(policy)) return;
        _ = RequireTown(settlementId);
        var local = State.Society.Policies.First(p => p.SettlementId == settlementId);
        if (local.PlayerOverride) return;
        local.Kind = policy; local.DecidedTick = State.Tick; local.Reason = "代表已收到递送的政策指令";
    }

    private void ReceiveSocietyReport(Settlement target, Resident carrier, AgentFact fact)
    {
        if (Distance(carrier.X, carrier.Y, target.X, target.Y) > 2 || fact.ObservedTick > State.Tick || fact.Confidence is < 0 or > 1 || !double.IsFinite(fact.Value)) return;
        ReceiveDiplomaticNotice(target, fact);
        ReceiveWarReport(target, fact);
        if (fact.Confidence >= 0.5 && fact.Kind == AgentFactKind.Research && fact.Value == Math.Truncate(fact.Value) && Enum.IsDefined((ResearchKind)(int)fact.Value)) GrantReceivedResearch(target.Id, (ResearchKind)(int)fact.Value, fact.EventId, fact.Id);
        if (fact.Confidence >= 0.5 && fact.Kind == AgentFactKind.Policy && State.Tick - fact.ObservedTick <= 240 && fact.Value is >= 0 and <= 4 && fact.Value == Math.Truncate(fact.Value))
        {
            var policy = State.Society.Policies.First(p => p.SettlementId == target.Id);
            if (fact.ObservedTick >= policy.EvidenceObservedTick)
            {
                ApplyReceivedPolicy(target.Id, (PolicyKind)(int)fact.Value);
                if (!policy.PlayerOverride) { policy.EvidenceFactId = fact.Id; policy.EvidenceObservedTick = fact.ObservedTick; }
            }
        }
        if (fact.Confidence >= 0.5 && fact.Kind == AgentFactKind.Culture && fact.Value is > 0 and <= 100_000 && fact.Value == Math.Truncate(fact.Value)) ObserveCulture(carrier, (int)fact.Value);
        if (fact.Kind is not (AgentFactKind.FoodSupply or AgentFactKind.ReliefRequest or AgentFactKind.Danger or AgentFactKind.Research or AgentFactKind.Personal)) return;
        if (State.Society.Reports.Any(r => r.RecipientSettlementId == target.Id && r.FactId == fact.Id)) return;
        State.Society.Reports.Add(new InstitutionReport { EventId = fact.EventId, RecipientSettlementId = target.Id, FactId = fact.Id, OriginResidentId = fact.OriginResidentId,
            RepresentativeId = carrier.Id, ReportedProfession = fact.OriginProfession, Topic = fact.Kind, SubjectId = fact.SubjectId, Value = fact.Value,
            Confidence = fact.Confidence, ObservedTick = fact.ObservedTick, ReceivedTick = State.Tick });
        if (State.Society.Reports.Count > 2_048) State.Society.Reports.RemoveRange(0, State.Society.Reports.Count - 2_048);
    }

    private void DecideLocalPolicy(Settlement town)
    {
        var institution = State.Society.Institutions.First(i => i.NationId == town.NationId);
        var local = State.Society.Policies.First(p => p.SettlementId == town.Id);
        if (institution.PlayerPolicy.HasValue) { local.Kind = institution.PlayerPolicy.Value; local.PlayerOverride = true; return; }
        if (local.PlayerOverride) return;
        var reports = State.Society.Reports.Where(r => r.RecipientSettlementId == town.Id && r.ObservedTick <= State.Tick)
            .GroupBy(r => (r.OriginResidentId, r.Topic)).Select(g => g.OrderByDescending(r => r.ObservedTick).ThenByDescending(r => r.ReceivedTick).First()).ToArray();
        if (reports.Length == 0) return;
        var scores = new double[5]; var evidence = new InstitutionReport?[5]; var largest = new double[5];
        foreach (var report in reports)
        {
            var policy = report.Topic switch { AgentFactKind.FoodSupply or AgentFactKind.ReliefRequest => PolicyKind.FoodSecurity, AgentFactKind.Danger => PolicyKind.Defense, AgentFactKind.Research => PolicyKind.Scholarship, _ => PolicyKind.PublicHealth };
            var urgency = report.Topic == AgentFactKind.FoodSupply ? 100 - Math.Clamp(report.Value, 0, 100) : report.Topic == AgentFactKind.Research ? 40 : Math.Clamp(report.Value, 0, 100);
            var relevance = policy == PolicyKind.FoodSecurity && report.ReportedProfession == Profession.Farmer ? 2 : policy == PolicyKind.Defense && report.ReportedProfession == Profession.Soldier ? 2 : policy == PolicyKind.Scholarship && report.ReportedProfession is Profession.Builder or Profession.Miner ? 1.8 : 1;
            var authority = institution.Kind switch
            {
                InstitutionKind.Monarchy => report.RepresentativeId == State.Nations.First(n => n.Id == town.NationId).RepresentativeId ? 5 : report.ReportedProfession == Profession.Soldier ? 2.5 : 0.6,
                InstitutionKind.GuildCouncil => report.ReportedProfession is Profession.Miner or Profession.Lumberjack or Profession.Builder ? 2.8 : 0.8,
                _ => 1 + GetCulture(town.CultureId).Cooperation * 0.35
            };
            var score = urgency * report.Confidence * relevance * authority / (1 + (State.Tick - report.ObservedTick) / 180d);
            scores[(int)policy] += score;
            if (score > largest[(int)policy]) { largest[(int)policy] = score; evidence[(int)policy] = report; }
        }
        var choice = Enumerable.Range(1, 4).OrderByDescending(i => scores[i]).ThenBy(i => i).First();
        var strongest = evidence[choice];
        if (scores[choice] < 5 || strongest is null) return;
        var changed = local.Kind != (PolicyKind)choice;
        local.Kind = (PolicyKind)choice; local.DecidedTick = State.Tick; local.EvidenceFactId = strongest.FactId; local.EvidenceObservedTick = strongest.ObservedTick;
        local.Reason = $"依据代表 {strongest.RepresentativeId} 送达的议题（观察于 {strongest.ObservedTick}，接收于 {strongest.ReceivedTick}），制度与职业相关权重合计 {scores[choice]:0.0}，采用{PolicyName(local.Kind)}";
        institution.LastDecision = local.Reason; institution.LastDecisionTick = State.Tick;
        if (town.Id == State.Nations.First(n => n.Id == town.NationId).CapitalId) State.Nations.First(n => n.Id == town.NationId).Decision = local.Reason;
        if (changed)
        {
            var policyEvent = AddEvent(WorldEventKind.Policy, $"{town.Name}议事决定采用{PolicyName(local.Kind)}。", town.X, town.Y, EventAction.Policy, town.Id, causeEventId: strongest.EventId, evidenceFactId: strongest.FactId);
            AddPublicFact(town, new AgentFact { Id = NewId(), EventId = policyEvent.Id, Kind = AgentFactKind.Policy, SubjectId = town.Id, X = town.X, Y = town.Y,
                Value = (int)local.Kind, ObservedTick = State.Tick, LearnedTick = State.Tick, OriginResidentId = town.RepresentativeId,
                OriginProfession = Profession.Representative, SourceResidentId = town.RepresentativeId, Text = local.Reason });
        }
    }

    public void RenameCulture(int cultureId, string name)
    {
        var culture = RequireCulture(cultureId); name = (name ?? "").Trim();
        if (name.Length is < 1 or > 40 || name.Any(char.IsControl)) throw new ArgumentException("文化名称须为 1–40 个可见字符。", nameof(name));
        culture.Name = name;
    }

    public void SetCultureValues(int cultureId, double cooperation, double innovation, double natureAffinity)
    {
        var culture = RequireCulture(cultureId);
        if (new[] { cooperation, innovation, natureAffinity }.Any(v => !double.IsFinite(v) || v is < 0 or > 1)) throw new ArgumentOutOfRangeException(nameof(cooperation), "文化参数须在 0 到 1 之间。");
        culture.Cooperation = cooperation; culture.Innovation = innovation; culture.NatureAffinity = natureAffinity;
    }

    public void SetNationCulture(int nationId, int cultureId) { _ = RequireCulture(cultureId); RequireNation(nationId).CultureId = cultureId; }
    public void SetResidentCulture(int residentId, int cultureId)
    {
        _ = RequireCulture(cultureId); var resident = State.Residents.FirstOrDefault(r => r.Id == residentId) ?? throw new ArgumentException("居民不存在。");
        resident.CultureId = cultureId;
    }

    public void ExchangeCulture(Resident first, Resident second)
    {
        if (Distance(first.X, first.Y, second.X, second.Y) > 2 || first.CultureId == second.CultureId) return;
        var firstCulture = first.CultureId; ObserveCulture(first, second.CultureId); ObserveCulture(second, firstCulture);
    }

    private void ObserveCulture(Resident resident, int cultureId)
    {
        if (resident.CultureId == cultureId || !State.Society.Cultures.Any(c => c.Id == cultureId)) return;
        var contact = State.Society.CulturalContacts.FirstOrDefault(c => c.ResidentId == resident.Id && c.CultureId == cultureId);
        if (contact is null) { contact = new CulturalContact { ResidentId = resident.Id, CultureId = cultureId, LastContactTick = -12 }; State.Society.CulturalContacts.Add(contact); }
        if (State.Tick - contact.LastContactTick < 12) return;
        contact.LastContactTick = State.Tick; contact.Exposure += 0.5 + resident.Agent.Personality.Sociability;
        if (contact.Exposure < 10) return;
        var previous = GetCulture(resident.CultureId).Name; resident.CultureId = cultureId;
        // A new identity requires fresh sustained contact before another conversion.
        foreach (var exposure in State.Society.CulturalContacts.Where(c => c.ResidentId == resident.Id))
        { exposure.Exposure = 0; exposure.LastContactTick = State.Tick; }
        RecordLife(resident, $"长期当面交流后，由{previous}转向{GetCulture(cultureId).Name}文化；种族与国籍未改变。");
        if (resident.History.Count > 24) resident.History.RemoveAt(0);
        var cultureEvent = AddEvent(WorldEventKind.Culture, $"{resident.Name}经长期交流转向{GetCulture(cultureId).Name}文化。", resident.X, resident.Y);
        cultureEvent.ResidentId = resident.Id; cultureEvent.NationId = resident.NationId;
    }

    public bool TryCastSpell(int casterId, SpellKind spell, int x, int y)
    {
        try { CastSpell(casterId, spell, x, y); return true; }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return false; }
    }

    public void CastSpell(int casterId, SpellKind spell, int x, int y)
    {
        if (!Enum.IsDefined(spell)) throw new ArgumentOutOfRangeException(nameof(spell));
        var caster = State.Residents.FirstOrDefault(r => r.Id == casterId) ?? throw new ArgumentException("施法居民不存在。");
        if (!InBounds(x, y) || Distance(caster.X, caster.Y, x, y) > 4) throw new InvalidOperationException("目标须位于施法者 4 格以内。");
        if (caster.Health <= 0 || caster.Age < 14 || caster.MagicTalent < 25 || caster.MagicTraining < 8) throw new InvalidOperationException("需要成年、魔法天赋至少 25 且完成至少 8 点奥术训练。");
        var cost = spell switch { SpellKind.Heal => 16d, SpellKind.HarvestBlessing => 25d, SpellKind.Shield => 22d, _ => 20d };
        cost *= caster.Race == RaceKind.Elf && spell == SpellKind.Heal || caster.Race == RaceKind.Dwarf && spell == SpellKind.Shield || caster.Race == RaceKind.Orc && spell == SpellKind.Ember ? 0.85 : 1;
        if (caster.Mana < cost) throw new InvalidOperationException($"法力不足：需要 {cost:0.#}，当前 {caster.Mana:0.#}。");
        Resident? recipient = null; Settlement? town = null;
        switch (spell)
        {
            case SpellKind.Heal:
                recipient = State.Residents.Where(r => r.NationId == caster.NationId && Distance(r.X, r.Y, x, y) <= 1 && (r.Health < 100 || r.SicknessTicks > 0)).OrderBy(r => r.Health).ThenBy(r => r.Id).FirstOrDefault();
                if (recipient is null) throw new InvalidOperationException("目标附近没有需要治疗的本国居民。");
                break;
            case SpellKind.HarvestBlessing:
            case SpellKind.Shield:
                town = State.Settlements.Where(s => s.NationId == caster.NationId && Distance(s.X, s.Y, x, y) <= 3).OrderBy(s => Distance(s.X, s.Y, x, y)).FirstOrDefault();
                if (town is null) throw new InvalidOperationException("目标附近没有可施加结界或丰饶祝福的本国聚落。");
                break;
            case SpellKind.Ember:
                recipient = State.Residents.Where(r => r.NationId != caster.NationId && Distance(r.X, r.Y, x, y) <= 1 && IsKnownHostile(caster, r.NationId)).OrderBy(r => r.Id).FirstOrDefault();
                if (recipient is null) throw new InvalidOperationException("目标附近没有正在交战的敌方居民。");
                break;
        }
        caster.Mana -= cost;
        var power = 0.7 + caster.MagicTalent / 150 + caster.MagicTraining / 250;
        if (spell == SpellKind.Heal) { recipient!.Health = Math.Min(100, recipient.Health + 22 * power); recipient.SicknessTicks = Math.Max(0, recipient.SicknessTicks - 15); }
        if (spell == SpellKind.HarvestBlessing) town!.FertilityBoostTicks = Math.Max(town.FertilityBoostTicks, (int)(50 * power));
        if (spell == SpellKind.Shield) town!.ShieldTicks = Math.Max(town.ShieldTicks, (int)(40 * power));
        if (spell == SpellKind.Ember) DamageResident(recipient!, TryAbsorbShieldDamage(recipient!, 18 * power), DeathCause.Magic);
        EmitVisual(spell switch { SpellKind.Heal => WorldVisualKind.Heal, SpellKind.HarvestBlessing => WorldVisualKind.Harvest,
            SpellKind.Shield => WorldVisualKind.Shield, _ => WorldVisualKind.Ember }, x, y, 2, caster.X, caster.Y);
        var detail = spell switch { SpellKind.Heal => "治疗", SpellKind.HarvestBlessing => "丰饶祝福", SpellKind.Shield => "守护结界", _ => "战斗火花" };
        caster.Agent.Decisions.Add(new AgentDecision { Tick = State.Tick, Goal = spell == SpellKind.Ember ? AgentGoalKind.Flee : AgentGoalKind.Work,
            Reason = $"在 {x},{y} 施放{detail}，消耗 {cost:0.#} 法力；天赋和训练决定效果" });
        if (caster.Agent.Decisions.Count > 6) caster.Agent.Decisions.RemoveAt(0);
        var magicEvent = AddEvent(WorldEventKind.Magic, $"{caster.Name}施放{detail}，消耗 {cost:0.#} 法力。", x, y);
        magicEvent.ResidentId = caster.Id; magicEvent.NationId = caster.NationId;
    }

    /// <returns>Damage remaining after a real local shield; does not change unrelated victims.</returns>
    public double TryAbsorbShieldDamage(Resident resident, double damage)
    {
        var multiplier = 1d;
        foreach (var town in State.Settlements)
        {
            if (town.NationId != resident.NationId || Distance(town.X, town.Y, resident.X, resident.Y) > 5) continue;
            var protection = (GetLocalPolicy(town.Id) == PolicyKind.Defense ? 0.88 : 1) * (town.ShieldTicks > 0 ? 0.6 : 1);
            multiplier = Math.Min(multiplier, protection);
        }
        return damage * multiplier;
    }

    public void ReconcileSocietyTopology()
    {
        EnsureTownCenters();
        var townIds = State.Settlements.Select(s => s.Id).ToHashSet(); var nationIds = State.Nations.Select(n => n.Id).ToHashSet();
        State.Society.Buildings.RemoveAll(b => !townIds.Contains(b.SettlementId) || !BuildingTerrainValid(b.Kind, State.Tiles[Index(b.X, b.Y)]) || b.Health <= 0 && b.Kind != BuildingKind.TownCenter);
        State.Society.Research.RemoveAll(r => !townIds.Contains(r.SettlementId));
        State.Society.Policies.RemoveAll(p => !townIds.Contains(p.SettlementId));
        State.Society.Institutions.RemoveAll(i => !nationIds.Contains(i.NationId));
        State.Society.Reports.RemoveAll(r => !townIds.Contains(r.RecipientSettlementId));
        var people = State.Residents.Select(r => r.Id).ToHashSet();
        State.Society.CulturalContacts.RemoveAll(c => !people.Contains(c.ResidentId));
        foreach (var building in State.Society.Buildings) building.Workers.RemoveAll(id => !people.Contains(id));
    }

    public void TickSociety()
    {
        if (State.Tick % 30 == 0) RefreshLocalRepresentatives();
        var townIds = State.Settlements.Select(s => s.Id).ToHashSet(); var nationIds = State.Nations.Select(n => n.Id).ToHashSet();
        State.Society.Research.RemoveAll(r => !townIds.Contains(r.SettlementId));
        State.Society.Policies.RemoveAll(p => !townIds.Contains(p.SettlementId));
        State.Society.Institutions.RemoveAll(i => !nationIds.Contains(i.NationId));
        State.Society.Reports.RemoveAll(r => !townIds.Contains(r.RecipientSettlementId) || State.Tick - r.ReceivedTick > 1_440);
        var liveResidents = State.Residents.Select(r => r.Id).ToHashSet();
        State.Society.CulturalContacts.RemoveAll(c => !liveResidents.Contains(c.ResidentId));
        foreach (var building in State.Society.Buildings)
        {
            if (!InBounds(building.X, building.Y) || !townIds.Contains(building.SettlementId)) { building.Health = 0; continue; }
            var tile = State.Tiles[Index(building.X, building.Y)];
            if (!BuildingTerrainValid(building.Kind, tile)) building.Health = 0;
            else if (tile.FireTicks > 0) building.Health = Math.Max(0, building.Health - 1.5 * BuildingFlammability(building));
            building.Workers.RemoveAll(id => !liveResidents.Contains(id));
        }
        var crossingCollapsed = RemoveFailedCrossings();
        State.Society.Buildings.RemoveAll(b => b.Health <= 0 && b.Kind != BuildingKind.TownCenter);
        if (crossingCollapsed) RelocateInvalidEntities();
        EnsureTownCenters();
        foreach (var town in State.Settlements)
        {
            if (town.FertilityBoostTicks > 0) town.FertilityBoostTicks--;
            if (town.ShieldTicks > 0) town.ShieldTicks--;
            if (State.Tick % 30 == 0) DecideLocalPolicy(town);
            if ((State.Tick + town.Id) % 60 == 0) PlanLocalDevelopment(town);
            if (GetLocalPolicy(town.Id) == PolicyKind.PublicHealth && town.Resources.Food >= 0.02)
            {
                var patient = _citizens.GetValueOrDefault(town.Id)?.Where(r => Distance(r.X, r.Y, town.X, town.Y) <= 2 && r.Health < 99).OrderBy(r => r.Health).FirstOrDefault();
                if (patient is not null) { town.Resources.Food -= 0.02; patient.Health = Math.Min(100, patient.Health + 0.15); }
            }
        }
        foreach (var person in State.Residents)
        {
            if (!InBounds(person.X, person.Y) || person.Health <= 0) continue;
            person.Mana = Math.Min(100, person.Mana + 0.025 * State.Rules.MagicRate * TerrainRules.For(State.Tiles[Index(person.X, person.Y)].Terrain).ManaRate * (0.5 + person.MagicTalent / 100));
            if (person.MagicTalent >= 25 && person.MagicTraining >= 8 && (State.Tick + person.Id) % 12 == 0) TryAutomaticMagic(person);
        }
        // Spread regeneration work across the map using a persisted clock, never wall time.
        const int batch = 128;
        for (var offset = 0; offset < Math.Min(batch, State.Tiles.Length); offset++)
        {
            var tile = State.Tiles[(int)((State.Tick * batch + offset) % State.Tiles.Length)];
            if (!State.Rules.ResourceRegeneration || !tile.IsWalkable || tile.FireTicks > 0) continue;
            var yields = TerrainRules.For(tile.Terrain);
            var renewal = (yields.FoodYield + yields.WoodYield) * (tile.DroughtTicks > 0 ? 0.2 : 1);
            if (tile.ResourceAmount < 100) tile.ResourceAmount = Math.Min(100, tile.ResourceAmount + renewal * 2);
        }
    }

    private void TryAutomaticMagic(Resident person)
    {
        if (person.Agent.Goal.Kind is AgentGoalKind.DeliverMessage or AgentGoalKind.Trade || person.Agent.Goal.PlayerDirected) return;
        var patient = State.Residents.Where(r => r.NationId == person.NationId && r.Health < 60 && Distance(person.X, person.Y, r.X, r.Y) <= 3).OrderBy(r => r.Health).ThenBy(r => r.Id).FirstOrDefault();
        if (patient is not null && (person.Agent.Personality.Sociability >= 0.3 || patient.Id == person.Id) && TryCastSpell(person.Id, SpellKind.Heal, patient.X, patient.Y)) return;
        if (person.ArmyId != 0 && person.Agent.Personality.Courage >= 0.35)
        {
            var enemy = State.Residents.FirstOrDefault(r => r.NationId != person.NationId && r.Health > 0 && Distance(person.X, person.Y, r.X, r.Y) <= 3 && IsKnownHostile(person, r.NationId));
            if (enemy is not null && TryCastSpell(person.Id, SpellKind.Ember, enemy.X, enemy.Y)) return;
        }
        if (!_settlements.TryGetValue(person.SettlementId, out var town) || Distance(person.X, person.Y, town.X, town.Y) > 4) return;
        if (GetLocalPolicy(town.Id) == PolicyKind.Defense && town.ShieldTicks < 6 && TryCastSpell(person.Id, SpellKind.Shield, town.X, town.Y)) return;
        if ((person.Profession is Profession.Farmer or Profession.Mage || GetCulture(person.CultureId).NatureAffinity >= 0.6)
            && town.FertilityBoostTicks < 6 && (person.Hunger > 30 || GetLocalPolicy(town.Id) == PolicyKind.FoodSecurity))
            TryCastSpell(person.Id, SpellKind.HarvestBlessing, town.X, town.Y);
    }

    private void RefreshLocalRepresentatives()
    {
        foreach (var town in State.Settlements)
        {
            if (State.Residents.Any(r => r.Id == town.RepresentativeId && r.SettlementId == town.Id && r.Health > 0 && r.Profession == Profession.Representative)) continue;
            var representative = State.Residents.Where(r => r.SettlementId == town.Id && r.Age >= 16 && r.Health > 0 && r.ArmyId == 0
                && r.Agent.DestinationSettlementId == 0 && Distance(r.X, r.Y, town.X, town.Y) <= 3)
                .OrderByDescending(r => r.Profession == Profession.Representative).ThenByDescending(r => r.Agent.Personality.Sociability)
                .ThenBy(r => r.Id).FirstOrDefault();
            town.RepresentativeId = representative?.Id ?? 0;
            if (representative is not null)
            {
                representative.Profession = Profession.Representative;
                representative.Agent.JobChangedTick = State.Tick;
            }
        }
        foreach (var nation in State.Nations)
            nation.RepresentativeId = State.Settlements.FirstOrDefault(s => s.Id == nation.CapitalId)?.RepresentativeId ?? 0;
    }

    private void PlanLocalDevelopment(Settlement town)
    {
        if (town.FoundationPending) return;
        town.LastDevelopmentTick = State.Tick;
        var local = State.Residents.Where(r => r.SettlementId == town.Id && r.Age >= 16 && r.Health > 50 && r.ArmyId == 0
            && Distance(r.X, r.Y, town.X, town.Y) <= 6 && r.Agent.DestinationSettlementId == 0).ToArray();
        var buildings = State.Society.Buildings.Where(b => b.SettlementId == town.Id).ToArray();
        var project = State.Society.Research.First(r => r.SettlementId == town.Id);
        if (local.Length < 4) { town.DevelopmentGoal = "恢复当地劳动力"; town.DevelopmentBlocker = "附近可工作的成年人少于 4 人"; return; }
        void Recruit(Profession job)
        {
            if (local.Any(r => r.Profession == job)) return;
            var recruit = local.Where(r => r.Profession is Profession.Builder or Profession.Farmer or Profession.Lumberjack or Profession.Miner or Profession.Scholar
                && !r.Agent.Goal.PlayerDirected && State.Tick - r.Agent.JobChangedTick >= 120
                && (r.Profession != Profession.Farmer || local.Count(p => p.Profession == Profession.Farmer) >= 4)
                && (job != Profession.Mage || r.MagicTalent >= 35))
                .OrderByDescending(r => r.Agent.Personality.Diligence).ThenBy(r => r.Id).FirstOrDefault();
            if (recruit is null) return;
            recruit.Profession = job; recruit.Agent.JobChangedTick = State.Tick; recruit.Agent.NextThinkTick = State.Tick;
            RecordLife(recruit, $"因家园发展需要，接受新的{ProfessionName(job)}岗位。");
            if (recruit.History.Count > 24) recruit.History.RemoveAt(0);
        }
        if (buildings.Any(b => !b.IsCompleted)) { Recruit(Profession.Builder); return; }
        if (project.ActiveProject.HasValue && buildings.Any(b => b.Kind == BuildingKind.Academy && b.IsCompleted))
        { Recruit(Profession.Scholar); return; }
        if (State.Society.MagicEnabled && buildings.Any(b => b.Kind == BuildingKind.ArcaneSanctum && b.IsCompleted)) Recruit(Profession.Mage);
        var lowFood = town.Resources.Food < Math.Max(25, town.Population);
        if (lowFood) Recruit(Profession.Farmer);
        string? blockedGoal = null, blockedReason = null;
        void RememberBlocker(string reason)
        {
            town.DevelopmentBlocker = reason;
            if (blockedGoal is not null) return;
            blockedGoal = town.DevelopmentGoal; blockedReason = reason;
        }
        bool PlanBuilding(BuildingKind kind)
        {
            var desired = kind == BuildingKind.Farm ? Math.Clamp((town.Population + 29) / 30, 1, 8) : 1;
            if (buildings.Count(b => b.Kind == kind) >= desired) return false;
            town.DevelopmentGoal = "修建" + BuildingName(kind);
            if (!State.Rules.Construction) { RememberBlocker("世界规则关闭了自主建设"); return false; }
            var missing = MissingResources(town.Resources, GetBuildingCost(kind));
            if (missing is not null)
            {
                RememberBlocker(missing + "；安排采集与实物运输");
                if (town.Resources.Wood < GetBuildingCost(kind).Wood) Recruit(Profession.Lumberjack);
                if (town.Resources.Stone < GetBuildingCost(kind).Stone || town.Resources.Ore < GetBuildingCost(kind).Ore) Recruit(Profession.Miner);
                return false;
            }
            var position = BestBuildingSite(town, kind);
            if (position < 0) { RememberBlocker("附近没有符合条件的建筑用地"); return false; }
            BuildFacility(town.Id, kind, position % State.Width, position / State.Width);
            Recruit(Profession.Builder); town.DevelopmentBlocker = "材料已备齐，等待工人到场";
            return true;
        }
        if (lowFood)
        {
            if (buildings.Count(b => b.Kind == BuildingKind.Farm) < Math.Clamp((town.Population + 29) / 30, 1, 8)) { PlanBuilding(BuildingKind.Farm); return; }
            town.DevelopmentGoal = "稳定粮食供给";
            town.DevelopmentBlocker = $"库存 {town.Resources.Food:0}，目标 {Math.Max(25, town.Population):0}；农民采集并带回粮仓";
            return;
        }
        if (!buildings.Any(b => b.Kind == BuildingKind.Academy)) { PlanBuilding(BuildingKind.Academy); return; }
        foreach (var plan in PendingLocalDevelopment(town, buildings, project))
        {
            var stage = plan.Research is { } researchKind ? AdvancementRules.For(researchKind) : plan.Facility is { } buildingKind ? AdvancementRules.For(buildingKind) : null;
            var isFoundation = plan.Research is ResearchKind.Agriculture or ResearchKind.Logistics;
            if (plan.Facility is { } facility)
            {
                if (PlanBuilding(facility)) return;
                if (stage is not null) break;
                continue;
            }
            var kind = plan.Research!.Value;
            town.DevelopmentGoal = "研究" + ResearchName(kind);
            if (!State.Rules.Research) { RememberBlocker("世界规则关闭了自主研究"); continue; }
            var missing = MissingResources(town.Resources, GetResearchCost(kind));
            if (missing is not null)
            {
                RememberBlocker(missing);
                if (town.Resources.Wood < GetResearchCost(kind).Wood) Recruit(Profession.Lumberjack);
                if (town.Resources.Stone < GetResearchCost(kind).Stone || town.Resources.Ore < GetResearchCost(kind).Ore) Recruit(Profession.Miner);
                if (stage is not null || isFoundation) break;
                continue;
            }
            StartResearch(town.Id, kind); Recruit(Profession.Scholar); town.DevelopmentBlocker = "等待学者到学舍工作"; return;
        }
        if (blockedGoal is not null)
        {
            town.DevelopmentGoal = blockedGoal; town.DevelopmentBlocker = blockedReason!;
            return;
        }
        if (PlanBuildingUpgrade(town, buildings)) return;
        town.DevelopmentGoal = State.Rules.Expansion ? "积累物资，建立新聚落" : "维持繁荣与对外交流";
        town.DevelopmentBlocker = State.Rules.Expansion ? $"拓荒条件：人口 {town.Population}/80；需要送达的建村勘察报告、相距至少 {MinimumSettlementDistance} 格的用地与携带补给的拓荒者；费用 {AdvancementRules.Stock(VillageFoundingCost)}" : "已有研究完成；扩张已关闭";
    }

    private IEnumerable<LocalDevelopmentPlan> PendingLocalDevelopment(Settlement town, Building[] buildings, SettlementResearch project)
    {
        if (!buildings.Any(b => b.Kind == BuildingKind.Academy))
        {
            yield return new(BuildingKind.Academy, null, GetBuildingCost(BuildingKind.Academy));
            yield break;
        }
        var focus = GetDevelopmentFocus(town.Id);
        var technology = focus is DevelopmentFocus.Technology or DevelopmentFocus.Integrated;
        var magic = State.Society.MagicEnabled && focus is DevelopmentFocus.MagicPractice or DevelopmentFocus.ArcaneIndustry or DevelopmentFocus.Integrated;
        var magicalIndustry = magic && focus is DevelopmentFocus.ArcaneIndustry or DevelopmentFocus.Integrated;
        // Complete the next usable stage before spending its materials on optional networks.
        // Natural magic develops practitioners and sanctums without requiring crystal industry.
        var foundations = magic && !technology
            ? new[] { ResearchKind.Agriculture, ResearchKind.ArcaneArts, ResearchKind.Logistics }
            : new[] { ResearchKind.Agriculture, ResearchKind.Logistics };
        if (!project.ActiveProject.HasValue)
            foreach (var kind in foundations)
                if (!HasResearch(town.Id, kind) && ResearchPrerequisiteError(town.Id, kind) is null)
                    yield return new(null, kind, GetResearchCost(kind));
        var route = AdvancementRules.All.Where(a => a.Magic ? magicalIndustry : technology).ToArray();
        foreach (var advancement in route)
        {
            if (HasResearch(town.Id, advancement.Research))
            {
                if (!buildings.Any(b => b.Kind == advancement.Facility))
                    yield return new(advancement.Facility, null, GetBuildingCost(advancement.Facility));
                continue;
            }
            if (!project.ActiveProject.HasValue && ResearchPrerequisiteError(town.Id, advancement.Research) is null)
                yield return new(null, advancement.Research, GetResearchCost(advancement.Research));
        }
        var facilities = new[]
        {
            (BuildingKind.Infirmary, GetLocalPolicy(town.Id) == PolicyKind.PublicHealth),
            (BuildingKind.ArcaneSanctum, magic && HasResearch(town.Id, ResearchKind.ArcaneArts)),
            (BuildingKind.Waystation, HasResearch(town.Id, ResearchKind.Logistics)),
            (BuildingKind.SignalTower, technology && HasResearch(town.Id, ResearchKind.SignalNetwork) && HasResearch(town.Id, ResearchKind.Electrification)),
            (BuildingKind.Dock, HasResearch(town.Id, ResearchKind.Logistics)),
            (BuildingKind.Shipyard, HasResearch(town.Id, ResearchKind.Logistics)),
            (BuildingKind.LumberCamp, town.Population >= 24),
            (BuildingKind.Quarry, town.Population >= 24),
            (BuildingKind.Well, town.Resources.Water < town.Population * 2),
            (BuildingKind.Granary, town.Population >= 40),
            (BuildingKind.Housing, town.Population >= 30),
            (BuildingKind.Market, town.Tier >= SettlementTier.Town),
            (BuildingKind.Watchtower, town.Tier >= SettlementTier.Town)
        };
        foreach (var (kind, needed) in facilities)
            if (needed && !buildings.Any(b => b.Kind == kind)) yield return new(kind, null, GetBuildingCost(kind));
        foreach (var kind in Enum.GetValues<BuildingKind>())
            if (BuildingRace(kind) is not null && CanBuildRacialFacility(town.Id, kind) && !buildings.Any(b => b.Kind == kind)
                && (kind != BuildingKind.DwarvenForge || HasResearch(town.Id, ResearchKind.Industry))
                && (kind != BuildingKind.SacredGrove || magic && HasResearch(town.Id, ResearchKind.ArcaneArts)))
                yield return new(kind, null, GetBuildingCost(kind));
        if (project.ActiveProject.HasValue) yield break;
        foreach (var kind in technology ? new[] { ResearchKind.SignalNetwork }.Concat(magic ? new[] { ResearchKind.ArcaneArts } : []) : [])
            if (!HasResearch(town.Id, kind) && ResearchPrerequisiteError(town.Id, kind) is null)
                yield return new(null, kind, GetResearchCost(kind));
    }

    private ResourceStock LocalDevelopmentReserve(Settlement town)
    {
        var buildings = State.Society.Buildings.Where(b => b.SettlementId == town.Id).ToArray();
        var project = State.Society.Research.First(r => r.SettlementId == town.Id);
        if (town.Resources.Food < Math.Max(25, town.Population) || buildings.Any(b => !b.IsCompleted)
            || project.ActiveProject.HasValue && buildings.Any(b => b.Kind == BuildingKind.Academy && b.IsCompleted)) return new();
        var plans = PendingLocalDevelopment(town, buildings, project)
            .Where(p => p.Facility.HasValue ? State.Rules.Construction : State.Rules.Research).ToArray();
        // Reserve wood and stone for the next project the local planner can pursue; optional ore
        // shortages may defer magic while the same planner proceeds with basic transport.
        return plans.FirstOrDefault()?.Cost ?? new();
    }

    private static void ValidateSocietyState(WorldState state)
    {
        static void Need(bool condition, string message) { if (!condition) throw new ArgumentException("无效存档：" + message); }
        static bool Range(double value, double max) => double.IsFinite(value) && value >= 0 && value <= max;
        static bool Text(string? value, int max = 1000) => value is not null && value.Length <= max && !value.Any(c => char.IsControl(c) && c != '\n');
        var society = state.Society;
        Need(society is not null, "缺少社会状态。");
        Need(society!.Cultures is { Count: > 0 and <= 64 } && society.Buildings is not null && society.Buildings.Count <= MaxBuildings
            && society.Research is not null && society.Research.Count <= 256 && society.Policies is not null && society.Policies.Count <= 256
            && society.Institutions is not null && society.Institutions.Count <= 64 && society.Reports is not null && society.Reports.Count <= 2_048
            && society.CulturalContacts is not null && society.CulturalContacts.Count <= MaxPopulation * 64, "社会记录数量或集合无效。");
        var cultureIds = new HashSet<int>();
        foreach (var culture in society.Cultures!) Need(culture is not null && culture.Id is > 0 and <= 100_000 && cultureIds.Add(culture.Id)
            && Text(culture.Name, 40) && culture.Name.Length > 0 && Range(culture.Cooperation, 1) && Range(culture.Innovation, 1) && Range(culture.NatureAffinity, 1), "文化定义无效。");
        var towns = state.Settlements.ToDictionary(s => s.Id); var nations = state.Nations.Select(n => n.Id).ToHashSet(); var residents = state.Residents.Select(r => r.Id).ToHashSet();
        foreach (var person in state.Residents) Need(cultureIds.Contains(person.CultureId), "居民文化引用无效。");
        foreach (var town in state.Settlements) Need(cultureIds.Contains(town.CultureId), "聚落文化引用无效。");
        foreach (var nation in state.Nations) Need(cultureIds.Contains(nation.CultureId), "国家文化引用无效。");
        var buildingIds = new HashSet<int>(); var occupied = new HashSet<(int, int)>();
        var entityIds = state.Residents.Select(r => r.Id).Concat(state.Nations.Select(n => n.Id)).Concat(state.Settlements.Select(s => s.Id)).Concat(state.Armies.Select(a => a.Id)).ToHashSet();
        foreach (var building in society.Buildings!)
            Need(building is not null && building.Id > 0 && building.Id < state.NextId && buildingIds.Add(building.Id) && !entityIds.Contains(building.Id)
                && towns.ContainsKey(building.SettlementId) && Enum.IsDefined(building.Kind) && building.X >= 0 && building.Y >= 0 && building.X < state.Width && building.Y < state.Height
                && occupied.Add((building.X, building.Y)) && Range(building.Health, 100) && Range(building.ConstructionProgress, 10_000)
                && building.ConstructionRequired > 0 && building.ConstructionRequired <= 10_000 && building.ConstructionProgress <= building.ConstructionRequired
                && building.Level is >= 1 and <= 3 && Enum.IsDefined(building.Direction)
                && (building.PendingDirection is null || building.Kind == BuildingKind.Bridge && Enum.IsDefined(building.PendingDirection.Value))
                && Range(building.UpgradeProgress, 10_000) && Range(building.UpgradeRequired, 10_000) && building.UpgradeProgress <= building.UpgradeRequired
                && (building.UpgradeRequired == 0 ? building.PendingDirection is null : building.ConstructionProgress >= building.ConstructionRequired && (building.PendingDirection.HasValue || building.Level < 3))
                && building.ProductionBatches is >= 0 and <= 1_000_000_000
                && building.WorkSlots is > 0 and <= 20 && building.Workers is not null && building.Workers.Count <= building.WorkSlots
                && building.Workers.Distinct().Count() == building.Workers.Count && building.Workers.All(residents.Contains)
                && building.LastWorkedTick >= -100 && building.LastWorkedTick <= state.Tick, "设施状态或引用无效。");
        var researchTowns = new HashSet<int>();
        foreach (var research in society.Research!)
            Need(research is not null && towns.ContainsKey(research.SettlementId) && researchTowns.Add(research.SettlementId)
                && (research.ActiveProject is null || Enum.IsDefined(research.ActiveProject.Value)) && Range(research.Progress, 10_000) && Range(research.RequiredProgress, 10_000)
                && research.Completed is not null && research.Completed.Count <= Enum.GetValues<ResearchKind>().Length && research.Completed.All(r => Enum.IsDefined(r))
                && research.Completed.Distinct().Count() == research.Completed.Count && (!research.ActiveProject.HasValue || !research.Completed.Contains(research.ActiveProject.Value))
                && (!research.ActiveProject.HasValue || research.RequiredProgress > 0 && research.Progress < research.RequiredProgress), "研究状态无效。");
        Need(researchTowns.Count == towns.Count, "聚落研究状态缺失。");
        var policyTowns = new HashSet<int>();
        foreach (var policy in society.Policies!) Need(policy is not null && towns.ContainsKey(policy.SettlementId) && policyTowns.Add(policy.SettlementId)
            && Enum.IsDefined(policy.Kind) && policy.DecidedTick >= 0 && policy.DecidedTick <= state.Tick && policy.EvidenceObservedTick >= 0 && policy.EvidenceObservedTick <= state.Tick
            && Text(policy.Reason), "政策状态无效。");
        Need(policyTowns.Count == towns.Count, "聚落政策状态缺失。");
        var institutionNations = new HashSet<int>();
        foreach (var institution in society.Institutions!) Need(institution is not null && nations.Contains(institution.NationId) && institutionNations.Add(institution.NationId)
            && Enum.IsDefined(institution.Kind) && (!institution.PlayerPolicy.HasValue || Enum.IsDefined(institution.PlayerPolicy.Value))
            && institution.LastDecisionTick >= 0 && institution.LastDecisionTick <= state.Tick && Text(institution.LastDecision), "制度状态无效。");
        Need(institutionNations.Count == nations.Count, "国家制度状态缺失。");
        var reports = new HashSet<(int, int)>();
        foreach (var report in society.Reports!) Need(report is not null && towns.ContainsKey(report.RecipientSettlementId) && report.FactId > 0 && report.FactId < state.NextId
            && reports.Add((report.RecipientSettlementId, report.FactId)) && Enum.IsDefined(report.Topic) && Enum.IsDefined(report.ReportedProfession)
            && double.IsFinite(report.Value) && Math.Abs(report.Value) <= 1_000_000_000 && Range(report.Confidence, 1)
            && report.ObservedTick >= 0 && report.ObservedTick <= report.ReceivedTick && report.ReceivedTick <= state.Tick, "机构报告无效。");
        var contacts = new HashSet<(int, int)>();
        foreach (var contact in society.CulturalContacts!) Need(contact is not null && residents.Contains(contact.ResidentId) && cultureIds.Contains(contact.CultureId)
            && contacts.Add((contact.ResidentId, contact.CultureId)) && Range(contact.Exposure, 20) && contact.LastContactTick >= -12 && contact.LastContactTick <= state.Tick, "文化交流状态无效。");
    }

    private void AddPublicFact(Settlement town, AgentFact fact)
    {
        if (town.PublicKnowledge.Any(f => f.Id == fact.Id)) return;
        var prior = town.PublicKnowledge.FirstOrDefault(f => f.Kind == fact.Kind && f.SubjectId == fact.SubjectId && f.TargetNationId == fact.TargetNationId
            && (fact.Kind is not (AgentFactKind.Danger or AgentFactKind.Personal) || f.X == fact.X && f.Y == fact.Y));
        if (prior is not null)
        {
            if (prior.ObservedTick >= fact.ObservedTick) return;
            town.PublicKnowledge.Remove(prior);
        }
        town.PublicKnowledge.Add(fact);
        while (town.PublicKnowledge.Count > 24) town.PublicKnowledge.RemoveAt(0);
    }
    private static void AddResidentFact(Resident resident, AgentFact fact)
    {
        if (resident.Agent.Memory.Any(f => f.Id == fact.Id)) return;
        resident.Agent.Memory.Add(fact);
        if (resident.Agent.Memory.Count > 16) resident.Agent.Memory.RemoveAt(0);
    }
    private static void Spend(ResourceStock stock, ResourceStock cost)
    {
        if (MissingResources(stock, cost) is not null)
            throw new InvalidOperationException("当地材料不足；" + MissingResources(stock, cost));
        stock.Food = Math.Max(0, stock.Food - cost.Food); stock.Wood = Math.Max(0, stock.Wood - cost.Wood);
        stock.Stone = Math.Max(0, stock.Stone - cost.Stone); stock.Ore = Math.Max(0, stock.Ore - cost.Ore);
        stock.Alloy = Math.Max(0, stock.Alloy - cost.Alloy); stock.EnergyCells = Math.Max(0, stock.EnergyCells - cost.EnergyCells);
        stock.Crystals = Math.Max(0, stock.Crystals - cost.Crystals);
        foreach (var kind in MineralAndVehicleResources) stock.Set(kind, Math.Max(0, stock.Get(kind) - cost.Get(kind)));
    }
    private Settlement RequireTown(int id) => _settlements.TryGetValue(id, out var town) ? town : throw new ArgumentException("聚落不存在。");
    private Nation RequireNation(int id) => _nations.TryGetValue(id, out var nation) ? nation : throw new ArgumentException("国家不存在。");
    private CultureDefinition RequireCulture(int id) => State.Society.Cultures.FirstOrDefault(c => c.Id == id) ?? throw new ArgumentException("文化不存在。");
    private CultureDefinition GetCulture(int id) => State.Society.Cultures.FirstOrDefault(c => c.Id == id) ?? State.Society.Cultures[0];
    public static string BuildingName(BuildingKind kind) => kind switch { BuildingKind.Shipyard => "船坞", BuildingKind.Dock => "码头", BuildingKind.LumberCamp => "林场", BuildingKind.Quarry => "采石场", BuildingKind.Well => "水井", BuildingKind.Granary => "粮仓", BuildingKind.Housing => "住宅", BuildingKind.Market => "集市", BuildingKind.Watchtower => "瞭望塔", BuildingKind.TownCenter => "城镇中心", BuildingKind.Farm => "农田", BuildingKind.Workshop => "工坊", BuildingKind.Academy => "学舍", BuildingKind.Waystation => "驿站", BuildingKind.SignalTower => "无线信号塔", BuildingKind.Bridge => "桥梁", BuildingKind.MountainPass => "山路", BuildingKind.ArcaneSanctum => "奥术研习所", BuildingKind.Infirmary => "医馆", BuildingKind.AssemblyHall => "议事厅", BuildingKind.TradeGuild => "商贸公会", BuildingKind.SacredGrove => "精灵圣林", BuildingKind.HerbGarden => "草药园", BuildingKind.MiningHall => "矮人矿业工坊", BuildingKind.HuntingCamp => "兽人狩猎营", BuildingKind.WarDrum => "战鼓营", _ => AdvancementRules.For(kind)?.FacilityName ?? kind.ToString() };
    public static string ResearchName(ResearchKind kind) => kind switch { ResearchKind.Agriculture => "农业改良", ResearchKind.Logistics => "驿路运输", ResearchKind.SignalNetwork => "信号网络", ResearchKind.ArcaneArts => "奥术基础", _ => AdvancementRules.For(kind)?.Name ?? kind.ToString() };
    public static string PolicyName(PolicyKind kind) => kind switch { PolicyKind.FoodSecurity => "粮食保障", PolicyKind.Defense => "防务优先", PolicyKind.Scholarship => "求知兴学", PolicyKind.PublicHealth => "公共医疗", _ => "均衡发展" };
}
