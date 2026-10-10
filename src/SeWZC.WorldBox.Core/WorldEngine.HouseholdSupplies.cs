using SeWZC.WorldBox.Core.Runtime;

namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    private readonly Dictionary<int, (double Food, double Water, int Adults)> _householdSupplyNeeds = [];

    private static bool NeedsHouseholdCare(Resident person) => person.Age < ResidentNeedsRules.MinimumOutdoorAge
        || ResidentNeedsRules.IsUnconscious(person);

    private void ShareHouseholdSupplies()
    {
        _householdSupplyNeeds.Clear();
        var donors = new Dictionary<int, List<StateReference<Resident>>>();
        foreach (var reference in Residents)
        {
            var person = reference.Value;
            if (person.Health <= 0 || person.HomeBuildingId == 0)
                continue;
            var need = _householdSupplyNeeds.GetValueOrDefault(person.HomeBuildingId);
            if (NeedsHouseholdCare(person))
                need = (need.Food + FoodUse(person), need.Water + WaterUse(person), need.Adults);
            else if (person.Age >= ResidentNeedsRules.AdultAge)
            {
                need = (need.Food, need.Water, need.Adults + 1);
                if (person.IsInsideHome)
                {
                    if (!donors.TryGetValue(person.HomeBuildingId, out var atHome))
                        donors[person.HomeBuildingId] = atHome = [];
                    atHome.Add(reference);
                }
            }
            _householdSupplyNeeds[person.HomeBuildingId] = need;
        }
        foreach (var reference in Residents)
        {
            var person = reference.Value;
            if (person.Health <= 0 || !person.IsInsideHome || !NeedsHouseholdCare(person)
                || !donors.TryGetValue(person.HomeBuildingId, out var atHome))
                continue;
            var inventory = person.Inventory;
            foreach (var donor in atHome)
            {
                var supply = donor.Value.Inventory;
                var food = Rules.Hunger ? Math.Min(Math.Max(0, FoodUse(person) * DailyRoutineRules.SupplyReserveDays - inventory.Food),
                    Math.Max(0, supply.Food - FoodUse(donor.Value) * DailyRoutineRules.SupplyReserveDays)) : 0;
                var water = Rules.Thirst ? Math.Min(Math.Max(0, WaterUse(person) * DailyRoutineRules.SupplyReserveDays - inventory.Water),
                    Math.Max(0, supply.Water - WaterUse(donor.Value) * DailyRoutineRules.SupplyReserveDays)) : 0;
                if (food == 0 && water == 0)
                    continue;
                donor.Replace(donor.Value.WithInventory(supply with { Food = supply.Food - food, Water = supply.Water - water }));
                inventory = inventory with { Food = inventory.Food + food, Water = inventory.Water + water };
            }
            reference.Replace(person.WithInventory(inventory));
        }
    }

    private (double Food, double Water) HouseholdSupplyReserve(Resident person)
    {
        if (person.Age < ResidentNeedsRules.AdultAge || NeedsHouseholdCare(person))
            return (0, 0);
        var need = _householdSupplyNeeds.GetValueOrDefault(person.HomeBuildingId);
        var days = DailyRoutineRules.SupplyReserveDays / (double)Math.Max(1, need.Adults);
        return (need.Food * days, need.Water * days);
    }
}
