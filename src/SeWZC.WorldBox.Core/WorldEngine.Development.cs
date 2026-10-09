namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    /// <summary>取得国家指定或根据本地文化和魔法开关推导的发展方向。</summary>
    /// <param name="settlementId">聚落 ID。</param>
    public DevelopmentFocus GetDevelopmentFocus(int settlementId)
    {
        var town = RequireTown(settlementId);
        var nation = Nations.FirstOrDefault(n => n.Value.Id == town.Value.NationId);
        if (nation?.Value is { DevelopmentFocus: not DevelopmentFocus.Automatic })
            return nation.Value.DevelopmentFocus;
        var culture = GetCulture(town.Value.CultureId);
        return !Society.MagicEnabled || culture.Innovation >= culture.NatureAffinity
            ? DevelopmentFocus.Technology
            : DevelopmentFocus.MagicPractice;
    }

    /// <summary>返回发展方向的中文名称。</summary>
    /// <param name="focus">未来规划采用的发展方向。</param>
    public static string DevelopmentFocusName(DevelopmentFocus focus)
    {
        return focus switch
        {
            DevelopmentFocus.Technology => "科技发展",
            DevelopmentFocus.MagicPractice => "法术传承",
            DevelopmentFocus.ArcaneIndustry => "魔法工艺",
            DevelopmentFocus.Integrated => "兼修科技与魔法",
            _ => "依当地文化选择",
        };
    }

    /// <summary>设置国家未来规划的发展方向，并记录变化。</summary>
    /// <param name="nationId">国家 ID。</param>
    /// <param name="focus">未来规划采用的发展方向。</param>
    public void SetDevelopmentFocus(int nationId, DevelopmentFocus focus)
    {
        if (!Enum.IsDefined(focus))
            throw new ArgumentOutOfRangeException(nameof(focus));
        var nation = Nations.FirstOrDefault(n => n.Value.Id == nationId) ?? throw new ArgumentException("国家不存在");
        if (nation.Value.DevelopmentFocus == focus)
            return;
        nation.Replace(nation.Value with { DevelopmentFocus = focus });
        AddEvent(WorldEventKind.Editor, $"{nation.Value.Name}的发展方向调整为{DevelopmentFocusName(focus)}。");
    }
}
