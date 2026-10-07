namespace SeWZC.WorldBox.Core;

/// <summary>居民与某种文化的接触记录。</summary>
public sealed record CulturalContact
{
    /// <summary>关联居民的稳定 ID。</summary>
    public int ResidentId { get; init; }

    /// <summary>所接触文化的稳定 ID。</summary>
    public int CultureId { get; init; }

    /// <summary>居民接触该文化的累计程度。</summary>
    public double Exposure { get; init; }

    /// <summary>最近接触该文化的模拟日序。</summary>
    public long LastContactTick { get; init; }
}
