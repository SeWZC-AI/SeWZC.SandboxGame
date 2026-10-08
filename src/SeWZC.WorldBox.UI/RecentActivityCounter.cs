using System.Diagnostics;

namespace SeWZC.WorldBox.UI;

/// <summary>使用循环队列统计最近一秒内的活动次数。</summary>
internal sealed class RecentActivityCounter
{
    private readonly Queue<long> _timestamps = new();

    internal void Record(long timestamp)
    {
        Expire(timestamp);
        _timestamps.Enqueue(timestamp);
    }

    internal int Count(long timestamp)
    {
        Expire(timestamp);
        return _timestamps.Count;
    }

    internal void Clear() => _timestamps.Clear();

    private void Expire(long timestamp)
    {
        var cutoff = timestamp - Stopwatch.Frequency;
        while (_timestamps.TryPeek(out var oldest) && oldest <= cutoff)
            _timestamps.Dequeue();
    }
}
