namespace SeWZC.WorldBox.Core;

public sealed partial record Tile
{
    internal Tile WithImprovement(LandImprovement value) =>
        EqualityComparer<LandImprovement>.Default.Equals(Improvement, value) ? this : this with { Improvement = value };

    internal Tile WithDepositAmount(double value) =>
        EqualityComparer<double>.Default.Equals(DepositAmount, value) ? this : this with { DepositAmount = value };

    internal Tile WithDepositDiscovered(bool value) =>
        EqualityComparer<bool>.Default.Equals(DepositDiscovered, value) ? this : this with { DepositDiscovered = value };

    internal Tile WithClaimedSettlementId(int value) =>
        EqualityComparer<int>.Default.Equals(ClaimedSettlementId, value) ? this : this with { ClaimedSettlementId = value };

    internal Tile WithWaterDrawTick(long value) =>
        EqualityComparer<long>.Default.Equals(WaterDrawTick, value) ? this : this with { WaterDrawTick = value };

    internal Tile WithWaterDrawn(double value) =>
        EqualityComparer<double>.Default.Equals(WaterDrawn, value) ? this : this with { WaterDrawn = value };

    internal Tile WithNaturalWaterYield(double value) =>
        EqualityComparer<double>.Default.Equals(NaturalWaterYield, value) ? this : this with { NaturalWaterYield = value };

    internal Tile WithPlants(PlantCoverage value) =>
        EqualityComparer<PlantCoverage>.Default.Equals(Plants, value) ? this : this with { Plants = value };

    internal Tile WithElevation(byte value) =>
        EqualityComparer<byte>.Default.Equals(Elevation, value) ? this : this with { Elevation = value };

    internal Tile WithFertility(byte value) =>
        EqualityComparer<byte>.Default.Equals(Fertility, value) ? this : this with { Fertility = value };

    internal Tile WithNationId(int value) =>
        EqualityComparer<int>.Default.Equals(NationId, value) ? this : this with { NationId = value };

    internal Tile WithSettlementId(int value) =>
        EqualityComparer<int>.Default.Equals(SettlementId, value) ? this : this with { SettlementId = value };

    internal Tile WithFireTicks(int value) =>
        EqualityComparer<int>.Default.Equals(FireTicks, value) ? this : this with { FireTicks = value };

    internal Tile WithDroughtTicks(int value) =>
        EqualityComparer<int>.Default.Equals(DroughtTicks, value) ? this : this with { DroughtTicks = value };

    internal Tile WithRoadLevel(byte value) =>
        EqualityComparer<byte>.Default.Equals(RoadLevel, value) ? this : this with { RoadLevel = value };

    internal Tile WithResourceAmount(double value) =>
        EqualityComparer<double>.Default.Equals(ResourceAmount, value) ? this : this with { ResourceAmount = value };
}
