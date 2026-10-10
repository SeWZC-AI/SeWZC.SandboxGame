namespace SeWZC.WorldBox.Core;

/// <summary>居民日常安排与提前返家的可配置规则。</summary>
public static class DailyRoutineRules
{
    /// <summary>预计抵家与正式入睡之间保留的刻数。</summary>
    public const int ReturnMarginTicks = 1;
    /// <summary>随身粮水低于多少日储备时安排重新评估。</summary>
    public const int SupplyReserveDays = 2;
    /// <summary>普通需求安排的最短复评间隔。</summary>
    public const int MinimumReviewTicks = 4;
    /// <summary>安排体力休息时采用的疲劳百分比。</summary>
    public const double RestFatigueThreshold = 60;
}
