namespace SeWZC.WorldBox.Core;

/// <summary>某一模拟日观测到的项目累计进度。</summary>
public sealed class ProgressSample
{
    /// <summary>采样时的模拟日序。</summary>
    public long Tick { get; set; }

    /// <summary>采样时项目已经累计的工作量。</summary>
    public double Progress { get; set; }
}
