using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

public enum BridgeDirection { Horizontal, Vertical }

public sealed partial class Building
{
    [JsonRequired] public int Level { get; set; } = 1;
    public double UpgradeProgress { get; set; }
    public double UpgradeRequired { get; set; }
    public BridgeDirection Direction { get; set; }
    public BridgeDirection? PendingDirection { get; set; }
    [JsonIgnore] public bool IsUpgrading => UpgradeRequired > 0;
    [JsonIgnore] public double Efficiency => 1 + (Level - 1) * .25;
}

public sealed partial class Resident
{
    [JsonRequired] public double Thirst { get; set; }
}

public sealed partial class Tile
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int ClaimedSettlementId { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public BridgeDirection BridgeDirection { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public byte BridgeLevel { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public long WaterDrawTick { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public double WaterDrawn { get; set; }
    [JsonPropertyName("w"), JsonRequired] public double NaturalWaterYield { get; set; }
    [JsonPropertyName("p"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public PlantCoverage Plants { get; set; }
}

public sealed partial class Settlement
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public bool FoundationPending { get; set; }
    [JsonRequired] public int MaxClaimRadius { get; set; } = 6;
}

public struct PlantCoverage
{
    [JsonPropertyName("t"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public double Trees { get; set; }
    [JsonPropertyName("s"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public double Shrubs { get; set; }
    [JsonPropertyName("g"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public double Grass { get; set; }
    [JsonPropertyName("r"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public double Reeds { get; set; }
    [JsonIgnore] public readonly double Total => Trees + Shrubs + Grass + Reeds;
    public readonly double Get(PlantKind kind) => kind switch
    { PlantKind.Trees => Trees, PlantKind.Shrubs => Shrubs, PlantKind.Grass => Grass, PlantKind.Reeds => Reeds, _ => 0 };
    public void Set(PlantKind kind, double cover)
    {
        switch (kind)
        { case PlantKind.Trees: Trees = cover; break; case PlantKind.Shrubs: Shrubs = cover; break;
          case PlantKind.Grass: Grass = cover; break; case PlantKind.Reeds: Reeds = cover; break; }
    }
}

public sealed partial class Army
{
    [JsonRequired] public double WaterSupplies { get; set; }
}
