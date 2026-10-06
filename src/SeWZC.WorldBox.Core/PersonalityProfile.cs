namespace SeWZC.WorldBox.Core;

/// <summary>用于目标评分及结构化经历影响的性格权重。</summary>
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
