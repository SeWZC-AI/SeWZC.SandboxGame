namespace SeWZC.WorldBox.Core;

/// <summary>研究分支及其路线归属和布局分组。</summary>
public sealed class ResearchBranch
{
    private ResearchBranch(string name, bool shared = false, string? lane = null)
    {
        Name = name;
        Shared = shared;
        Lane = shared ? "共同基础" : lane ?? name;
    }

    public string Name { get; }
    public bool Shared { get; }
    public string Lane { get; }
    public static ResearchBranch Resources { get; } = new("民生与资源", true);
    public static ResearchBranch PublicHealth { get; } = new("城建与公共卫生", true);
    public static ResearchBranch Survey { get; } = new("知识与勘察", true);
    public static ResearchBranch Industry { get; } = new("工业与能源");
    public static ResearchBranch Transport { get; } = new("运输与计算");
    public static ResearchBranch Communication { get; } = new("知识与通信", lane: "运输与计算");
    public static ResearchBranch Military { get; } = new("工程军备");
    public static ResearchBranch Restoration { get; } = new("奥术与修复");
    public static ResearchBranch Runes { get; } = new("符文与以太");
    public static ResearchBranch Warding { get; } = new("元素与结界");
    public override string ToString() => Name;
}
