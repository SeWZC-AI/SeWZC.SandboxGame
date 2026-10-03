namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    private static readonly ResourceKind[] MineralAndVehicleResources =
        [ResourceKind.Coal, ResourceKind.Oil, ResourceKind.RareEarth, ResourceKind.Boats, ResourceKind.Aircraft, ResourceKind.Water];

    /// <summary>Vehicles are manufactured, carried to the warehouse and borrowed there by actual travellers.</summary>
    private void PrepareJourneyTransport(Resident person, Settlement home)
    {
        if (person.TravelMode != TravelMode.Foot || Distance(person.X, person.Y, home.X, home.Y) > 1) return;
        var distance = Distance(home.X, home.Y, person.Agent.Goal.TargetX, person.Agent.Goal.TargetY);
        if (distance < 6) return;
        // Reserve an entire out-and-back flight at takeoff. No remote warehouse supplies fuel en route.
        var fuel = Math.Max(1, distance * .04);
        if (HasResearch(home.Id, ResearchKind.Aviation) && HasResearch(home.Id, ResearchKind.Electrification)
            && home.Resources.Aircraft >= 1 && home.Resources.Oil >= fuel)
        {
            home.Resources.Aircraft--; person.Inventory.Aircraft++;
            home.Resources.Oil -= fuel;
            person.TravelMode = TravelMode.Aircraft;
            AddEvent(WorldEventKind.Trade, $"{person.Name}在{home.Name}装载运输机，携带货物与消息启程；已消耗往返燃料 {fuel:0.#}。",
                home.X, home.Y, EventAction.Started, home.Id, person.Id);
        }
        else if (HasResearch(home.Id, ResearchKind.Logistics) && home.Resources.Boats >= 1)
        {
            home.Resources.Boats--; person.Inventory.Boats++;
            person.TravelMode = TravelMode.Boat;
        }
    }

    public static string TravelModeName(TravelMode mode) => mode switch
    { TravelMode.Aircraft => "航空运输", TravelMode.Boat => "舟船运输（水上航行／陆地搬运）", _ => "步行" };
}
