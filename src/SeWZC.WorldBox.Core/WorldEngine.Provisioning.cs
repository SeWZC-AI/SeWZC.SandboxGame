namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    public static double FoodUse(Resident person) => person.Age < 14 ? .02 : person.Race == RaceKind.Orc ? .052 : .04;
    public static double WaterUse(Resident person) => person.Age < 14 ? .015 : .025;

    private double WaterReserve(Resident person) => Math.Clamp(.75 + Distance(person.X, person.Y,
        person.Agent.Goal.TargetX, person.Agent.Goal.TargetY) * WaterUse(person) * 6, .75, 6);

    private void ProvisionAtHome(Resident person, Settlement home)
    {
        // A meal comes before travel stockpiling. Leave a day's shared meals in
        // the warehouse so earlier residents cannot take everybody else's food.
        var available = Math.Max(Math.Min(home.Resources.Food, FoodUse(person)), home.Resources.Food - home.Population * .06);
        var food = State.Rules.Hunger ? Math.Min(available, Math.Max(0, TravelReserve(person) - person.Inventory.Food)) : 0;
        home.Resources.Food -= food; person.Inventory.Food += food;
        available = Math.Max(Math.Min(home.Resources.Water, WaterUse(person)), home.Resources.Water - home.Population * .03);
        var water = State.Rules.Thirst ? Math.Min(available, Math.Max(0, WaterReserve(person) - person.Inventory.Water)) : 0;
        home.Resources.Water -= water; person.Inventory.Water += water;
        void TakeJobSupply(ResourceKind kind, double target)
        {
            var take = Math.Min(home.Resources.Get(kind), Math.Max(0, target - person.Inventory.Get(kind)));
            home.Resources.Set(kind, home.Resources.Get(kind) - take); person.Inventory.Set(kind, person.Inventory.Get(kind) + take);
        }
        if (person.Profession == Profession.Engineer) TakeJobSupply(ResourceKind.Tools, .5);
        if (person.Profession == Profession.Physician) TakeJobSupply(ResourceKind.Medicine, 2);
        if (person.Profession == Profession.Ranger) TakeJobSupply(ResourceKind.Ammunition, 8);
    }

    private void DrinkCarriedWater(Resident person)
    {
        if (!State.Rules.Thirst) { person.Thirst = 0; return; }
        var use = WaterUse(person);
        // Draw before drinking so water at one's feet helps on the same day.
        if (person.Inventory.Water < use && State.Tick - person.MoveStartedTick >= person.MoveDurationTicks)
            DrawWater(person, Index(person.X, person.Y), use - person.Inventory.Water);
        var drink = Math.Min(use, person.Inventory.Water);
        person.Inventory.Water -= drink;
        person.Thirst = Math.Clamp(person.Thirst + (drink >= use - .000001 ? -3 : .6 * (1 - drink / use)), 0, 100);
        if (person.Thirst > 95) DamageResident(person, .25, DeathCause.Dehydration);
    }

    public double AvailableWater(int x, int y)
    {
        if (!InBounds(x, y)) return 0;
        var tile = State.Tiles[Index(x, y)];
        if (tile.FireTicks > 0) return 0;
        var supply = DailyWaterYield(tile);
        return Math.Max(0, supply - (tile.WaterDrawTick == State.Tick ? tile.WaterDrawn : 0));
    }

    // Unlimited supply is a derived query result, never a stored resource amount.
    public static double DailyWaterYield(Tile tile) => IsFreshWater(tile) ? double.PositiveInfinity
        : tile.NaturalWaterYield * (tile.DroughtTicks > 0 ? .2 : 1);

    private double DrawWater(Resident person, int source, double wanted)
    {
        if (source < 0 || source >= State.Tiles.Length || State.Tick - person.MoveStartedTick < person.MoveDurationTicks
            || Distance(person.X, person.Y, source % State.Width, source / State.Width) > (IsFreshWater(State.Tiles[source]) ? 1 : 0)) return 0;
        var tile = State.Tiles[source];
        var amount = Math.Min(Math.Max(0, wanted), AvailableWater(source % State.Width, source / State.Width));
        amount = Math.Min(amount, 1_000_000 - person.Inventory.Water);
        if (!IsFreshWater(tile))
        {
            if (tile.WaterDrawTick != State.Tick) { tile.WaterDrawTick = State.Tick; tile.WaterDrawn = 0; }
            tile.WaterDrawn += amount;
        }
        person.Inventory.Water += amount;
        if (amount > .05) RecordHarvest(tile, amount);
        return amount;
    }

    private (int Bank, int Source) FindWaterSite(Resident person)
    {
        var reachable = 0;
        // Sources outside the visible circle must have an actually learned address.
        var bestBank = -1; var bestSource = -1; var bestScore = 0d;
        void Consider(int source)
        {
            if (source < 0 || source >= State.Tiles.Length || !IsWaterSource(State.Tiles[source]) || State.Tiles[source].FireTicks > 0) return;
            var x = source % State.Width; var y = source / State.Width;
            var available = Math.Min(1, AvailableWater(x, y));
            if (available <= 0) return;
            if (RaceTerrainRules.CanWalk(State.Tiles[source], person.Race))
            {
                var distance = Distance(person.X, person.Y, x, y);
                var score = available / (1 + distance * .25);
                if (score > bestScore && VisibleSiteReachable(person, source, ref reachable)) { bestScore = score; bestBank = source; bestSource = source; }
                return;
            }
            foreach (var (dx, dy) in Directions)
            {
                var xx = x + dx; var yy = y + dy;
                if (!Walkable(xx, yy, person.Race) || State.Tiles[Index(xx, yy)].FireTicks > 0) continue;
                var distance = Distance(person.X, person.Y, xx, yy);
                var score = available / (1 + distance * .25);
                if (score <= bestScore) continue;
                if (!VisibleSiteReachable(person, Index(xx, yy), ref reachable)) continue;
                bestScore = score; bestBank = Index(xx, yy); bestSource = source;
            }
        }
        foreach (var offset in VisibleResourceOffsets)
        {
            var x = person.X + offset.X; var y = person.Y + offset.Y;
            if (InBounds(x, y)) Consider(Index(x, y));
            if (bestScore == 1) break;
        }
        // Distant learned sources can guide later exploration; a daily refill
        // must use a bank reachable within the currently visible terrain.
        if (bestSource >= 0 && Distance(person.X, person.Y, bestSource % State.Width, bestSource / State.Width) <= 6)
        {
            var existing = person.Agent.Memory.FirstOrDefault(f => f.Kind == AgentFactKind.WaterSource && f.SubjectId == bestSource + 1);
            if (existing is null || State.Tick - existing.ObservedTick >= 120)
                RememberAgentFact(person, MakeAgentFact(person, AgentFactKind.WaterSource, bestSource + 1,
                    bestSource % State.Width, bestSource / State.Width, 1, "实地发现可取水的河湖或有供水的陆地"), copy: false);
        }
        return (bestBank, bestSource);
    }

    private void AddProvisionChoices(Resident person, Settlement home, List<GoalChoice> choices)
    {
        var needsWater = person.Inventory.Water < .3
            && (person.Thirst >= 10 || AvailableWater(person.X, person.Y) < WaterUse(person));
        if (State.Rules.Thirst && (needsWater || person.Id % 5 == 0 && person.Inventory.Water < WaterReserve(person) + 3))
        {
            var atHome = Distance(person.X, person.Y, home.X, home.Y) <= 1;
            if (atHome && home.Resources.Water >= .3)
                choices.Add(new(AgentGoalKind.Eat, home.X, home.Y, 75 + person.Thirst, "在家园领取随身饮水与口粮", SettlementId: home.Id));
            else
            {
                var water = FindWaterSite(person);
                if (water.Bank >= 0 && (needsWater || DailyWaterYield(State.Tiles[water.Source]) >= .1))
                    choices.Add(new(AgentGoalKind.FetchWater, water.Bank % State.Width, water.Bank / State.Width,
                        needsWater ? 65 + person.Thirst : atHome && home.Resources.Water < home.Population * .5 ? 72 : 18,
                        "前往实际见过的河湖或有供水的陆地取水，随身携带并运回家园", EntityId: water.Source + 1));
                else if (needsWater && water.Bank < 0)
                {
                    if (person.Thirst < 40 && Distance(person.X, person.Y, home.X, home.Y) >= 12
                        && !(person.Agent.Goal.Kind == AgentGoalKind.ReturnHome && State.Tick - person.Agent.Goal.StartedTick > 48))
                    {
                        choices.Add(new(AgentGoalKind.ReturnHome, home.X, home.Y, 180 + person.Thirst,
                            "寻水已到安全补给边界，返回已知家园后重新安排路线", SettlementId: home.Id));
                        return;
                    }
                    var heading = Directions[person.Agent.ExplorationHeading % 4];
                    var best = -1; var bestDistance = int.MaxValue;
                    var reachable = 0;
                    foreach (var offset in VisibleResourceOffsets)
                    {
                        var x = person.X + offset.X; var y = person.Y + offset.Y;
                        if (!Walkable(x, y, person.Race) || State.Tiles[Index(x, y)].FireTicks > 0) continue;
                        var distance = Distance(x, y, person.X + heading.X * 6, person.Y + heading.Y * 6);
                        if (distance < bestDistance && VisibleSiteReachable(person, Index(x, y), ref reachable))
                        { best = Index(x, y); bestDistance = distance; }
                    }
                    if (best >= 0) choices.Add(new(AgentGoalKind.FetchWater, best % State.Width, best / State.Width,
                        50 + person.Thirst, "沿眼前可通行土地勘察水源；尚未发现河湖或有供水的陆地"));
                }
            }
        }
        if (State.Rules.Thirst && person.Inventory.Water >= WaterReserve(person) + 2)
            choices.Add(new(AgentGoalKind.ReturnHome, home.X, home.Y, 110, "携带打来的饮水返仓，为家园补给", SettlementId: home.Id));
        var homeDistance = Distance(person.X, person.Y, home.X, home.Y);
        var daysHome = homeDistance * 4 + 12;
        if (State.Rules.Hunger && homeDistance > 1 && person.Hunger < 20
            && person.Inventory.Food < FoodUse(person) * daysHome)
            choices.Add(new(AgentGoalKind.ReturnHome, home.X, home.Y, 85, "口粮接近保守返程需求，先实地返仓补给", SettlementId: home.Id));
        if (person.Age < 14) return;
        var foodNeeded = FoodSupplyNeeded(person, home);
        if (foodNeeded) AddBoatFishingChoice(person, home, choices);
        var wildlife = foodNeeded && (person.Profession is Profession.Farmer or Profession.Fisher || person.Hunger > 20 && person.Inventory.Food < .3) ? FindHarvestableWildlife(person) : (-1, -1, false);
        if (wildlife.Item1 >= 0)
            choices.Add(new(wildlife.Item3 ? AgentGoalKind.Fish : AgentGoalKind.Hunt,
                wildlife.Item1 % State.Width, wildlife.Item1 / State.Width,
                (person.Inventory.Food < .3 && person.Hunger > 20 ? 100 : person.Profession is Profession.Farmer or Profession.Fisher ? 43 : 14)
                    + (person.Inventory.Food < .3 ? person.Hunger : 0),
                wildlife.Item3 ? "在眼前水岸捕鱼，鱼群数量会实际减少" : "在眼前栖息地狩猎，将食物携带返乡",
                EntityId: wildlife.Item2 + 1));
        var claim = VisibleClaimSite(person, home);
        if (claim >= 0) choices.Add(new(AgentGoalKind.ClaimLand, claim % State.Width, claim / State.Width,
            (IsSettlementActive(home.Id) ? SettlementNeedsClaimArea(home) ? 58 : home.IsExpanding ? 52 : 25 : 70) + person.Agent.Personality.Ambition * 8,
            !IsSettlementActive(home.Id) ? $"城镇占地 {GetSettlementArea(home.Id)}/{SettlementActivationArea} 格，加成尚未生效，先实地登记领地"
                : SettlementNeedsClaimArea(home) ? $"升级面积 {GetSettlementArea(home.Id)}/{GetSettlementExpansionArea(home.Id)} 格，先实地登记相邻领地"
                : "实际前往可达的相邻边界地块，登记城镇地盘", SettlementId: home.Id));
    }

    public bool TryFetchWater(Resident person)
    {
        if (person.Agent.Goal.Kind != AgentGoalKind.FetchWater || person.Health <= 0) return false;
        var source = person.Agent.Goal.TargetEntityId - 1;
        var amount = DrawWater(person, source, Math.Min(source >= 0 && source < State.Tiles.Length ? GatheringTerritoryMultiplier(person, State.Tiles[source]) : 0, WaterReserve(person) + 3 - person.Inventory.Water));
        if (source < 0 || amount <= 0)
        {
            // Continue each outward leg; rotating after every arrival only
            // walked the same small square. Turn when the visible edge is blocked.
            if (source >= 0 || person.Agent.Goal.WorkTicks > 1)
                person.Agent.ExplorationHeading = (person.Agent.ExplorationHeading + 1) % 8;
            person.Agent.NextThinkTick = State.Tick;
        }
        person.Activity = ResidentActivity.Working;
        return amount > 0;
    }

    private (int Site, int Source, bool Fishing) FindHarvestableWildlife(Resident person)
    {
        var reachable = 0; var best = (Site: -1, Source: -1, Fishing: false); var bestScore = 0d;
        foreach (var offset in VisibleResourceOffsets)
        {
            // A shore fisher can reach a bank six steps away beside a fish
            // source seven steps away. Land prey itself must be within six.
            if (offset.Distance > 7) break;
            var x = person.X + offset.X; var y = person.Y + offset.Y;
            if (!InBounds(x, y)) continue;
            var index = Index(x, y); var tile = State.Tiles[index];
            if (tile.FireTicks > 0) continue;
            var aquatic = IsWaterTerrain(tile.Terrain); var kind = EdibleAnimal(tile, aquatic);
            var efficiency = WildlifeHarvestEfficiency(tile, kind);
            if (kind == WildlifeKind.None || efficiency < .25) continue;
            var score = Math.Min(tile.AnimalPopulation(kind) * .1, .15 * efficiency) * AnimalRules.For(kind).BodyMass
                * GatheringTerritoryMultiplier(person, tile) / (1 + offset.Distance * .2);
            if (score <= bestScore) continue;
            if (!aquatic && offset.Distance <= 6 && RaceTerrainRules.CanWalk(tile, person.Race)
                && VisibleSiteReachable(person, index, ref reachable))
            { best = (index, index, false); bestScore = score; continue; }
            if (!aquatic) continue;
            foreach (var (dx, dy) in Directions)
                if (Walkable(x + dx, y + dy, person.Race) && State.Tiles[Index(x + dx, y + dy)].FireTicks == 0
                    && VisibleSiteReachable(person, Index(x + dx, y + dy), ref reachable))
                { best = (Index(x + dx, y + dy), index, true); bestScore = score; break; }
        }
        return best;
    }

    private void AddBoatFishingChoice(Resident person, Settlement home, List<GoalChoice> choices)
    {
        if (person.Profession != Profession.Fisher || person.Age < 14
            || person.TravelMode != TravelMode.Boat && !(Distance(person.X, person.Y, home.X, home.Y) <= 1
                && HasResearch(home.Id, ResearchKind.Logistics) && home.Resources.Boats >= 1)) return;
        var reachable = 0; var best = -1; var score = double.NegativeInfinity;
        foreach (var offset in VisibleResourceOffsets)
        {
            if (offset.Distance > 6) break;
            var x = person.X + offset.X; var y = person.Y + offset.Y;
            if (!InBounds(x, y)) continue;
            var index = Index(x, y); var tile = State.Tiles[index];
            var animal = EdibleAnimal(tile, aquatic: true);
            var efficiency = WildlifeHarvestEfficiency(tile, animal);
            if (animal == WildlifeKind.None || efficiency < .25 || tile.FireTicks > 0) continue;
            var value = Math.Min(tile.AnimalPopulation(animal) * .1, .15 * efficiency) * AnimalRules.For(animal).BodyMass / (1 + offset.Distance * .2);
            if (value > score && VisibleSiteReachable(person, index, ref reachable, TravelMode.Boat))
            { best = index; score = value; }
        }
        if (best >= 0) choices.Add(new(AgentGoalKind.Fish, best % State.Width, best / State.Width, 60,
            "领取家园舟船，前往眼前鱼群捕鱼，装满后返岸交付鱼获与舟船", EntityId: best + 1));
        else if (person.TravelMode == TravelMode.Boat) choices.Add(new(AgentGoalKind.ReturnHome, home.X, home.Y, 90,
            "附近鱼群已不足，返岸交付鱼获与舟船", SettlementId: home.Id));
    }

    private static WildlifeKind EdibleAnimal(Tile tile, bool aquatic = false)
    {
        if (aquatic != IsWaterTerrain(tile.Terrain)) return WildlifeKind.None;
        return tile.EdibleAnimal(aquatic);
    }

    public bool TryHarvestWildlife(Resident person)
    {
        var goal = person.Agent.Goal; var source = goal.TargetEntityId - 1;
        if (person.Health <= 0 || person.Age < 14 || source < 0 || source >= State.Tiles.Length
            || goal.Kind is not (AgentGoalKind.Hunt or AgentGoalKind.Fish)
            || State.Tick - person.MoveStartedTick < person.MoveDurationTicks
            || Distance(person.X, person.Y, source % State.Width, source / State.Width) > (goal.Kind == AgentGoalKind.Fish ? 1 : 0)) return false;
        var tile = State.Tiles[source]; var kind = EdibleAnimal(tile, aquatic: goal.Kind == AgentGoalKind.Fish);
        if (kind == WildlifeKind.None || tile.FireTicks > 0) { person.Agent.NextThinkTick = State.Tick; return false; }
        var yield = AnimalRules.For(kind).BodyMass;
        var amount = WildlifeHarvestAmount(tile, kind, .15 * State.Rules.GatheringRate * GatheringCondition(person) * GatheringTerritoryMultiplier(person, tile));
        amount = Math.Min(amount, (1_000_000 - person.Inventory.Food) / yield);
        tile.SetAnimalPopulation(kind, tile.AnimalPopulation(kind) - amount);
        person.Inventory.Food += amount * yield; RecordHarvest(tile, amount * yield);
        person.Agent.Fatigue = Math.Min(100, person.Agent.Fatigue + .3); person.Activity = ResidentActivity.Working;
        if (!goal.PlayerDirected && !WildlifeSiteProductive(tile, goal.Kind == AgentGoalKind.Fish)) person.Agent.NextThinkTick = State.Tick + 1;
        return amount > 0;
    }
}
