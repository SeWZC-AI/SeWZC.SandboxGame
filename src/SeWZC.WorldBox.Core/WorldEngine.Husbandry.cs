using SeWZC.WorldBox.Core.Runtime;

namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    /// <summary>判断设施是否为牧场或水产养殖厂。</summary>
    /// <param name="kind">设施类别。</param>
    public static bool IsHusbandry(BuildingKind kind)
    {
        return kind is BuildingKind.Pasture or BuildingKind.Aquaculture;
    }

    /// <summary>返回设施等级允许的养殖容量。</summary>
    /// <param name="building">要查询养殖容量的设施。</param>
    public static double LivestockCapacity(Building building)
    {
        return (building.Kind == BuildingKind.Pasture ? 8 : 12) * building.Efficiency;
    }

    private static bool CanDomesticate(WildlifeKind kind, bool aquatic)
    {
        return AnimalRules.For(kind).Diet == AnimalDiet.Herbivore
               && (aquatic
                   ? kind is WildlifeKind.Fish or WildlifeKind.GrassCarp
                   : kind is WildlifeKind.Deer or WildlifeKind.Boar or WildlifeKind.Goat or WildlifeKind.Bison
                       or WildlifeKind.Yak or WildlifeKind.Gazelle);
    }

    private (int Source, WildlifeKind Kind) HusbandryStockAt(int x, int y, bool aquatic)
    {
        var source = -1;
        var species = WildlifeKind.None;
        var largest = .02;
        foreach (var i in Circle(x, y, aquatic ? 1 : 0))
        {
            if (aquatic && !IsFreshWater(Current.Tiles[i].Value))
                continue;
            foreach (var kind in AnimalRules.Species)
                if (CanDomesticate(kind, aquatic) && Current.Tiles[i].AnimalPopulation(kind) > largest)
                {
                    largest = Current.Tiles[i].AnimalPopulation(kind);
                    source = i;
                    species = kind;
                }
        }

        return (source, species);
    }

    private static double LivestockFeed(Building b)
    {
        return .02 * Math.Min(6, b.LivestockPopulation);
    }

    private static double LivestockWater(Building b)
    {
        return .005 * Math.Min(6, b.LivestockPopulation);
    }

    private bool HusbandryHasWork(Building b, ResidentCursor person)
    {
        if (!HasResearch(b.SettlementId,
                b.Kind == BuildingKind.Pasture ? Advancement.Agriculture : Advancement.Industry)
            || (b.Kind == BuildingKind.Aquaculture && !HasResearch(b.SettlementId, Advancement.Logistics)))
            return false;
        if (person.Profession != (b.Kind == BuildingKind.Pasture ? Profession.Farmer : Profession.Fisher))
            return false;
        var tile = Current.Tiles[Index(b.X, b.Y)];
        if (tile.DroughtTicks > 0 || tile.FireTicks > 0)
            return false;
        if (b.LivestockPopulation < .01)
            return HusbandryStockAt(b.X, b.Y, b.Kind == BuildingKind.Aquaculture).Source >= 0;
        var home = RequireTown(b.SettlementId);
        return (person.Inventory.Food >= LivestockFeed(b) || home.Resources.Food >= LivestockFeed(b))
               && (person.Inventory.Water >= .75 + LivestockWater(b) || home.Resources.Water >= .08 ||
                   DailyWaterYield(tile.Value) >= .08)
               && (b.Kind == BuildingKind.Aquaculture || tile.ResourceAmount >= .2);
    }

    private bool ActOnHusbandry(ResidentCursor person, SettlementCursor home)
    {
        var goal = person.Agent.Goal;
        if (goal.Kind != AgentGoalKind.Work || FindBuilding(goal.TargetEntityId) is not { } b || !IsHusbandry(b.Value.Kind)
            || !b.Value.IsCompleted || b.Value.IsUpgrading)
            return false;
        if (!HusbandryHasWork(b.Value, person))
        {
            person.Agent = person.Agent with { NextThinkTick = Current.Tick };
            return true;
        }

        if (b.Value.LivestockPopulation >= .01 && (person.Inventory.Food < LivestockFeed(b.Value) ||
                                             person.Inventory.Water < .75 + LivestockWater(b.Value)))
        {
            person.Agent = person.Agent.WithGoal(goal = goal with { TargetX = home.X, TargetY = home.Y });
            if (Distance(person.X, person.Y, home.X, home.Y) > 1)
            {
                MoveAgentTowards(person, home.X, home.Y);
                person.Activity = ResidentActivity.Delivering;
                return true;
            }

            foreach (var (kind, target) in new[]
                     {
                         (ResourceKind.Food, TravelReserve(person) + 1), (ResourceKind.Water, 1.5),
                     })
            {
                var take = Math.Min(home.Resources.Get(kind), Math.Max(0, target - person.Inventory.Get(kind)));
                home.UpdateResources(home.Resources.WithAmount(kind, home.Resources.Get(kind) - take));
                person.Inventory = person.Inventory.WithAmount(kind, person.Inventory.Get(kind) + take);
            }

            DrawWater(person, Index(person.X, person.Y), Math.Max(0, 1.5 - person.Inventory.Water));
        }

        person.Agent = person.Agent.WithGoal(goal = goal with { TargetX = b.Value.X, TargetY = b.Value.Y });
        if (Distance(person.X, person.Y, b.Value.X, b.Value.Y) > 0)
        {
            MoveAgentTowards(person, b.Value.X, b.Value.Y);
            person.Activity = ResidentActivity.Delivering;
            return true;
        }

        if (TryWorkAtBuilding(person))
            person.Activity = ResidentActivity.Working;
        if (person.Inventory.Food >= TravelReserve(person) + 3)
        {
            var previous = person.Agent.Goal;
            person.Agent = person.Agent.WithGoal(new AgentGoal
            {
                Kind = AgentGoalKind.ReturnHome, TargetX = home.X, TargetY = home.Y, StartedTick = Current.Tick,
            });
            ChangeWorkReservation(previous, person.Agent.Goal);
            person.Agent = person.Agent with { NextThinkTick = Current.Tick };
        }

        return true;
    }

    private bool WorkHusbandry(StateReference<Building> b, ResidentCursor person, double effort)
    {
        if (!HusbandryHasWork(b.Value, person) || person.X != b.Value.X || person.Y != b.Value.Y)
            return false;
        if (b.Value.LivestockPopulation < .01)
        {
            var stock = HusbandryStockAt(b.Value.X, b.Value.Y, b.Value.Kind == BuildingKind.Aquaculture);
            if (stock.Source < 0)
                return false;
            var tile = Current.Tiles[stock.Source];
            var take = Math.Min(1, tile.AnimalPopulation(stock.Kind) * .5);
            tile.SetAnimalPopulation(stock.Kind, tile.AnimalPopulation(stock.Kind) - take);
            b.Replace(b.Value with { LivestockKind = stock.Kind, LivestockPopulation = take });
        }
        else
        {
            if (person.Inventory.Food < LivestockFeed(b.Value) || person.Inventory.Water < .75 + LivestockWater(b.Value))
                return false;
            person.Inventory = person.Inventory with
            {
                Food = person.Inventory.Food - LivestockFeed(b.Value),
                Water = person.Inventory.Water - LivestockWater(b.Value),
            };
            var tile = Current.Tiles[Index(b.Value.X, b.Value.Y)];
            if (b.Value.Kind == BuildingKind.Pasture)
                HarvestPlants(tile, .04 * Math.Min(6, b.Value.LivestockPopulation) * NaturalPlantHarvestEfficiency(tile));
            // 种群恢复需要饲料和到场劳动，采收不能消耗保留的繁殖种群。
            b.Replace(b.Value with
            {
                LivestockPopulation = Math.Min(LivestockCapacity(b.Value),
                b.Value.LivestockPopulation + .04 * effort * b.Value.LivestockPopulation *
                (1 - b.Value.LivestockPopulation / LivestockCapacity(b.Value)))
            });
            if (b.Value.LivestockPopulation > 2)
            {
                var harvest = Math.Min(b.Value.LivestockPopulation - 2, .04 * Math.Min(1.5, effort));
                b.Replace(b.Value with { LivestockPopulation = b.Value.LivestockPopulation - (harvest) });
                person.Inventory = person.Inventory with
                {
                    Food = person.Inventory.Food + harvest * (b.Value.Kind == BuildingKind.Pasture ? 8 : 9),
                };
                b.Replace(b.Value with { ProductionBatches = Math.Min(1_000_000_000, b.Value.ProductionBatches + 1) });
            }
        }

        b.Replace(b.Value with
        {
            ServiceActions = Math.Min(1_000_000_000, b.Value.ServiceActions + 1),
            LastServiceTick = Current.Tick,
        });
        return true;
    }
}
