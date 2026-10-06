namespace SeWZC.WorldBox.Core;

/// <summary>居民接触某种文化的累计程度和最近接触时间。</summary>
public sealed class CulturalContact
{
    /// <summary>关联居民的稳定 ID。</summary>
    public int ResidentId { get; set; }

    /// <summary>所接触文化的稳定 ID。</summary>
    public int CultureId { get; set; }

    /// <summary>居民接触该文化的累计程度。</summary>
    public double Exposure { get; set; }

    /// <summary>最近接触该文化的模拟日序。</summary>
    public long LastContactTick { get; set; }
}
