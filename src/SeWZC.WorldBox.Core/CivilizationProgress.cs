namespace SeWZC.WorldBox.Core;

/// <summary>科技或魔法文明目标的研究、设施与实际生产达成情况。</summary>
/// <param name="Magic">是否为魔法文明路线，关闭时为科技路线。</param>
/// <param name="KnownResearch">本地已经掌握的路线研究数。</param>
/// <param name="TotalResearch">该路线要求掌握的研究总数。</param>
/// <param name="ReadyFacilities">已经满足就绪条件的必需设施类别数。</param>
/// <param name="TotalFacilities">文明目标要求的设施类别总数。</param>
/// <param name="MissingResearch">仍未掌握的研究名称。</param>
/// <param name="MissingFacilities">仍未具备就绪设施的类别名称。</param>
/// <param name="UnprovenProduction">尚无实际生产批次记录的必需设施类别名称。</param>
public sealed record CivilizationProgress(
    bool Magic,
    int KnownResearch,
    int TotalResearch,
    int ReadyFacilities,
    int TotalFacilities,
    IReadOnlyList<string> MissingResearch,
    IReadOnlyList<string> MissingFacilities,
    IReadOnlyList<string> UnprovenProduction)
{
    /// <summary>是否已掌握路线全部研究、具备所需设施并证明实际生产。</summary>
    public bool Achieved => KnownResearch == TotalResearch && ReadyFacilities == TotalFacilities &&
                            UnprovenProduction.Count == 0;

    /// <summary>该路线达成时展示的文明名称。</summary>
    public string Name => Magic ? "魔法帝国" : "科技帝国";
}
