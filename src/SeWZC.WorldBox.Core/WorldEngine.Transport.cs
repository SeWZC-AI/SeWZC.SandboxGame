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
    private void PrepareJourneyTransport(ResidentCursor person, SettlementCursor home)
    {
        if (person.TravelMode != TravelMode.Foot || Distance(person.X, person.Y, home.X, home.Y) > 1)
            return;
        var distance = Distance(home.X, home.Y, person.Agent.Goal.TargetX, person.Agent.Goal.TargetY);
        if (distance < 6 && person.Agent.Goal.Kind != AgentGoalKind.Fish)
            return;
        // 起飞时预留往返燃料，避免途中凭空从远方仓库补给。
        var fuel = Math.Max(1, distance * .04);
        if (person.Agent.Goal.Kind != AgentGoalKind.Fish && HasResearch(home.Id, Advancement.Aviation) &&
            HasResearch(home.Id, Advancement.Electrification)
            && home.Resources.Aircraft >= 1 && home.Resources.Oil >= fuel)
        {
            home.Resources = home.Resources with { Aircraft = home.Resources.Aircraft - 1 };
            person.Inventory = person.Inventory with { Aircraft = person.Inventory.Aircraft + 1 };
            home.Resources = home.Resources with { Oil = home.Resources.Oil - fuel };
            person.TravelMode = TravelMode.Aircraft;
            AddEvent(WorldEventKind.Trade, $"{person.Name}在{home.Name}装载运输机，携带货物与消息启程；已消耗往返燃料 {fuel:0.#}。",
                home.X, home.Y, EventAction.Started, home.Id, person.Id);
        }
        else if (HasResearch(home.Id, Advancement.Logistics) && home.Resources.Boats >= 1)
        {
            home.Resources = home.Resources with { Boats = home.Resources.Boats - 1 };
            person.Replace(person.Value with { Inventory = person.Inventory with { Boats = person.Inventory.Boats + 1 }, TravelMode = TravelMode.Boat });
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
