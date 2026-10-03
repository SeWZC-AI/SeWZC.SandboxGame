using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

public enum LandImprovement { None, Farmland, MountainPass, Bridge }
public enum TravelMode { Foot, Boat, Aircraft }

public sealed partial class Tile
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public LandImprovement Improvement { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public ResourceKind? Deposit { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public double DepositAmount { get; set; }
    // An observer's map marker; agents still prospect only locally with their own settlement's knowledge.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public bool DepositDiscovered { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public long LastHarvestTick { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public double Harvested { get; set; }
}

public sealed partial class Resident
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public TravelMode TravelMode { get; set; }
}
