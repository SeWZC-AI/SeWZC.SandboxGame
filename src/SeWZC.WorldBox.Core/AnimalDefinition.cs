namespace SeWZC.WorldBox.Core;

/// <summary>动物物种的生态特征定义。</summary>
/// <param name="Name">物种显示名称。</param>
/// <param name="Size">物种体型等级。</param>
/// <param name="Diet">物种食性。</param>
/// <param name="Habitats">可组合的适宜栖息地类别。</param>
/// <param name="MinimumFertility">允许该物种生存的最低地格肥力。</param>
/// <param name="MinimumWater">允许该物种生存的最低环境供水量。</param>
public readonly record struct AnimalDefinition(
    string Name,
    AnimalSize Size,
    AnimalDiet Diet,
    AnimalHabitat Habitats,
    byte MinimumFertility,
    double MinimumWater)
{
    /// <summary>按体型折算的相对生物量，用于共享食物和猎物预算。</summary>
    public double BodyMass => Size == AnimalSize.Small ? 1 : Size == AnimalSize.Medium ? 2 : 4;

    /// <summary>物种的适宜栖息地是否包含淡水或海洋。</summary>
    public bool Aquatic => (Habitats & (AnimalHabitat.Fresh | AnimalHabitat.Marine)) != 0;
}
