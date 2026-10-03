using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

public enum WildlifeKind { None, Rabbit, Deer, Boar, Goat, Wolf, Waterfowl, Fish }
public enum ConflictStage { Dispute, Confrontation, Violence, Resolved }
public enum ConflictScope { Individual, Group, Settlement }

public sealed partial class Tile
{
    [JsonRequired] public WildlifeKind Wildlife { get; set; }
    [JsonRequired] public double WildlifePopulation { get; set; }
    // A zero-valued population set is the safe default for existing format 8 worlds.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public WildlifePopulations OtherWildlife { get; set; }

    [JsonIgnore] public int WildlifeMask => OtherWildlife.ActiveMask | (WildlifePopulation > 0 ? 1 << (int)Wildlife : 0);

    public double AnimalPopulation(WildlifeKind kind) => kind == Wildlife ? WildlifePopulation : OtherWildlife.Get(kind);

    internal void SetAnimalPopulation(WildlifeKind kind, double population)
    {
        if (kind == Wildlife) WildlifePopulation = population;
        else if (Wildlife == WildlifeKind.None && population > 0)
        { var others = OtherWildlife; others.Set(kind, 0); OtherWildlife = others; Wildlife = kind; WildlifePopulation = population; }
        else { var others = OtherWildlife; others.Set(kind, population); OtherWildlife = others; }
    }
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

// Value storage avoids allocating a collection for every map tile. Zero fields are omitted.
public struct WildlifePopulations
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public double Rabbit { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public double Deer { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public double Boar { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public double Goat { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public double Wolf { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public double Waterfowl { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public double Fish { get; set; }
    [JsonIgnore] public readonly int ActiveMask => (Rabbit > 0 ? 1 << (int)WildlifeKind.Rabbit : 0) | (Deer > 0 ? 1 << (int)WildlifeKind.Deer : 0) | (Boar > 0 ? 1 << (int)WildlifeKind.Boar : 0) | (Goat > 0 ? 1 << (int)WildlifeKind.Goat : 0) | (Wolf > 0 ? 1 << (int)WildlifeKind.Wolf : 0) | (Waterfowl > 0 ? 1 << (int)WildlifeKind.Waterfowl : 0) | (Fish > 0 ? 1 << (int)WildlifeKind.Fish : 0);
    public readonly double Get(WildlifeKind kind) => kind switch
    {
        WildlifeKind.Rabbit => Rabbit, WildlifeKind.Deer => Deer, WildlifeKind.Boar => Boar, WildlifeKind.Goat => Goat, WildlifeKind.Wolf => Wolf, WildlifeKind.Waterfowl => Waterfowl, WildlifeKind.Fish => Fish, _ => 0
    };
    public void Set(WildlifeKind kind, double population)
    {
        switch (kind)
        {
            case WildlifeKind.Rabbit: Rabbit = population; break;
            case WildlifeKind.Deer: Deer = population; break;
            case WildlifeKind.Boar: Boar = population; break;
            case WildlifeKind.Goat: Goat = population; break;
            case WildlifeKind.Wolf: Wolf = population; break;
            case WildlifeKind.Waterfowl: Waterfowl = population; break;
            case WildlifeKind.Fish: Fish = population; break;
        }
    }
}
