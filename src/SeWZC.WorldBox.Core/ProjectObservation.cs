using System.Collections.Immutable;

namespace SeWZC.WorldBox.Core;

/// <summary>项目的不可变进展观测记录，用于估算完工时间。</summary>
public sealed record ProjectObservation
{
    /// <summary>当前项目开始时关联的事件 ID。</summary>
    public int StartEventId { get; init; }

    /// <summary>已经为项目提供劳动的居民 ID。</summary>
    public ImmutableArray<int> Contributors { get; init; } = [];

    /// <summary>采样时采用的发展速率倍率，用于判定样本是否仍适用。</summary>
    public double DevelopmentRate { get; init; } = 1;

    /// <summary>按时间保存的近期进度样本。</summary>
    public ImmutableArray<ProgressSample> Samples { get; init; } = [];

    /// <summary>返回登记劳动者后的观察记录；已有劳动者或记录已满时返回原记录。</summary>
    /// <param name="residentId">实际提供劳动的居民 ID。</param>
    public ProjectObservation AddContributor(int residentId)
    {
        return Contributors.Length >= 32 || Contributors.Contains(residentId)
            ? this
            : this with { Contributors = Contributors.Add(residentId) };
    }

    /// <summary>按四日间隔采样；速率变化时丢弃旧样本，最多保留七条。</summary>
    /// <param name="tick">当前模拟日序。</param>
    /// <param name="rate">采样采用的发展速率倍率。</param>
    /// <param name="progress">项目累计工作量。</param>
    public ProjectObservation Observe(long tick, double rate, double progress)
    {
        var samples = DevelopmentRate == rate ? Samples : [];
        if (samples.Length > 0 && tick - samples[^1].Tick < 4)
            return this;
        if (samples.Length == 7)
            samples = samples.RemoveAt(0);
        return this with
        {
            DevelopmentRate = rate, Samples = samples.Add(new ProgressSample { Tick = tick, Progress = progress }),
        };
    }
}
