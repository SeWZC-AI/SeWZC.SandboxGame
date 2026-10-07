namespace SeWZC.WorldBox.Core;

/// <summary>一个种族对某类地形的适应参数。</summary>
/// <param name="Habitable">该地形是否适合此种族定居。</param>
/// <param name="Movement">此种族在该地形上的移动成本倍率，越低越快。</param>
/// <param name="Productivity">此种族在该地形上的劳动产量倍率。</param>
public readonly record struct TerrainAdaptation(bool Habitable, double Movement, double Productivity);
