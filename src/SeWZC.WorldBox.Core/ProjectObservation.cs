namespace SeWZC.WorldBox.Core;

/// <summary>项目开工事件、贡献者和近期进度样本，用于估算完工时间。</summary>
public sealed class ProjectObservation
{
    /// <summary>当前项目开始时关联的事件 ID。</summary>
    public int StartEventId { get; set; }
    /// <summary>已经为项目提供劳动的居民 ID。</summary>
    public List<int> Contributors { get; set; } = [];
    /// <summary>采样时采用的发展速率倍率，用于判定样本是否仍适用。</summary>
    public double DevelopmentRate { get; set; } = 1;
    /// <summary>按时间保存的近期进度样本。</summary>
    public List<ProgressSample> Samples { get; set; } = [];
}
