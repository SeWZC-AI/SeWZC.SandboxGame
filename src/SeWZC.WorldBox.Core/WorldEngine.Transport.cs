namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    private static readonly ResourceKind[] MineralAndVehicleResources =
    [
        ResourceKind.Coal, ResourceKind.Oil, ResourceKind.RareEarth, ResourceKind.Boats, ResourceKind.Aircraft,
        ResourceKind.Water,
    ];

    /// <summary>在实际到达的聚落仓库借用载具，并预留旅程所需燃料。</summary>
    /// <param name="person">参与当前操作的居民状态。</param>
    /// <param name="home">出借载具的本地聚落仓库。</param>
    private void PrepareJourneyTransport(Resident person, Settlement home)
    {
        if (person.TravelMode != TravelMode.Foot || Distance(person.X, person.Y, home.X, home.Y) > 1) return;
        var distance = Distance(home.X, home.Y, person.Agent.Goal.TargetX, person.Agent.Goal.TargetY);
        if (distance < 6 && person.Agent.Goal.Kind != AgentGoalKind.Fish) return;
        // 起飞时预留往返燃料，避免途中凭空从远方仓库补给。
        var fuel = Math.Max(1, distance * .04);
        if (person.Agent.Goal.Kind != AgentGoalKind.Fish && HasResearch(home.Id, ResearchKind.Aviation) &&
            HasResearch(home.Id, ResearchKind.Electrification)
            && home.Resources.Aircraft >= 1 && home.Resources.Oil >= fuel)
        {
            home.Resources.Aircraft--;
            person.Inventory.Aircraft++;
            home.Resources.Oil -= fuel;
            person.TravelMode = TravelMode.Aircraft;
            AddEvent(WorldEventKind.Trade, $"{person.Name}在{home.Name}装载运输机，携带货物与消息启程；已消耗往返燃料 {fuel:0.#}。",
                home.X, home.Y, EventAction.Started, home.Id, person.Id);
        }
        else if (HasResearch(home.Id, ResearchKind.Logistics) && home.Resources.Boats >= 1)
        {
            home.Resources.Boats--;
            person.Inventory.Boats++;
            person.TravelMode = TravelMode.Boat;
        }
    }

    /// <summary>返回交通方式的中文名称。</summary>
    /// <param name="mode">待判断的交通方式。</param>
    public static string TravelModeName(TravelMode mode)
    {
        return mode switch
        {
            TravelMode.Aircraft => "航空运输", TravelMode.Boat => "舟船运输（水上航行／陆地搬运）", _ => "步行",
        };
    }
}
