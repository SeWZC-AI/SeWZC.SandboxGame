namespace SeWZC.WorldBox.Core;

/// <summary>影响居民行为倾向的文化定义。</summary>
public sealed record CultureDefinition
{
    /// <summary>文化的稳定 ID。</summary>
    public int Id { get; init; }

    /// <summary>文化的显示名称。</summary>
    public string Name { get; init; } = "";

    /// <summary>合作倾向权重，范围为 0 至 1。</summary>
    public double Cooperation { get; init; } = 0.5;

    /// <summary>创新倾向权重，范围为 0 至 1。</summary>
    public double Innovation { get; init; } = 0.5;

    /// <summary>亲自然倾向权重，范围为 0 至 1。</summary>
    public double NatureAffinity { get; init; } = 0.5;
}
