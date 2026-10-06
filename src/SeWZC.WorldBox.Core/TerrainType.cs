namespace SeWZC.WorldBox.Core;

/// <summary>地格的地形类别，决定通行、资源和生态规则。</summary>
public enum TerrainType
{
    /// <summary>深海。</summary>
    DeepWater = 0,
    /// <summary>浅海。</summary>
    Water = 1,
    /// <summary>沙地。</summary>
    Sand = 2,
    /// <summary>草地。</summary>
    Grass = 3,
    /// <summary>森林。</summary>
    Forest = 4,
    /// <summary>山地。</summary>
    Mountain = 5,
    /// <summary>雪地。</summary>
    Snow = 6,
    /// <summary>丘陵。</summary>
    Hills = 7,
    /// <summary>湿地。</summary>
    Wetland = 8,
    /// <summary>沙漠。</summary>
    Desert = 9,
    /// <summary>河流。</summary>
    River = 10,
    /// <summary>苔原。</summary>
    Tundra = 11,
    /// <summary>湖泊。</summary>
    Lake = 12,
    /// <summary>旱沃地。</summary>
    DryFertile = 13,
    /// <summary>小溪。</summary>
    Stream,
    /// <summary>大江。</summary>
    LargeRiver,
    /// <summary>草甸。</summary>
    Meadow,
    /// <summary>疏林。</summary>
    Woodland,
    /// <summary>雨林。</summary>
    Rainforest,
    /// <summary>稀树草原。</summary>
    Savanna,
    /// <summary>灌丛。</summary>
    Scrub,
    /// <summary>冲积平原。</summary>
    Floodplain,
    /// <summary>高山草甸。</summary>
    AlpineMeadow,
}
