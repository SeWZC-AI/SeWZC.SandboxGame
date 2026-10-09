namespace SeWZC.WorldBox.Core.Runtime;

/// <summary>Tile 的引擎内定位引用；每次写入提交新的不可变状态。</summary>
internal sealed partial class TileCursor : StateReference<Tile>
{
    public TileCursor(Tile value) : base(value) { }

    public WildlifeKind Wildlife => Value.Wildlife;

    public int WildlifeMask => Value.WildlifeMask;

    public LandImprovement Improvement
    {
        get => Value.Improvement;
        set
        {
            if (!EqualityComparer<LandImprovement>.Default.Equals(Value.Improvement, value))
                ReplaceChanged(Value with { Improvement = value });
        }
    }

    public ResourceKind? Deposit
    {
        get => Value.Deposit;
        set
        {
            if (!EqualityComparer<ResourceKind?>.Default.Equals(Value.Deposit, value))
                ReplaceChanged(Value with { Deposit = value });
        }
    }

    public double DepositAmount
    {
        get => Value.DepositAmount;
        set
        {
            if (!EqualityComparer<double>.Default.Equals(Value.DepositAmount, value))
                ReplaceChanged(Value with { DepositAmount = value });
        }
    }

    public bool DepositDiscovered
    {
        get => Value.DepositDiscovered;
        set
        {
            if (!EqualityComparer<bool>.Default.Equals(Value.DepositDiscovered, value))
                ReplaceChanged(Value with { DepositDiscovered = value });
        }
    }

    public double Harvested => Value.Harvested;

    public int ClaimedSettlementId
    {
        get => Value.ClaimedSettlementId;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(Value.ClaimedSettlementId, value))
                ReplaceChanged(Value with { ClaimedSettlementId = value });
        }
    }

    public BridgeDirection BridgeDirection => Value.BridgeDirection;

    public long WaterDrawTick
    {
        get => Value.WaterDrawTick;
        set
        {
            if (!EqualityComparer<long>.Default.Equals(Value.WaterDrawTick, value))
                ReplaceChanged(Value with { WaterDrawTick = value });
        }
    }

    public double WaterDrawn
    {
        get => Value.WaterDrawn;
        set
        {
            if (!EqualityComparer<double>.Default.Equals(Value.WaterDrawn, value))
                ReplaceChanged(Value with { WaterDrawn = value });
        }
    }

    public double NaturalWaterYield
    {
        get => Value.NaturalWaterYield;
        set
        {
            if (!EqualityComparer<double>.Default.Equals(Value.NaturalWaterYield, value))
                ReplaceChanged(Value with { NaturalWaterYield = value });
        }
    }

    public PlantCoverage Plants
    {
        get => Value.Plants;
        set
        {
            if (!EqualityComparer<PlantCoverage>.Default.Equals(Value.Plants, value))
                ReplaceChanged(Value with { Plants = value });
        }
    }

    public TerrainType Terrain
    {
        get => Value.Terrain;
        set => Replace(Value.WithTerrain(value));
    }

    public byte Elevation
    {
        get => Value.Elevation;
        set
        {
            if (!EqualityComparer<byte>.Default.Equals(Value.Elevation, value))
                ReplaceChanged(Value with { Elevation = value });
        }
    }

    public byte Fertility
    {
        get => Value.Fertility;
        set
        {
            if (!EqualityComparer<byte>.Default.Equals(Value.Fertility, value))
                ReplaceChanged(Value with { Fertility = value });
        }
    }

    public int NationId
    {
        get => Value.NationId;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(Value.NationId, value))
                ReplaceChanged(Value with { NationId = value });
        }
    }

    public int SettlementId
    {
        get => Value.SettlementId;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(Value.SettlementId, value))
                ReplaceChanged(Value with { SettlementId = value });
        }
    }

    public int FireTicks
    {
        get => Value.FireTicks;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(Value.FireTicks, value))
                ReplaceChanged(Value with { FireTicks = value });
        }
    }

    public int DroughtTicks
    {
        get => Value.DroughtTicks;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(Value.DroughtTicks, value))
                ReplaceChanged(Value with { DroughtTicks = value });
        }
    }

    public bool IsWalkable => Value.IsWalkable;

    public double Rainfall
    {
        get => Value.Rainfall;
        set
        {
            if (!EqualityComparer<double>.Default.Equals(Value.Rainfall, value))
                ReplaceChanged(Value with { Rainfall = value });
        }
    }

    public byte RiverWidth
    {
        get => Value.RiverWidth;
        set
        {
            if (!EqualityComparer<byte>.Default.Equals(Value.RiverWidth, value))
                ReplaceChanged(Value with { RiverWidth = value });
        }
    }

    public long FireSuppressionTick => Value.FireSuppressionTick;

    public int FireSuppressed
    {
        get => Value.FireSuppressed;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(Value.FireSuppressed, value))
                ReplaceChanged(Value with { FireSuppressed = value });
        }
    }

    public byte RoadLevel
    {
        get => Value.RoadLevel;
        set
        {
            if (!EqualityComparer<byte>.Default.Equals(Value.RoadLevel, value))
                ReplaceChanged(Value with { RoadLevel = value });
        }
    }

    public double ResourceAmount
    {
        get => Value.ResourceAmount;
        set
        {
            if (!EqualityComparer<double>.Default.Equals(Value.ResourceAmount, value))
                ReplaceChanged(Value with { ResourceAmount = value });
        }
    }
}
