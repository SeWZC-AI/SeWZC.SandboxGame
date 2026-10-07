namespace SeWZC.WorldBox.Core;

/// <summary>按研究领域划分的一组知识项目。</summary>
public sealed class ResearchBranch
{
    private ResearchBranch(string name, bool shared = false, string? lane = null)
    {
        Name = name;
        Shared = shared;
        Lane = shared ? "共同基础" : lane ?? name;
    }

    /// <summary>分支的中文显示名称。</summary>
    public string Name { get; }

    /// <summary>是否属于科技与魔法共用的基础分支。</summary>
    public bool Shared { get; }

    /// <summary>科技树布局使用的分组名称。</summary>
    public string Lane { get; }

    /// <summary>民生与资源共同基础分支。</summary>
    public static ResearchBranch Resources { get; } = new("民生与资源", true);

    /// <summary>城建与公共卫生共同基础分支。</summary>
    public static ResearchBranch PublicHealth { get; } = new("城建与公共卫生", true);

    /// <summary>知识与勘察共同基础分支。</summary>
    public static ResearchBranch Survey { get; } = new("知识与勘察", true);

    /// <summary>工业与能源分支。</summary>
    public static ResearchBranch Industry { get; } = new("工业与能源");

    /// <summary>运输与计算分支。</summary>
    public static ResearchBranch Transport { get; } = new("运输与计算");

    /// <summary>知识与通信分支。</summary>
    public static ResearchBranch Communication { get; } = new("知识与通信", lane: "运输与计算");

    /// <summary>工程军备分支。</summary>
    public static ResearchBranch Military { get; } = new("工程军备");

    /// <summary>奥术与修复分支。</summary>
    public static ResearchBranch Restoration { get; } = new("奥术与修复");

    /// <summary>符文与以太分支。</summary>
    public static ResearchBranch Runes { get; } = new("符文与以太");

    /// <summary>元素与结界分支。</summary>
    public static ResearchBranch Warding { get; } = new("元素与结界");

    /// <summary>返回分支的中文显示名称。</summary>
    public override string ToString()
    {
        return Name;
    }
}
