namespace SeWZC.WorldBox.Core;

/// <summary>影响居民行为倾向的文化定义。</summary>
public sealed class CultureDefinition
{
    /// <summary>文化的稳定 ID。</summary>
    public int Id { get; set; }

    /// <summary>文化的显示名称。</summary>
    public string Name { get; set; } = "";

    /// <summary>合作倾向权重，范围为 0 至 1。</summary>
    public double Cooperation { get; set; } = 0.5;

    /// <summary>创新倾向权重，范围为 0 至 1。</summary>
    public double Innovation { get; set; } = 0.5;

    /// <summary>亲自然倾向权重，范围为 0 至 1。</summary>
    public double NatureAffinity { get; set; } = 0.5;
}
