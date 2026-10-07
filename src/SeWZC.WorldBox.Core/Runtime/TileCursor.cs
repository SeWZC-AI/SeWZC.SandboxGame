using System.Collections.Immutable;

namespace SeWZC.WorldBox.Core.Runtime;

/// <summary>Tile 的引擎内定位引用；每次写入提交新的不可变状态。</summary>
internal sealed partial class TileCursor : StateCursor<global::SeWZC.WorldBox.Core.Tile>
{
    public TileCursor() : this(new()) { }
    public TileCursor(global::SeWZC.WorldBox.Core.Tile value) : base(value) { }
    public static implicit operator global::SeWZC.WorldBox.Core.Tile(TileCursor cursor) => cursor.Value;
    public static implicit operator TileCursor(global::SeWZC.WorldBox.Core.Tile value) => new(value);
    public WildlifeKind Wildlife { get => Value.Wildlife; set { if (!EqualityComparer<WildlifeKind>.Default.Equals(Value.Wildlife, value)) ReplaceChanged(Value with { Wildlife = value }); } }
    public double WildlifePopulation { get => Value.WildlifePopulation; set { if (!EqualityComparer<double>.Default.Equals(Value.WildlifePopulation, value)) ReplaceChanged(Value with { WildlifePopulation = value }); } }
    public WildlifePopulations OtherWildlife { get => Value.OtherWildlife; set { if (!EqualityComparer<WildlifePopulations>.Default.Equals(Value.OtherWildlife, value)) ReplaceChanged(Value with { OtherWildlife = value }); } }
    public int WildlifeMask => Value.WildlifeMask;
    public LandImprovement Improvement { get => Value.Improvement; set { if (!EqualityComparer<LandImprovement>.Default.Equals(Value.Improvement, value)) ReplaceChanged(Value with { Improvement = value }); } }
    public ResourceKind? Deposit { get => Value.Deposit; set { if (!EqualityComparer<ResourceKind?>.Default.Equals(Value.Deposit, value)) ReplaceChanged(Value with { Deposit = value }); } }
    public double DepositAmount { get => Value.DepositAmount; set { if (!EqualityComparer<double>.Default.Equals(Value.DepositAmount, value)) ReplaceChanged(Value with { DepositAmount = value }); } }
    public bool DepositDiscovered { get => Value.DepositDiscovered; set { if (!EqualityComparer<bool>.Default.Equals(Value.DepositDiscovered, value)) ReplaceChanged(Value with { DepositDiscovered = value }); } }
    public long LastHarvestTick { get => Value.LastHarvestTick; set { if (!EqualityComparer<long>.Default.Equals(Value.LastHarvestTick, value)) ReplaceChanged(Value with { LastHarvestTick = value }); } }
    public double Harvested { get => Value.Harvested; set { if (!EqualityComparer<double>.Default.Equals(Value.Harvested, value)) ReplaceChanged(Value with { Harvested = value }); } }
    public int ClaimedSettlementId { get => Value.ClaimedSettlementId; set { if (!EqualityComparer<int>.Default.Equals(Value.ClaimedSettlementId, value)) ReplaceChanged(Value with { ClaimedSettlementId = value }); } }
    public BridgeDirection BridgeDirection { get => Value.BridgeDirection; set { if (!EqualityComparer<BridgeDirection>.Default.Equals(Value.BridgeDirection, value)) ReplaceChanged(Value with { BridgeDirection = value }); } }
    public byte BridgeLevel { get => Value.BridgeLevel; set { if (!EqualityComparer<byte>.Default.Equals(Value.BridgeLevel, value)) ReplaceChanged(Value with { BridgeLevel = value }); } }
    public long WaterDrawTick { get => Value.WaterDrawTick; set { if (!EqualityComparer<long>.Default.Equals(Value.WaterDrawTick, value)) ReplaceChanged(Value with { WaterDrawTick = value }); } }
    public double WaterDrawn { get => Value.WaterDrawn; set { if (!EqualityComparer<double>.Default.Equals(Value.WaterDrawn, value)) ReplaceChanged(Value with { WaterDrawn = value }); } }
    public double NaturalWaterYield { get => Value.NaturalWaterYield; set { if (!EqualityComparer<double>.Default.Equals(Value.NaturalWaterYield, value)) ReplaceChanged(Value with { NaturalWaterYield = value }); } }
    public PlantCoverage Plants { get => Value.Plants; set { if (!EqualityComparer<PlantCoverage>.Default.Equals(Value.Plants, value)) ReplaceChanged(Value with { Plants = value }); } }
    public TerrainType Terrain { get => Value.Terrain; set => Replace(Value.WithTerrain(value)); }
    public byte Elevation { get => Value.Elevation; set { if (!EqualityComparer<byte>.Default.Equals(Value.Elevation, value)) ReplaceChanged(Value with { Elevation = value }); } }
    public byte Fertility { get => Value.Fertility; set { if (!EqualityComparer<byte>.Default.Equals(Value.Fertility, value)) ReplaceChanged(Value with { Fertility = value }); } }
    public int NationId { get => Value.NationId; set { if (!EqualityComparer<int>.Default.Equals(Value.NationId, value)) ReplaceChanged(Value with { NationId = value }); } }
    public int SettlementId { get => Value.SettlementId; set { if (!EqualityComparer<int>.Default.Equals(Value.SettlementId, value)) ReplaceChanged(Value with { SettlementId = value }); } }
    public int FireTicks { get => Value.FireTicks; set { if (!EqualityComparer<int>.Default.Equals(Value.FireTicks, value)) ReplaceChanged(Value with { FireTicks = value }); } }
    public int DroughtTicks { get => Value.DroughtTicks; set { if (!EqualityComparer<int>.Default.Equals(Value.DroughtTicks, value)) ReplaceChanged(Value with { DroughtTicks = value }); } }
    public bool IsWalkable => Value.IsWalkable;
    public double Rainfall { get => Value.Rainfall; set { if (!EqualityComparer<double>.Default.Equals(Value.Rainfall, value)) ReplaceChanged(Value with { Rainfall = value }); } }
    public byte RiverWidth { get => Value.RiverWidth; set { if (!EqualityComparer<byte>.Default.Equals(Value.RiverWidth, value)) ReplaceChanged(Value with { RiverWidth = value }); } }
    public long FireSuppressionTick { get => Value.FireSuppressionTick; set { if (!EqualityComparer<long>.Default.Equals(Value.FireSuppressionTick, value)) ReplaceChanged(Value with { FireSuppressionTick = value }); } }
    public int FireSuppressed { get => Value.FireSuppressed; set { if (!EqualityComparer<int>.Default.Equals(Value.FireSuppressed, value)) ReplaceChanged(Value with { FireSuppressed = value }); } }
    public byte RoadLevel { get => Value.RoadLevel; set { if (!EqualityComparer<byte>.Default.Equals(Value.RoadLevel, value)) ReplaceChanged(Value with { RoadLevel = value }); } }
    public double ResourceAmount { get => Value.ResourceAmount; set { if (!EqualityComparer<double>.Default.Equals(Value.ResourceAmount, value)) ReplaceChanged(Value with { ResourceAmount = value }); } }
}
