using System.Diagnostics;
using SeWZC.WorldBox.Core;

namespace SeWZC.WorldBox.UI.Services;

/// <summary>按速度、积累时间和单帧预算推进模拟，统计实际推进的模拟日。</summary>
internal sealed class SimulationScheduler
{
    internal const double TickSeconds = .2;
    internal const int UnlimitedSpeed = 0;
    private readonly RecentActivityCounter _simulationDays = new();
    private double _lastStepMilliseconds;
    internal double Accumulator { get; private set; }
    internal double LastMapRefreshMilliseconds { get; set; }
    internal double TickFraction => Math.Clamp(Accumulator / TickSeconds, 0, .999999);
    internal bool IsCatchingUp => Accumulator > .4;

    internal double DaysPerSecond(long timestamp)
    {
        return _simulationDays.Count(timestamp) / (double)SimulationTime.TicksPerDay;
    }

    internal void ClearPendingTime()
    {
        Accumulator = 0;
    }

    internal void ClearActivity()
    {
        _simulationDays.Clear();
    }

    internal void Reset()
    {
        Accumulator = 0;
        _lastStepMilliseconds = LastMapRefreshMilliseconds = 0;
        _simulationDays.Clear();
    }

    internal double NextDelay(int speed, bool idle, double callbackMilliseconds)
    {
        return idle ? 50
            : speed == UnlimitedSpeed ? 1
            : Math.Clamp((TickSeconds - Accumulator) * 1000 / speed - callbackMilliseconds, 1, 50);
    }

    internal int Advance(WorldEngine engine, int speed, double elapsed)
    {
        var unlimited = speed == UnlimitedSpeed;
        // 五倍档最多保留八 tick 欠账，让偶发慢帧之后仍能追回真实模拟步。
        Accumulator = unlimited
            ? 0
            : Math.Min((speed == 5 ? 8 : 4) * TickSeconds,
                Accumulator + elapsed * speed);
        var work = Stopwatch.GetTimestamp();
        var count = 0;
        // 五倍档和不限速档预留 64 ms；不限速不等待 tick 截止时间，也不限制批次步数，预算后让出界面线程。
        var budgetMilliseconds = speed == 1 ? 12 : speed == 5 || unlimited ? 64 : 48;
        while ((unlimited || Accumulator >= TickSeconds) && (unlimited || count < 4))
        {
            // 追加模拟步前先预留上一轮地图刷新耗时；首步再贵也须推进，避免世界停滞。
            if (count > 0 && Stopwatch.GetElapsedTime(work).TotalMilliseconds
                + _lastStepMilliseconds + LastMapRefreshMilliseconds > budgetMilliseconds)
                break;
            var stepStarted = Stopwatch.GetTimestamp();
            engine.Step();
            _simulationDays.Record(Stopwatch.GetTimestamp());
            if (!unlimited)
                Accumulator -= TickSeconds;
            count++;
            _lastStepMilliseconds = Stopwatch.GetElapsedTime(stepStarted).TotalMilliseconds;
            if (Stopwatch.GetElapsedTime(work).TotalMilliseconds + LastMapRefreshMilliseconds >
                budgetMilliseconds)
                break;
        }

        return count;
    }
}
