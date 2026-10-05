using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

public enum BridgeDirection
{
    Horizontal,
    Vertical,
}

public sealed partial class Building
{
    public string PlanningReason { get; set; } = "";
    public string SiteReason { get; set; } = "";

    [JsonRequired]
    public int Level { get; set; } = 1;

    public double UpgradeProgress { get; set; }
    public double UpgradeRequired { get; set; }
    public BridgeDirection Direction { get; set; }
    public BridgeDirection? PendingDirection { get; set; }

    [JsonIgnore]
    public bool IsUpgrading => UpgradeRequired > 0;

    [JsonIgnore]
    public double Efficiency => 1 + (Level - 1) * .25;
}

public sealed partial class Resident
{
    [JsonRequired]
    public double Thirst { get; set; }
}

public sealed partial class Tile
{
    private BridgeDirection _bridgeDirection;
    private int _claimedSettlementId;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int ClaimedSettlementId
    {
        get => _claimedSettlementId;
        set
        {
            if (_claimedSettlementId == value) return;
            _claimedSettlementId = value;
            TerritoryCounts?.InvalidateClaims();
        }
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public BridgeDirection BridgeDirection
    {
        get => _bridgeDirection;
        set
        {
            if (_bridgeDirection == value) return;
            _bridgeDirection = value;
            TerritoryCounts?.InvalidateTraversal();
        }
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public byte BridgeLevel { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public long WaterDrawTick { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double WaterDrawn { get; set; }

    [JsonPropertyName("w")]
    [JsonRequired]
    public double NaturalWaterYield { get; set; }

    [JsonPropertyName("p")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public PlantCoverage Plants { get; set; }
}

public sealed partial class Settlement
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool FoundationPending { get; set; }

    [JsonRequired]
    public int MaxClaimRadius { get; set; } = 6;
}

public struct PlantCoverage : IEquatable<PlantCoverage>
{
    [JsonPropertyName("t")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Trees { get; set; }

    [JsonPropertyName("s")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Shrubs { get; set; }

    [JsonPropertyName("g")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Grass { get; set; }

    [JsonPropertyName("r")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Reeds { get; set; }

    public readonly bool Equals(PlantCoverage other)
    {
        return Trees.Equals(other.Trees) && Shrubs.Equals(other.Shrubs)
                                         && Grass.Equals(other.Grass) && Reeds.Equals(other.Reeds);
    }

    public readonly override bool Equals(object? obj)
    {
        return obj is PlantCoverage other && Equals(other);
    }

    public readonly override int GetHashCode()
    {
        return HashCode.Combine(Trees, Shrubs, Grass, Reeds);
    }

    [JsonIgnore]
    public readonly double Total => Trees + Shrubs + Grass + Reeds;

    public readonly double Get(PlantKind kind)
    {
        return kind switch
        {
            PlantKind.Trees => Trees, PlantKind.Shrubs => Shrubs, PlantKind.Grass => Grass,
            PlantKind.Reeds => Reeds, _ => 0,
        };
    }

    public void Set(PlantKind kind, double cover)
    {
        switch (kind)
        {
            case PlantKind.Trees: Trees = cover; break;
            case PlantKind.Shrubs: Shrubs = cover; break;
            case PlantKind.Grass: Grass = cover; break;
            case PlantKind.Reeds: Reeds = cover; break;
        }
    }
}

public sealed partial class Army
{
    [JsonRequired]
    public double WaterSupplies { get; set; }
}
