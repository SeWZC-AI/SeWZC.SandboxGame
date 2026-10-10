using SeWZC.WorldBox.Core.Runtime;

namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    private static readonly Profession[] WorkforcePriorities =
    [
        Profession.Physician, Profession.Firefighter, Profession.Builder, Profession.Fisher,
        Profession.Lumberjack, Profession.Miner, Profession.Engineer, Profession.Messenger,
        Profession.Scholar, Profession.Mage, Profession.Trader, Profession.Ranger,
        Profession.Archivist, Profession.Battlemage, Profession.Surveyor, Profession.Gardener, Profession.Farmer,
    ];

    private void BalanceLocalWorkforce()
    {
        Span<int> counts = stackalloc int[(int)Profession.Laborer + 1];
        Span<int> targets = stackalloc int[counts.Length];
        Span<int> facilityTargets = stackalloc int[counts.Length];
        foreach (var town in Settlements)
        {
            if (town.Value.FoundationPending || !_citizens.TryGetValue(town.Value.Id, out var citizens))
                continue;
            counts.Clear();
            targets.Clear();
            facilityTargets.Clear();
            var adults = new List<StateReference<Resident>>();
            var buildings = new List<StateReference<Building>>();
            var patients = 0;
            var dailyFood = 0d;
            foreach (var person in citizens)
            {
                if (person.Value.Health <= 0)
                    continue;
                dailyFood += FoodUse(person);
                if (Distance(person.Value.X, person.Value.Y, town.Value.X, town.Value.Y) <= 6
                    && (person.Value.Health < 90 || person.Value.SicknessTicks > 0))
                    patients++;
                if (person.Value.Age < 16 || person.Value.ArmyId != 0)
                    continue;
                adults.Add(person);
                counts[(int)person.Value.Profession]++;
            }

            if (adults.Count == 0)
                continue;
            var fieldYield = 0d;
            var fields = 0;
            var timber = 0;
            var stone = 0;
            var fishingSites = 0;
            var fishingYield = 0d;
            var plots = new List<NaturalWorkPlot>();
            var mountainWorkers = adults.Any(person => person.Value.Race == RaceKind.Dwarf);
            foreach (var index in Circle(town.Value.X, town.Value.Y, 6))
            {
                var tile = Tiles[index];
                var yield = PlantSiteYield(tile, false);
                if (NaturalWorkPlotAvailable(index, Profession.Farmer, town))
                {
                    fieldYield += .7 * yield;
                    fields++;
                    plots.Add(new NaturalWorkPlot(index, Profession.Farmer));
                }

                if (NaturalWorkPlotAvailable(index, Profession.Lumberjack, town))
                {
                    timber++;
                    plots.Add(new NaturalWorkPlot(index, Profession.Lumberjack));
                }

                if (NaturalWorkPlotAvailable(index, Profession.Miner, town)
                    && (tile.Value.IsWalkable || mountainWorkers))
                {
                    stone++;
                    plots.Add(new NaturalWorkPlot(index, Profession.Miner));
                }

                if (IsFreshWater(tile.Value) && EdibleAnimal(tile, true) is var fish && fish != WildlifeKind.None
                    && WildlifeHarvestEfficiency(tile, fish) >= .25)
                {
                    fishingSites++;
                    fishingYield += .15 * WildlifeHarvestEfficiency(tile, fish) * AnimalRules.For(fish).BodyMass;
                }
            }

            var foodDeficit = Math.Max(0, dailyFood * 30 - town.Value.Resources.Food);
            var foodWorkers = fields == 0
                ? 0
                : Math.Min(fields,
                    (int)Math.Ceiling((dailyFood + foodDeficit / 30) / Math.Max(.04, fieldYield / fields)));
            targets[(int)Profession.Farmer] = foodWorkers;
            if (foodDeficit > 0 && fishingSites > 0)
            {
                targets[(int)Profession.Fisher] = Math.Min(fishingSites,
                    (int)Math.Ceiling((dailyFood + foodDeficit / 30) / Math.Max(.04, fishingYield / fishingSites)));
            }

            targets[(int)Profession.Lumberjack] = Math.Min(timber,
                (int)Math.Ceiling(Math.Max(0, 60 - town.Value.Resources.Wood) / 12));
            targets[(int)Profession.Miner] = Math.Min(stone,
                (int)Math.Ceiling(LocalMineralDeficit(town) / 12));
            targets[(int)Profession.Representative] = town.Value.RepresentativeId == 0 ? 0 : 1;
            if (Rules.Expansion && SettlementNeedsClaimArea(town))
            {
                targets[(int)Profession.Builder] = Math.Max(1, (GetSettlementExpansionArea(town.Value.Id)
                    - GetSettlementArea(town.Value.Id) + 9) / 10);
            }

            var contacts = town.Value.PublicKnowledge.Count(f => f.Kind == AgentFactKind.SettlementLocation
                                                           && f.SubjectId != town.Value.Id &&
                                                           f.ReliabilityAt(SimulationTick) >= .5);
            targets[(int)Profession.Messenger] = Math.Min(contacts, (adults.Count + 79) / 80);
            targets[(int)Profession.Trader] = town.Value.Resources.Food > dailyFood * 30 ? Math.Min(contacts, 3) : 0;
            var researching =
                Society.Research.Any(r => r.SettlementId == town.Value.Id && r.ActiveProject is not null);
            foreach (var building in Buildings)
            {
                if (building.Value.SettlementId != town.Value.Id || building.Value.Health <= 0 || !building.Value.Enabled
                    || Distance(building.Value.X, building.Value.Y, town.Value.X, town.Value.Y) > 8)
                    continue;
                buildings.Add(building);
                if (!building.Value.IsCompleted || building.Value.IsUpgrading || building.Value.Health < 50)
                {
                    targets[(int)Profession.Builder] += building.Value.WorkSlots;
                    continue;
                }

                var job = WorkplaceProfession(building.Value.Kind);
                if (job is null || (job == Profession.Scholar && !researching)
                                || (job == Profession.Fisher && foodDeficit <= 0)
                                || (job == Profession.Physician && patients == 0))
                    continue;
                var unlock = ResearchRules.Unlocking(job.Value);
                if (unlock is not null && !HasResearch(town.Value.Id, unlock))
                    continue;
                facilityTargets[(int)job.Value] += job == Profession.Physician
                    ? Math.Min(building.Value.WorkSlots, (patients + 2) / 3)
                    : building.Value.WorkSlots;
            }

            for (var job = 0; job < targets.Length; job++)
                targets[job] = Math.Max(targets[job], facilityTargets[job]);
            foreach (var job in WorkforcePriorities)
                while (counts[(int)job] < targets[(int)job])
                {
                    StateReference<Resident>? recruit = null;
                    var best = double.NegativeInfinity;
                    foreach (var person in adults)
                    {
                        var oldJob = person.Value.Profession;
                        if (oldJob is Profession.Representative or Profession.Soldier || oldJob == job
                            || counts[(int)oldJob] <= targets[(int)oldJob]
                            || !AvailableForLocalAssignment(person, town) || !SuitableForProfession(person, job))
                            continue;
                        var fit = ProfessionSuitability(person, job);
                        if (fit > best || (fit == best && person.Value.Id < recruit!.Value.Id))
                        {
                            recruit = person;
                            best = fit;
                        }
                    }

                    if (recruit is null)
                        break;
                    counts[(int)recruit.Value.Profession]--;
                    counts[(int)job]++;
                    ChangeLocalProfession(recruit, job);
                }

            // 没有专业工位的人参与临时劳动，不能把所有多余职业都改成农民。
            foreach (var person in adults)
            {
                var job = person.Value.Profession;
                if (job is Profession.Child or Profession.Laborer or Profession.Soldier
                    || person.Value.Id == town.Value.RepresentativeId || counts[(int)job] <= targets[(int)job]
                    || !AvailableForLocalAssignment(person, town))
                    continue;
                counts[(int)job]--;
                counts[(int)Profession.Laborer]++;
                ChangeLocalProfession(person, Profession.Laborer);
            }

            AssignLocalWorkplaces(town, adults, buildings);
            AssignNaturalWorkAreas(town, adults, plots);
        }
    }

    private bool AvailableForLocalAssignment(StateReference<Resident> person, StateReference<Settlement> town)
    {
        return person.Value.Health >= 60 && person.Value.SicknessTicks == 0 && person.Value.ArmyId == 0
               && !person.Value.Agent.Goal.PlayerDirected && person.Value.Agent.DestinationSettlementId == 0
               && person.Value.TravelMode == TravelMode.Foot && Distance(person.Value.X, person.Value.Y, town.Value.X, town.Value.Y) <= 3
               && SimulationTick - person.Value.MoveStartedTick >= person.Value.MoveDurationTicks
               && (person.Value.Profession == Profession.Laborer || SimulationTick == 0 ||
                   SimulationTick - person.Value.Agent.JobChangedTick >= SimulationTime.TicksPerYear);
    }

    private static bool SuitableForProfession(StateReference<Resident> person, Profession job)
    {
        return job is not (Profession.Mage or Profession.Battlemage or Profession.Gardener) || person.Value.MagicTalent >= 35;
    }

    private static double ProfessionSuitability(StateReference<Resident> person, Profession job)
    {
        var personality = person.Value.Agent.Personality;
        return job switch
        {
            Profession.Trader or Profession.Messenger => personality.Sociability * 2 + personality.Diligence,
            Profession.Mage or Profession.Battlemage or Profession.Gardener => person.Value.MagicTalent / 25
                                                                               + person.Value.MagicTraining / 10 +
                                                                               personality.Diligence,
            Profession.Firefighter or Profession.Ranger => personality.Courage + personality.Diligence
                                                                               + person.Value.Health / 100,
            Profession.Lumberjack => personality.Diligence * 2 + (person.Value.Race == RaceKind.Elf ? .5 : 0),
            Profession.Miner or Profession.Engineer => personality.Diligence * 2 +
                                                       (person.Value.Race == RaceKind.Dwarf ? .5 : 0),
            Profession.Scholar or Profession.Archivist or Profession.Surveyor => personality.Diligence +
                personality.Ambition,
            _ => personality.Diligence * 2 + person.Value.Health / 100,
        };
    }

    private void ChangeLocalProfession(StateReference<Resident> person, Profession job)
    {
        person.Replace(person.Value with
        {
            Profession = job,
            Agent = person.Value.Agent with
            {
                JobChangedTick = SimulationTick,
                WorkplaceId = 0,
                WorkAreaIndex = -1,
                NextThinkTick = SimulationTick,
                DaytimeGoal = null,
                Goal = new AgentGoal
                {
                    Kind = AgentGoalKind.Idle,
                    TargetX = person.Value.X,
                    TargetY = person.Value.Y,
                    Reason = $"因本地供给缺口和可用工位，接受{ProfessionName(job)}分工",
                },
            },
        });
    }

    private static Profession? WorkplaceProfession(BuildingKind kind)
    {
        return PreferredExpansionJob(kind) ?? kind switch
        {
            BuildingKind.Farm or BuildingKind.AutomatedFarm or BuildingKind.RunicGarden or BuildingKind.Pasture =>
                Profession.Farmer,
            BuildingKind.LumberCamp => Profession.Lumberjack,
            BuildingKind.Quarry or BuildingKind.MiningHall => Profession.Miner,
            BuildingKind.Academy => Profession.Scholar,
            BuildingKind.ArcaneSanctum or BuildingKind.SacredGrove => Profession.Mage,
            BuildingKind.Infirmary => Profession.Physician,
            BuildingKind.Dock or BuildingKind.Aquaculture => Profession.Fisher,
            _ => null,
        };
    }

    private void AssignLocalWorkplaces(StateReference<Settlement> town, List<StateReference<Resident>> adults,
        List<StateReference<Building>> buildings)
    {
        var occupied = new Dictionary<int, int>();
        foreach (var person in adults)
            if (person.Value.Agent.WorkplaceId != 0)
                occupied[person.Value.Agent.WorkplaceId] = occupied.GetValueOrDefault(person.Value.Agent.WorkplaceId) + 1;
        foreach (var person in adults)
        {
            // 工作地点稳定；旧岗位失效时只有本人回到家园，才重新接受当地安排。
            if (person.Value.Health < 60 || person.Value.SicknessTicks > 0 || person.Value.Agent.Goal.PlayerDirected
                || person.Value.Agent.DestinationSettlementId != 0 || Distance(person.Value.X, person.Value.Y, town.Value.X, town.Value.Y) > 3
                || person.Value.TravelMode != TravelMode.Foot ||
                SimulationTick - person.Value.MoveStartedTick < person.Value.MoveDurationTicks)
                continue;
            var previous = person.Value.Agent.WorkplaceId;
            if (buildings.Any(b => b.Value.Id == previous && WorkplaceFits(b.Value, person)
                                                    && occupied.GetValueOrDefault(previous) <= b.Value.WorkSlots))
                continue;
            if (previous != 0)
                occupied[previous]--;
            StateReference<Building>? selected = null;
            var bestDistance = int.MaxValue;
            foreach (var building in buildings)
            {
                if (!WorkplaceFits(building.Value, person) || occupied.GetValueOrDefault(building.Value.Id) >= building.Value.WorkSlots)
                    continue;
                var distance = Distance(person.Value.X, person.Value.Y, building.Value.X, building.Value.Y);
                if (distance < bestDistance || (distance == bestDistance && building.Value.Id < selected!.Value.Id))
                {
                    selected = building;
                    bestDistance = distance;
                }
            }

            var next = selected?.Value.Id ?? 0;
            if (next == previous)
                continue;
            if (next != 0)
                occupied[next] = occupied.GetValueOrDefault(next) + 1;
            if (person.Value.Agent.WorkplaceId != next)
                person.Replace(person.Value.WithAgent(person.Value.Agent with { WorkplaceId = next }));
        }
    }

    private static bool WorkplaceFits(Building building, StateReference<Resident> person)
    {
        return building.Enabled && building.Health > 0 &&
               (BuildingRace(building.Kind) is not { } race || race == person.Value.Race)
               && (building.IsCompleted && !building.IsUpgrading && building.Health >= 50
                   ? WorkplaceProfession(building.Kind) == person.Value.Profession
                   : person.Value.Profession is Profession.Builder or Profession.Engineer);
    }
}
