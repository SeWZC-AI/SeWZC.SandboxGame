namespace SeWZC.WorldBox.Core;

public enum EventAction { General, Started, Completed, Gifted, Declaration, Muster, Battle, Capture, Retreat, Report, Homecoming, Migration, Secession, Delivery, Policy, Culture, Career, Death }
public enum WarObjective { OccupySettlement, DefendHomeland }
public enum WarOutcome { None, ObjectiveReached, SupplyShortage, HeavyLosses, TargetChanged, OrdersReceived, RouteBlocked, Exhausted }

public sealed partial class Nation
{
    public MilitaryRecord Military { get; set; } = new();
}

/// <summary>The institution's orders and actually received reports, never live front-line telemetry.</summary>
public sealed class MilitaryRecord
{
    public int CampaignEventId { get; set; }
    public int EnemyNationId { get; set; }
    public WarObjective Objective { get; set; }
    public int TargetSettlementId { get; set; }
    public int TargetX { get; set; }
    public int TargetY { get; set; }
    public long StartedTick { get; set; }
    public int LastMobilizedOrderId { get; set; }
    public long RecoveryUntilTick { get; set; }
    public int LastReportEventId { get; set; }
    public long LastReportObservedTick { get; set; }
    public long LastReportReceivedTick { get; set; }
    public WarOutcome ReportedOutcome { get; set; }
    public string Report { get; set; } = "尚未收到前线战报";
}

public sealed partial class Army
{
    public WarObjective Objective { get; set; }
    public int CampaignEventId { get; set; }
    public int LastEventId { get; set; }
    public int InitialSoldiers { get; set; }
    public long StartedTick { get; set; }
    public int BlockedTicks { get; set; }
    public WarOutcome Outcome { get; set; }
    public bool BattleRecorded { get; set; }
}

public sealed partial class WorldEvent
{
    public EventAction Action { get; set; }
    public int SettlementId { get; set; }
    public int SecondSettlementId { get; set; }
    public int EvidenceFactId { get; set; }
    public List<int> AdditionalCauseEventIds { get; set; } = [];
}

public sealed class ProgressSample
{
    public long Tick { get; set; }
    public double Progress { get; set; }
}

public sealed class ProjectObservation
{
    public int StartEventId { get; set; }
    public List<int> Contributors { get; set; } = [];
    public double DevelopmentRate { get; set; } = 1;
    public List<ProgressSample> Samples { get; set; } = [];
}

public readonly record struct CompletionEstimate(long? RemainingTicks, string Explanation);
public sealed record EventGroup(IReadOnlyList<WorldEvent> Entries)
{
    public WorldEvent Latest => Entries[^1];
    public int Count => Entries.Count;
}

public enum ObservedObjectKind { Nation, Settlement, Resident }
public readonly record struct ObservedObject(ObservedObjectKind Kind, int Id);

/// <summary>Presentation queries do not mutate events, simulation time, or the random sequence.</summary>
public static class WorldStories
{
    public static bool Involves(WorldEvent item, ObservedObject target) => target.Kind switch
    {
        ObservedObjectKind.Nation => item.NationId == target.Id || item.SecondNationId == target.Id,
        ObservedObjectKind.Settlement => item.SettlementId == target.Id || item.SecondSettlementId == target.Id,
        _ => item.ResidentId == target.Id
    };

    public static IEnumerable<int> Causes(WorldEvent item) => new[] { item.CauseEventId }
        .Concat(item.AdditionalCauseEventIds).Where(id => id > 0).Distinct();

    public static IReadOnlyList<EventGroup> Group(IEnumerable<WorldEvent> events)
    {
        var groups = new List<List<WorldEvent>>();
        foreach (var entry in events.OrderBy(e => e.Tick).ThenBy(e => e.Id))
        {
            var group = entry.Importance < EventImportance.Major && entry.Action != EventAction.General
                ? groups.LastOrDefault(g => g[0].Importance < EventImportance.Major && g[0].Kind == entry.Kind
                    && g[0].Action == entry.Action && g[0].NationId == entry.NationId && g[0].SecondNationId == entry.SecondNationId
                    && g[0].SettlementId == entry.SettlementId && g[0].SecondSettlementId == entry.SecondSettlementId
                    && g[0].ResidentId == entry.ResidentId && entry.Tick - g[0].Tick <= 60) : null;
            if (group is null) groups.Add([entry]); else group.Add(entry);
        }
        return groups.Select(g => new EventGroup(g)).OrderByDescending(g => g.Latest.Tick).ThenByDescending(g => g.Latest.Id).ToArray();
    }
}
