using SeWZC.WorldBox.Core.Runtime;

namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    private static readonly ResourceKind[] MineralAndVehicleResources =
    [
        ResourceKind.Coal, ResourceKind.Oil, ResourceKind.RareEarth, ResourceKind.Boats, ResourceKind.Aircraft,
        ResourceKind.Water,
    ];

    /// <summary>在实际到达的聚落仓库借用载具，并预留旅程所需燃料。</summary>
    /// <param name="person">借用载具的居民。</param>
    /// <param name="home">出借载具的本地聚落仓库。</param>
    private void PrepareJourneyTransport(StateReference<Resident> person, StateReference<Settlement> home)
    {
        if (person.Value.TravelMode != TravelMode.Foot || Distance(person.Value.X, person.Value.Y, home.Value.X, home.Value.Y) > 1)
            return;
        var distance = Distance(home.Value.X, home.Value.Y, person.Value.Agent.Goal.TargetX, person.Value.Agent.Goal.TargetY);
        if (distance < 6 && person.Value.Agent.Goal.Kind != AgentGoalKind.Fish)
            return;
        // 起飞时预留往返燃料，避免途中凭空从远方仓库补给。
        var fuel = Math.Max(1, distance * .04);
        if (person.Value.Agent.Goal.Kind != AgentGoalKind.Fish && HasResearch(home.Value.Id, Advancement.Aviation) &&
            HasResearch(home.Value.Id, Advancement.Electrification)
            && home.Value.Resources.Aircraft >= 1 && home.Value.Resources.Oil >= fuel)
        {
            home.Replace(home.Value.WithResources(home.Value.Resources with { Aircraft = home.Value.Resources.Aircraft - 1 }));
            person.Replace(person.Value.WithInventory(person.Value.Inventory with { Aircraft = person.Value.Inventory.Aircraft + 1 }));
            home.Replace(home.Value.WithResources(home.Value.Resources with { Oil = home.Value.Resources.Oil - fuel }));
            person.Replace(person.Value.WithTravelMode(TravelMode.Aircraft));
            AddEvent(WorldEventKind.Trade, $"{person.Value.Name}在{home.Value.Name}装载运输机，携带货物与消息启程；已消耗往返燃料 {fuel:0.#}。",
                home.Value.X, home.Value.Y, EventAction.Started, home.Value.Id, person.Value.Id);
        }
        else if (HasResearch(home.Value.Id, Advancement.Logistics) && home.Value.Resources.Boats >= 1)
        {
            home.Replace(home.Value.WithResources(home.Value.Resources with { Boats = home.Value.Resources.Boats - 1 }));
            person.Replace(person.Value.WithInventory(person.Value.Inventory with { Boats = person.Value.Inventory.Boats + 1 }));
            person.Replace(person.Value.WithTravelMode(TravelMode.Boat));
        }
    }

    /// <summary>返回交通方式的中文名称。</summary>
    /// <param name="mode">待判断的交通方式。</param>
    public static string TravelModeName(TravelMode mode)
    {
        return mode switch
        {
            TravelMode.Boat => "舟船运输（水上航行／陆地搬运）",
            TravelMode.Aircraft => "航空运输",
            _ => "步行",
        };
    }
}
