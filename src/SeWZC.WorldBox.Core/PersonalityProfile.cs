namespace SeWZC.WorldBox.Core;

/// <summary>居民在自主决策中使用的性格倾向。</summary>
public sealed class PersonalityProfile
{
    /// <summary>勇气权重，范围为 0 至 1。</summary>
    public double Courage { get; set; } = 0.5;

    /// <summary>勤劳权重，范围为 0 至 1。</summary>
    public double Diligence { get; set; } = 0.5;

    /// <summary>社交权重，范围为 0 至 1。</summary>
    public double Sociability { get; set; } = 0.5;

    /// <summary>进取权重，范围为 0 至 1。</summary>
    public double Ambition { get; set; } = 0.5;
}
