using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

public enum WildlifeKind { None, Rabbit, Deer, Boar, Goat, Wolf, Waterfowl, Fish }
public enum ConflictStage { Dispute, Confrontation, Violence, Resolved }
public enum ConflictScope { Individual, Group, Settlement }

public sealed partial class Tile
{
    [JsonRequired] public WildlifeKind Wildlife { get; set; }
    [JsonRequired] public double WildlifePopulation { get; set; }
}

public sealed partial class WorldState
{
    [JsonRequired] public List<LocalConflict> Conflicts { get; set; } = [];
}

public sealed class LocalConflict
{
    public int Id { get; set; }
    public int FirstResidentId { get; set; }
    public int SecondResidentId { get; set; }
    public int SettlementId { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public ConflictScope Scope { get; set; }
    public ConflictStage Stage { get; set; }
    public double Tension { get; set; }
    public long StartedTick { get; set; }
    public long StageStartedTick { get; set; }
    public long LastChangedTick { get; set; }
    public int LastEventId { get; set; }
    public List<int> Participants { get; set; } = [];
}
