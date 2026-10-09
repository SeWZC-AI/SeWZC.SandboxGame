namespace SeWZC.WorldBox.Core;

/// <summary>模拟日历的单位换算及日内作息。</summary>
public static class SimulationTime
{
    /// <summary>一年包含的月数。</summary>
    public const int MonthsPerYear = 4;

    /// <summary>一月包含的日数。</summary>
    public const int DaysPerMonth = 5;

    /// <summary>一天包含的模拟步数。</summary>
    public const int TicksPerDay = 24;

    /// <summary>一年包含的日数。</summary>
    public const int DaysPerYear = MonthsPerYear * DaysPerMonth;

    /// <summary>一月包含的模拟步数。</summary>
    public const int TicksPerMonth = DaysPerMonth * TicksPerDay;

    /// <summary>一年包含的模拟步数。</summary>
    public const int TicksPerYear = MonthsPerYear * TicksPerMonth;

    /// <summary>早晨开始日常活动的日内步数。</summary>
    public const int WakeTick = TicksPerDay / 4;

    /// <summary>傍晚开始返家的日内步数。</summary>
    public const int ReturnHomeTick = TicksPerDay * 3 / 4;

    /// <summary>夜晚开始睡眠的日内步数。</summary>
    public const int SleepTick = TicksPerDay * 5 / 6;

    /// <summary>返回从零开始的模拟日序。</summary>
    /// <param name="tick">从零开始的模拟步序。</param>
    public static long DayIndex(long tick)
    {
        return tick / TicksPerDay;
    }

    /// <summary>返回一天内从零开始的模拟步数。</summary>
    /// <param name="tick">从零开始的模拟步序。</param>
    public static int TimeOfDay(long tick)
    {
        return (int)(tick % TicksPerDay);
    }
}
