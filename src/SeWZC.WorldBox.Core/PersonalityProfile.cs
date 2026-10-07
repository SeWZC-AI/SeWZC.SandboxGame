namespace SeWZC.WorldBox.Core;

/// <summary>居民在自主决策中使用的性格倾向。</summary>
public sealed record PersonalityProfile
{
    /// <summary>勇气权重，范围为 0 至 1。</summary>
    public double Courage { get; init; } = 0.5;

    /// <summary>勤劳权重，范围为 0 至 1。</summary>
    public double Diligence { get; init; } = 0.5;

    /// <summary>社交权重，范围为 0 至 1。</summary>
    public double Sociability { get; init; } = 0.5;

    /// <summary>进取权重，范围为 0 至 1。</summary>
    public double Ambition { get; init; } = 0.5;
}
