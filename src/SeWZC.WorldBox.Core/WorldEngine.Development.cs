namespace SeWZC.WorldBox.Core;

public enum DevelopmentFocus { Automatic, Technology, MagicPractice, ArcaneIndustry, Integrated }

public sealed partial class Nation
{
    // Automatic is a deterministic culture-based default for worlds saved before this option.
    public DevelopmentFocus DevelopmentFocus { get; set; }
}

public sealed partial class WorldEngine
{
    public DevelopmentFocus GetDevelopmentFocus(int settlementId)
    {
        var town = RequireTown(settlementId);
        var nation = State.Nations.FirstOrDefault(n => n.Id == town.NationId);
        if (nation is { DevelopmentFocus: not DevelopmentFocus.Automatic }) return nation.DevelopmentFocus;
        var culture = GetCulture(town.CultureId);
        return !State.Society.MagicEnabled || culture.Innovation >= culture.NatureAffinity ? DevelopmentFocus.Technology : DevelopmentFocus.MagicPractice;
    }

    public static string DevelopmentFocusName(DevelopmentFocus focus) => focus switch
    {
        DevelopmentFocus.Technology => "科技发展", DevelopmentFocus.MagicPractice => "法术传承",
        DevelopmentFocus.ArcaneIndustry => "魔法工艺", DevelopmentFocus.Integrated => "兼修科技与魔法", _ => "依当地文化选择"
    };

    public void SetDevelopmentFocus(int nationId, DevelopmentFocus focus)
    {
        if (!Enum.IsDefined(focus)) throw new ArgumentOutOfRangeException(nameof(focus));
        var nation = State.Nations.FirstOrDefault(n => n.Id == nationId) ?? throw new ArgumentException("国家不存在");
        if (nation.DevelopmentFocus == focus) return;
        nation.DevelopmentFocus = focus;
        AddEvent(WorldEventKind.Editor, $"{nation.Name}的发展方向调整为{DevelopmentFocusName(focus)}。");
        // Choices affect future planning; existing research and facilities keep their history.
    }
}
