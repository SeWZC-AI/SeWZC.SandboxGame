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
        foreach (var town in Current.Settlements)
        {
            if (town.FoundationPending || !_citizens.TryGetValue(town.Id, out var citizens)) continue;
            counts.Clear();
            targets.Clear();
            facilityTargets.Clear();
            var adults = new List<ResidentCursor>();
            var buildings = new List<BuildingCursor>();
            var patients = 0;
            var dailyFood = 0d;
            foreach (var person in citizens)
            {
                if (person.Health <= 0) continue;
                dailyFood += FoodUse(person);
                if (person.Age < 16 || person.ArmyId != 0) continue;
                adults.Add(person);
                counts[(int)person.Profession]++;
                if (Distance(person.X, person.Y, town.X, town.Y) <= 6
                    && (person.Health < 90 || person.SicknessTicks > 0)) patients++;
            }
            if (adults.Count == 0) continue;
            var fieldYield = 0d;
            var fields = 0;
            var timber = 0;
            var stone = 0;
            var fishingSites = 0;
            var fishingYield = 0d;
            var plots = new List<NaturalWorkPlot>();
            foreach (var index in Circle(town.X, town.Y, 6))
            {
                var tile = Current.Tiles[index];
                var yield = tile.PlantSiteYield(false);
                if (NaturalWorkPlotAvailable(tile, Profession.Farmer))
                {
                    fieldYield += .7 * yield; fields++;
                    plots.Add(new(index, Profession.Farmer));
                }
                if (NaturalWorkPlotAvailable(tile, Profession.Lumberjack))
                { timber++; plots.Add(new(index, Profession.Lumberjack)); }
                if (NaturalWorkPlotAvailable(tile, Profession.Miner))
                { stone++; plots.Add(new(index, Profession.Miner)); }
                if (IsFreshWater(tile) && EdibleAnimal(tile, true) is var fish && fish != WildlifeKind.None
                    && WildlifeHarvestEfficiency(tile, fish) >= .25)
                {
                    fishingSites++;
                    fishingYield += .15 * WildlifeHarvestEfficiency(tile, fish) * AnimalRules.For(fish).BodyMass;
                }
            }
            var foodDeficit = Math.Max(0, dailyFood * 30 - town.Resources.Food);
            var foodWorkers = fields == 0 ? 0 : Math.Min(fields,
                (int)Math.Ceiling((dailyFood + foodDeficit / 30) / Math.Max(.04, fieldYield / fields)));
            targets[(int)Profession.Farmer] = foodWorkers;
            if (foodDeficit > 0 && fishingSites > 0)
                targets[(int)Profession.Fisher] = Math.Min(fishingSites,
                    (int)Math.Ceiling((dailyFood + foodDeficit / 30) / Math.Max(.04, fishingYield / fishingSites)));
            targets[(int)Profession.Lumberjack] = Math.Min(timber,
                (int)Math.Ceiling(Math.Max(0, 60 - town.Resources.Wood) / 12));
            targets[(int)Profession.Miner] = Math.Min(stone,
                (int)Math.Ceiling(Math.Max(0, 80 - town.Resources.Stone - town.Resources.Ore) / 12));
            targets[(int)Profession.Representative] = town.RepresentativeId == 0 ? 0 : 1;
            if (Current.Rules.Expansion && SettlementNeedsClaimArea(town))
                targets[(int)Profession.Builder] = Math.Max(1, (GetSettlementExpansionArea(town.Id)
                    - GetSettlementArea(town.Id) + 9) / 10);
            var contacts = town.PublicKnowledge.Count(f => f.Kind == AgentFactKind.SettlementLocation
                && f.SubjectId != town.Id && f.ReliabilityAt(Current.Tick) >= .5);
            targets[(int)Profession.Messenger] = Math.Min(contacts, (adults.Count + 79) / 80);
            targets[(int)Profession.Trader] = town.Resources.Food > dailyFood * 30 ? Math.Min(contacts, 3) : 0;
            var researching = Current.Society.Research.Any(r => r.SettlementId == town.Id && r.ActiveProject is not null);
            foreach (var building in Current.Society.Buildings)
            {
                if (building.SettlementId != town.Id || building.Health <= 0 || !building.Enabled
                    || Distance(building.X, building.Y, town.X, town.Y) > 8) continue;
                buildings.Add(building);
                if (!building.IsCompleted || building.IsUpgrading || building.Health < 50)
                {
                    targets[(int)Profession.Builder] += building.WorkSlots;
                    continue;
                }
                var job = WorkplaceProfession(building.Kind);
                if (job is null || job == Profession.Scholar && !researching
                    || job == Profession.Fisher && foodDeficit <= 0
                    || job == Profession.Physician && patients == 0) continue;
                var unlock = ResearchRules.Unlocking(job.Value);
                if (unlock is not null && !HasResearch(town.Id, unlock)) continue;
                facilityTargets[(int)job.Value] += job == Profession.Physician
                    ? Math.Min(building.WorkSlots, (patients + 2) / 3) : building.WorkSlots;
            }
            for (var job = 0; job < targets.Length; job++)
                targets[job] = Math.Max(targets[job], facilityTargets[job]);
            foreach (var job in WorkforcePriorities)
                while (counts[(int)job] < targets[(int)job])
                {
                    ResidentCursor? recruit = null;
                    var best = double.NegativeInfinity;
                    foreach (var person in adults)
                    {
                        var oldJob = person.Profession;
                        if (oldJob is Profession.Representative or Profession.Soldier || oldJob == job
                            || counts[(int)oldJob] <= targets[(int)oldJob]
                            || !AvailableForLocalAssignment(person, town) || !SuitableForProfession(person, job)) continue;
                        var fit = ProfessionSuitability(person, job);
                        if (fit > best || fit == best && person.Id < recruit!.Id) { recruit = person; best = fit; }
                    }
                    if (recruit is null) break;
                    counts[(int)recruit.Profession]--;
                    counts[(int)job]++;
                    ChangeLocalProfession(recruit, job);
                }
            // 没有专业工位的人参与临时劳动，不能把所有多余职业都改成农民。
            foreach (var person in adults)
            {
                var job = person.Profession;
                if (job is Profession.Child or Profession.Laborer or Profession.Soldier
                    || person.Id == town.RepresentativeId || counts[(int)job] <= targets[(int)job]
                    || !AvailableForLocalAssignment(person, town)) continue;
                counts[(int)job]--;
                counts[(int)Profession.Laborer]++;
                ChangeLocalProfession(person, Profession.Laborer);
            }
            AssignLocalWorkplaces(town, adults, buildings);
            AssignNaturalWorkAreas(town, adults, plots);
        }
    }

    private bool AvailableForLocalAssignment(ResidentCursor person, SettlementCursor town) =>
        person.Health >= 60 && person.SicknessTicks == 0 && person.ArmyId == 0
        && !person.Agent.Goal.PlayerDirected && person.Agent.DestinationSettlementId == 0
        && person.TravelMode == TravelMode.Foot && Distance(person.X, person.Y, town.X, town.Y) <= 3
        && Current.Tick - person.MoveStartedTick >= person.MoveDurationTicks
        && (person.Profession == Profession.Laborer || Current.Tick == 0 || Current.Tick - person.Agent.JobChangedTick >= 120);

    private static bool SuitableForProfession(ResidentCursor person, Profession job) =>
        job is not (Profession.Mage or Profession.Battlemage or Profession.Gardener) || person.MagicTalent >= 35;

    private static double ProfessionSuitability(ResidentCursor person, Profession job)
    {
        var personality = person.Agent.Personality;
        return job switch
        {
            Profession.Trader or Profession.Messenger => personality.Sociability * 2 + personality.Diligence,
            Profession.Mage or Profession.Battlemage or Profession.Gardener => person.MagicTalent / 25
                + person.MagicTraining / 10 + personality.Diligence,
            Profession.Firefighter or Profession.Ranger => personality.Courage + personality.Diligence
                + person.Health / 100,
            Profession.Lumberjack => personality.Diligence * 2 + (person.Race == RaceKind.Elf ? .5 : 0),
            Profession.Miner or Profession.Engineer => personality.Diligence * 2 + (person.Race == RaceKind.Dwarf ? .5 : 0),
            Profession.Scholar or Profession.Archivist or Profession.Surveyor => personality.Diligence + personality.Ambition,
            _ => personality.Diligence * 2 + person.Health / 100,
        };
    }

    private void ChangeLocalProfession(ResidentCursor person, Profession job)
    {
        person.Replace(person.Value with { Profession = job, Agent = person.Agent.Value with
        {
            JobChangedTick = Current.Tick, WorkplaceId = 0, WorkAreaIndex = -1, NextThinkTick = Current.Tick,
            Goal = new AgentGoal { Kind = AgentGoalKind.Idle, TargetX = person.X, TargetY = person.Y,
                Reason = $"因本地供给缺口和可用工位，接受{ProfessionName(job)}分工" },
        } });
    }

    private static Profession? WorkplaceProfession(BuildingKind kind) => PreferredExpansionJob(kind) ?? kind switch
    {
        BuildingKind.Farm or BuildingKind.AutomatedFarm or BuildingKind.RunicGarden or BuildingKind.Pasture => Profession.Farmer,
        BuildingKind.LumberCamp => Profession.Lumberjack,
        BuildingKind.Quarry or BuildingKind.MiningHall => Profession.Miner,
        BuildingKind.Academy => Profession.Scholar,
        BuildingKind.ArcaneSanctum or BuildingKind.SacredGrove => Profession.Mage,
        BuildingKind.Infirmary => Profession.Physician,
        BuildingKind.Dock or BuildingKind.Aquaculture => Profession.Fisher,
        _ => null,
    };

    private void AssignLocalWorkplaces(SettlementCursor town, List<ResidentCursor> adults, List<BuildingCursor> buildings)
    {
        var occupied = new Dictionary<int, int>();
        foreach (var person in adults)
            if (person.Agent.WorkplaceId != 0)
                occupied[person.Agent.WorkplaceId] = occupied.GetValueOrDefault(person.Agent.WorkplaceId) + 1;
        foreach (var person in adults)
        {
            // 工作地点稳定；旧岗位失效时只有本人回到家园，才重新接受当地安排。
            if (person.Health < 60 || person.SicknessTicks > 0 || person.Agent.Goal.PlayerDirected
                || person.Agent.DestinationSettlementId != 0 || Distance(person.X, person.Y, town.X, town.Y) > 3
                || person.TravelMode != TravelMode.Foot || Current.Tick - person.MoveStartedTick < person.MoveDurationTicks)
                continue;
            var previous = person.Agent.WorkplaceId;
            if (buildings.Any(b => b.Id == previous && WorkplaceFits(b, person)
                && occupied.GetValueOrDefault(previous) <= b.WorkSlots)) continue;
            if (previous != 0) occupied[previous]--;
            BuildingCursor? selected = null;
            var bestDistance = int.MaxValue;
            foreach (var building in buildings)
            {
                if (!WorkplaceFits(building, person) || occupied.GetValueOrDefault(building.Id) >= building.WorkSlots) continue;
                var distance = Distance(person.X, person.Y, building.X, building.Y);
                if (distance < bestDistance || distance == bestDistance && building.Id < selected!.Id)
                { selected = building; bestDistance = distance; }
            }
            var next = selected?.Id ?? 0;
            if (next == previous) continue;
            if (next != 0) occupied[next] = occupied.GetValueOrDefault(next) + 1;
            person.Agent.WorkplaceId = next;
        }
    }

    private static bool WorkplaceFits(BuildingCursor building, ResidentCursor person) =>
        building.Enabled && building.Health > 0 && (BuildingRace(building.Kind) is not { } race || race == person.Race)
        && (building.IsCompleted && !building.IsUpgrading && building.Health >= 50
            ? WorkplaceProfession(building.Kind) == person.Profession
            : person.Profession is Profession.Builder or Profession.Engineer);
}
